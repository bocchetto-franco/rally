using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Opt-in Windows measurement. No files, auto-driving or recorders in normal play.</summary>
[DefaultExecutionOrder(30000)]
public sealed class RallyFrameProbe : MonoBehaviour
{
    struct Sample
    {
        public double seconds, wall;
        public long main, render, gc, shader, upload, batches;
        public int frame, checkpoint, collections;
        public float speed, x, z;
        public bool focus, ready;
    }
    readonly Sample[] samples = new Sample[40000];
    static readonly string[] MarkerNames = {
        "WaitForTargetFPS", "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Gfx.ProcessCommands",
        "Gfx.WaitForRenderThread", "GfxDeviceD3D11.WaitForLastPresent", "Physics.Simulate", "Physics.FetchResults",
        "Physics.TriggerEnterExits", "Physics.TriggerStays", "BehaviourUpdate", "BehaviourFixedUpdate",
        "RenderPipelineManager.DoRenderLoop_Internal", "Canvas.SendWillRenderCanvases", "Loading.UpdatePreloading",
        "GarbageCollector.CollectIncremental", "GC.Collect", "Terrain.Update"
    };
    static readonly string[] MarkerColumns = {
        "wait_fps", "wait_present", "present", "gfx_commands", "wait_render", "wait_last_present", "physics",
        "physics_fetch", "trigger_enter", "trigger_stay", "behaviour_update", "behaviour_fixed", "render_loop",
        "canvas", "preloading", "incremental_gc", "full_gc", "terrain"
    };
    readonly ProfilerRecorder[] detail = new ProfilerRecorder[MarkerNames.Length];
    readonly long[] detailSamples = new long[40000 * MarkerNames.Length];
    ProfilerRecorder main, render, gc, shader, upload, batches;
    int count, previousCollections;
    double previous, began, raceBegan;
    string output, circuit;
    int players;
    float duration;
    bool stopping;
    Rigidbody[] cars;
    Gamepad[] pads;
    RallyCheckpointManager timer;
    Vector3[] route;
    int savedPlayers;
    string savedCircuit;
    bool hadPlayers, hadCircuit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-rally-frame-probe");
        if (flag < 0) return;
        var probe = new GameObject("Rally Frame Probe - diagnostic only").AddComponent<RallyFrameProbe>();
        DontDestroyOnLoad(probe.gameObject);
        probe.output = Value(args, "-rally-probe-output", Path.Combine(Application.persistentDataPath, "FrameProbe.csv"));
        probe.circuit = Value(args, "-rally-probe-circuit", "Circuit_03");
        probe.players = Mathf.Clamp(int.Parse(Value(args, "-rally-probe-players", "1"), CultureInfo.InvariantCulture), 1, 2);
        probe.duration = Mathf.Clamp(float.Parse(Value(args, "-rally-probe-seconds", "90"), CultureInfo.InvariantCulture), 10, 240);
        probe.Begin();
    }
    static string Value(string[] args, string key, string fallback)
    {
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
    void Begin()
    {
        if (Array.IndexOf(RallyGameSession.CircuitScenes, circuit) < 0) throw new ArgumentException("Unknown probe circuit");
        hadPlayers = PlayerPrefs.HasKey("Rally.LocalPlayers"); savedPlayers = PlayerPrefs.GetInt("Rally.LocalPlayers", 1);
        hadCircuit = PlayerPrefs.HasKey("Rally.SelectedCircuit"); savedCircuit = PlayerPrefs.GetString("Rally.SelectedCircuit");
        PlayerPrefs.SetInt("Rally.LocalPlayers", players);
        PlayerPrefs.SetString("Rally.SelectedCircuit", RallyGameSession.CircuitNames[Array.IndexOf(RallyGameSession.CircuitScenes, circuit)]);
        RallyGameSession.RestoreSavedState();
        UnityEngine.Random.InitState(83421);
        Application.runInBackground = true;
        // Diagnostic-only, same cap/counters in both comparison runs. Prevent a hidden
        // player from silently filling the buffer with non-rendered 1000fps frames.
        Application.targetFrameRate = 120;
        Profiler.enabled = true;
        // CPU profiling alone does not enable the Rendering module's stats.
        // A valid recorder with permanently zero batches is not render evidence.
        Profiler.SetAreaEnabled(ProfilerArea.Rendering, true);
        Profiler.SetAreaEnabled(ProfilerArea.Memory, true);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        shader = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.CreateGPUProgram", 1);
        upload = ProfilerRecorder.StartNew(ProfilerCategory.Loading, "AsyncUploadManager.Update", 1);
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
        StartDetailRecorders();
        began = previous = Time.realtimeSinceStartupAsDouble;
        previousCollections = GC.CollectionCount(0);
        StartCoroutine(Drive());
    }
    IEnumerator Drive()
    {
        yield return null; yield return null;
        // Baseline bypasses the future loading gate; this records the existing synchronous path.
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-rally-no-load-prep") >= 0)
            SceneManager.LoadScene(circuit);
        else
        {
            // Reflection keeps this diagnostic usable before the new loader is introduced.
            var type = typeof(RallyFrameProbe).Assembly.GetType("RallyRaceLoading");
            if (type == null) SceneManager.LoadScene(circuit);
            else type.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { circuit });
        }
        while (SceneManager.GetActiveScene().name != circuit) yield return null;
        yield return null; yield return null;
        while (Time.timeScale == 0 && Time.realtimeSinceStartupAsDouble - began < 120) yield return null;
        if (players == 2)
            while (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready) yield return null;
        var roots = GameObject.Find("AI_Waypoints").transform;
        route = new Vector3[roots.childCount];
        for (int i = 0; i < route.Length; i++) route[i] = roots.GetChild(i).position;
        var player = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
        cars = players == 1 ? new[] { player.GetComponent<Rigidbody>() } :
            new[] { player.GetComponent<Rigidbody>(), RallySplitScreen.Active.PlayerTwo.GetComponent<Rigidbody>() };
        timer = players == 1 ? FindAnyObjectByType<RallyCheckpointManager>() : RallySplitScreen.Active.TimerOne;
        pads = new Gamepad[players];
        for (int i = 0; i < players; i++)
        {
            pads[i] = InputSystem.AddDevice<Gamepad>();
            var input = cars[i].GetComponent<RallyLocalPlayerInput>() ?? cars[i].gameObject.AddComponent<RallyLocalPlayerInput>();
            input.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, 22f);
            input.PairGamepad(pads[i]);
            // Match the normal local rig: checkpoint triggers dispatch through this reference.
            input.Checkpoints = i == 0 ? timer : RallySplitScreen.Active.TimerTwo;
            var controller = cars[i].GetComponent<JrsVehicleController>(); controller.SetLocalInput(input);
            foreach (var dynamics in FindObjectsByType<RallyVehicleDynamics>())
                if (dynamics.VehicleController == controller) dynamics.SetLocalInput(input);
        }
        // Some markers only register after a scene has exercised their code path.
        StartDetailRecorders();
        raceBegan = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - raceBegan < duration && !timer.IsFinished)
        {
            for (int i = 0; i < cars.Length; i++) DriveCar(cars[i], pads[i]);
            yield return null;
        }
        stopping = true;
        foreach (Gamepad pad in pads) InputSystem.RemoveDevice(pad);
        // Visual evidence comes after the measured interval, so PNG encoding/disk
        // I/O cannot manufacture a hitch in the race capture. This reads the game
        // framebuffer, never the user's desktop or lock/authentication screen.
        yield return new WaitForEndOfFrame();
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllBytes(output + ".png", screenshot.EncodeToPNG());
        Destroy(screenshot);
        Flush();
        Restore();
        Application.Quit();
    }
    void DriveCar(Rigidbody car, Gamepad pad)
    {
        // Test inputs through the real Input System; no force/tuning/collider modifications.
        Vector3 position = car.position;
        int nearest = 0; float nearestDistance = float.PositiveInfinity;
        Vector3 projected = position;
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 leg = route[(i + 1) % route.Length] - route[i];
            float projection = Mathf.Clamp01(Vector3.Dot(position - route[i], leg) / Mathf.Max(.01f, leg.sqrMagnitude));
            Vector3 point = route[i] + leg * projection;
            float d = (point - position).sqrMagnitude;
            if (d < nearestDistance) { nearest = i; nearestDistance = d; projected = point; }
        }
        int next = (nearest + 1) % route.Length;
        float lookAhead = 13f + Mathf.Clamp(car.linearVelocity.magnitude * .28f, 0, 6);
        Vector3 target = projected;
        for (int step = 0; step < 8; step++)
        {
            float length = Vector3.Distance(target, route[next]);
            if (length >= lookAhead) { target = Vector3.MoveTowards(target, route[next], lookAhead); break; }
            lookAhead -= length; target = route[next]; next = (next + 1) % route.Length;
        }
        Vector3 local = car.transform.InverseTransformPoint(target);
        float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float speed = car.linearVelocity.magnitude * 3.6f;
        float desired = Mathf.Lerp(90f, 43f, Mathf.InverseLerp(5f, 32f, Mathf.Abs(angle)));
        float throttle = Mathf.Clamp01((desired - speed) / 12f);
        float brake = Mathf.Clamp01((speed - desired - 3f) / 20f);
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(Mathf.Clamp(angle / 40f, -1, 1), 0), rightTrigger = throttle, leftTrigger = brake });
    }
    void LateUpdate()
    {
        if (stopping || output == null || count >= samples.Length) return;
        double now = Time.realtimeSinceStartupAsDouble;
        int collectionCount = GC.CollectionCount(0);
        var row = new Sample { seconds = now - began, wall = (now - previous) * 1000, frame = Time.frameCount,
            main = main.Valid ? main.LastValue : -1, render = render.Valid ? render.LastValue : -1,
            gc = gc.Valid ? gc.LastValue : -1, shader = shader.Valid ? shader.LastValue : -1, upload = upload.Valid ? upload.LastValue : -1,
            batches = batches.Valid ? batches.LastValue : -1,
            checkpoint = timer != null ? timer.NextCheckpoint : -1, focus = Application.isFocused,
            ready = raceBegan > 0, collections = collectionCount - previousCollections };
        if (cars != null && cars[0] != null) { row.speed = cars[0].linearVelocity.magnitude * 3.6f; row.x = cars[0].position.x; row.z = cars[0].position.z; }
        for (int i = 0; i < detail.Length; i++)
            detailSamples[count * detail.Length + i] = detail[i].Valid ? detail[i].LastValue : -1;
        samples[count++] = row; previous = now; previousCollections = collectionCount;
    }
    void StartDetailRecorders()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            // Name decodes native UTF8 into a new managed string. Do not repeat
            // that allocation for every requested marker in the inner loop.
            string name = description.Name;
            for (int i = 0; i < detail.Length; i++)
                if (!detail[i].Valid && name == MarkerNames[i])
                    detail[i] = ProfilerRecorder.StartNew(description.Category, name, 1);
        }
    }
    void Flush()
    {
        // Write once outside the measured race, never Debug.Log or disk I/O per frame.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var text = new StringBuilder(count * 100);
        text.Append("seconds,frame,wall_ms,previous_main_ns,previous_render_ns,previous_gc_bytes,previous_shader_ns,previous_upload_ns,gc_collections,checkpoint,speed_kph,x,z,focused,race_ready,previous_batches");
        foreach (string column in MarkerColumns) text.Append(",previous_").Append(column).Append("_ns");
        text.AppendLine();
        for (int i = 0; i < count; i++)
        {
            Sample s = samples[i];
            text.AppendFormat(CultureInfo.InvariantCulture, "{0:F4},{1},{2:F3},{3},{4},{5},{6},{7},{8},{9},{10:F2},{11:F2},{12:F2},{13},{14},{15}",
                s.seconds, s.frame, s.wall, s.main, s.render, s.gc, s.shader, s.upload, s.collections, s.checkpoint, s.speed, s.x, s.z, s.focus, s.ready, s.batches);
            for (int j = 0; j < detail.Length; j++) text.Append(',').Append(detailSamples[i * detail.Length + j]);
            text.AppendLine();
        }
        File.WriteAllText(output, text.ToString());
        File.WriteAllText(output + ".meta.txt", "Circuit=" + circuit + "; players=" + players + "; Unity=" + Application.unityVersion + "; API=" + SystemInfo.graphicsDeviceType + "; GPU=" + SystemInfo.graphicsDeviceName +
            "; resolution=" + Screen.width + "x" + Screen.height + "; frames=" + count + "; duration=" + duration + "; drivenSeconds=" + (Time.realtimeSinceStartupAsDouble - raceBegan).ToString("F2", CultureInfo.InvariantCulture) +
            "; finished=" + (timer != null && timer.IsFinished) + "; checkpointCount=" + (timer != null ? timer.CheckpointCount : 0) +
            "; Main=" + main.Valid + "; Render=" + render.Valid + "; GC=" + gc.Valid + "; Shader=" + shader.Valid + "; Upload=" + upload.Valid + "; Batches=" + batches.Valid +
            "; RenderingArea=" + Profiler.GetAreaEnabled(ProfilerArea.Rendering) + "\nAutomated gamepad inputs, real player physics. Unavailable counters = -1; recorder values refer to previous frame. No universal hitch-free guarantee.");
        var markers = new StringBuilder();
        for (int i = 0; i < detail.Length; i++) markers.AppendLine(MarkerColumns[i] + "=" + MarkerNames[i] + "; valid=" + detail[i].Valid);
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            string name = description.Name;
            if (name.Contains("Terrain") || name.Contains("Present") || name.Contains("Preload") || name.Contains("Canvas") ||
                name.Contains("Collect") || name.Contains("Behaviour") || name.Contains("Physics.") || name.Contains("RenderLoop"))
                markers.AppendLine("Available: " + description.Category + "/" + name);
        }
        File.WriteAllText(output + ".markers.txt", markers.ToString());
    }
    void Restore()
    {
        if (hadPlayers) PlayerPrefs.SetInt("Rally.LocalPlayers", savedPlayers); else PlayerPrefs.DeleteKey("Rally.LocalPlayers");
        if (hadCircuit) PlayerPrefs.SetString("Rally.SelectedCircuit", savedCircuit); else PlayerPrefs.DeleteKey("Rally.SelectedCircuit");
        PlayerPrefs.Save();
    }
    void OnApplicationQuit() { if (!stopping && output != null) { stopping = true; Flush(); Restore(); } }
    void OnDestroy()
    {
        main.Dispose(); render.Dispose(); gc.Dispose(); shader.Dispose(); upload.Dispose(); batches.Dispose();
        foreach (var recorder in detail) recorder.Dispose();
    }
}
