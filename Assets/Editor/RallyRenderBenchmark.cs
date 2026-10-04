using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repeatable rendered fly-through using the actual one/two-player setup.</summary>
[InitializeOnLoad]
public static class RallyRenderBenchmark
{
    const string Prefix = "Rally.RenderBenchmark.";
    const string Request = "Logs/render-benchmark-request.txt";
    static readonly List<double> Frames = new List<double>();
    static Camera[] cameras;
    static Vector3[] route;
    static double began, lastFrame;
    static double setupBegan;
    static long draws, triangles;
    static int renderedFrames;
    static int lastRenderedFrame = -1;

    static RallyRenderBenchmark()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += ModeChanged;
        Camera.onPostRender += Rendered;
        UnityEngine.Rendering.RenderPipelineManager.endContextRendering += EndFrame;
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Prefix + "Running", false)) return;
            SessionState.SetBool(Prefix + "Advance", false);
            EditorApplication.isPlaying = false;
        };
    }

    [MenuItem("Tools/Rally/Performance/Benchmark All Circuits - 1 and 2 Players")]
    public static void Start() => StartRun("optimized");

    static void StartRun(string label)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene and leave Play before benchmarking.");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(Prefix + "PreviousScene", EditorSceneManager.GetActiveScene().path);
        SessionState.SetString(Prefix + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetInt(Prefix + "Players", PlayerPrefs.GetInt("Rally.LocalPlayers", 1));
        SessionState.SetBool(Prefix + "Background", Application.runInBackground);
        SessionState.SetString(Prefix + "Label", label);
        SessionState.SetInt(Prefix + "Case", 0);
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetBool(Prefix + "TransitionPending", false);
        File.WriteAllText(Result, "Rendered Unity Editor benchmark, same route per scene. Includes editor overhead.\n" +
            "Warmup 6s, sample 24s; two views separated by 25% of lap. VSync/target cap disabled for measurement only.\n");
        LoadCase();
    }

    static string Result => "Logs/render-benchmark-" + SessionState.GetString(Prefix + "Label", "optimized") + ".txt";

    static void LoadCase()
    {
        cameras = null;
        route = null;
        int index = SessionState.GetInt(Prefix + "Case", 0);
        SessionState.SetBool(Prefix + "TransitionPending", false);
        string path = "Assets/Scenes/Circuit_0" + (index / 2 + 1) + ".unity";
        PlayerPrefs.SetInt("Rally.LocalPlayers", index % 2 + 1);
        SessionState.SetBool(Prefix + "LoadedCase", false);
        setupBegan = EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene(path);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
        EditorApplication.isPaused = false;
    }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Prefix + "Running", false) &&
            !SessionState.GetBool(Prefix + "Advance", false) && !SessionState.GetBool(Prefix + "TransitionPending", false))
            Restore(); // A script reload can consume the pending EnteredEditMode event.
        if (SessionState.GetBool("Rally.SplitTest.Active", false)) return;
        if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode &&
            SessionState.GetInt("Rally.Performance.Scene", -1) < 0 && !StaticOcclusionCulling.isRunning &&
            !SessionState.GetBool("Rally.SplitTest.Active", false) && !File.Exists("Temp/rally-split-screen-test.request"))
        {
            string label = File.ReadAllText(Request).Trim();
            File.Move(Request, Request + ".consumed-" + DateTime.Now.Ticks);
            try { StartRun(string.IsNullOrEmpty(label) ? "optimized" : label); }
            catch (Exception e) { File.WriteAllText("Logs/render-benchmark-error.txt", e.ToString()); }
        }
        if (!SessionState.GetBool(Prefix + "Running", false) || !EditorApplication.isPlaying) return;
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        string expectedScene = "Circuit_0" + (SessionState.GetInt(Prefix + "Case", 0) / 2 + 1);
        int expectedPlayers = SessionState.GetInt(Prefix + "Case", 0) % 2 + 1;
        if (!SessionState.GetBool(Prefix + "LoadedCase", false) ||
            SceneManager.GetActiveScene().name != expectedScene || PlayerPrefs.GetInt("Rally.LocalPlayers", 1) != expectedPlayers)
        {
            // Frontend initialization may restore MainMenu across domain reloads.
            // Explicitly load the intended circuit once Play has started.
            RallyGameSession.SelectLocalPlayers(expectedPlayers);
            cameras = null;
            route = null;
            SessionState.SetBool(Prefix + "LoadedCase", true);
            // Required when this project disables both scene and domain reload:
            // entering Play alone does not fire sceneLoaded/install two players.
            SceneManager.LoadScene(expectedScene);
            return;
        }
        // Scene/domain reload are disabled in this project. Runtime scene loads
        // can invalidate cached Unity objects without resetting static fields.
        if (cameras != null && cameras.Any(camera => camera == null))
        {
            cameras = null;
            route = null;
        }
        if (cameras == null)
        {
            int expected = SessionState.GetInt(Prefix + "Case", 0) % 2 + 1;
            if (expected == 2 && (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready))
            {
                if (setupBegan == 0) setupBegan = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - setupBegan > 60)
                {
                    File.AppendAllText(Result, expectedScene + ", 2P: NOT MEASURED, split setup did not become Ready.\n");
                    SessionState.SetBool(Prefix + "Advance", true);
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            cameras = Camera.allCameras.Where(c => c.cameraType == CameraType.Game &&
                c.gameObject.scene == SceneManager.GetActiveScene()).OrderBy(c => c.depth).ToArray();
            if (cameras.Length != expected) throw new InvalidOperationException("Incorrect active camera count.");
            // Bind after entering Play: Unity can reset rendering delegates when
            // changing the play-mode domain/render pipeline.
            UnityEngine.Rendering.RenderPipelineManager.endContextRendering -= EndFrame;
            UnityEngine.Rendering.RenderPipelineManager.endContextRendering += EndFrame;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= CameraRendered;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += CameraRendered;
            foreach (var camera in cameras)
            {
                var follow = camera.GetComponent<JrsFollowCamera>();
                if (follow != null) follow.enabled = false;
            }
            Transform points = GameObject.Find("AI_Waypoints").transform;
            route = Enumerable.Range(0, points.childCount).Select(i => points.GetChild(i).position).ToArray();
            // Stop completion/pause menus from interrupting the fly-through.
            foreach (var flow in UnityEngine.Object.FindObjectsByType<RallyRaceFlow>()) flow.enabled = false;
            SessionState.SetInt(Prefix + "VSync", QualitySettings.vSyncCount);
            SessionState.SetInt(Prefix + "Target", Application.targetFrameRate);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Type gameView = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            Frames.Clear(); draws = triangles = renderedFrames = 0; lastRenderedFrame = -1;
            began = EditorApplication.timeSinceStartup;
            lastFrame = 0;
        }
        double elapsed = EditorApplication.timeSinceStartup - began;
        // Rendering may otherwise stop when the Game tab is hidden/unfocused.
        EditorApplication.QueuePlayerLoopUpdate();
        InternalEditorUtility.RepaintAllViews();
        for (int i = 0; i < cameras.Length; i++)
        {
            float cursor = (float)((Math.Max(0, elapsed - 6) / 24 + i * .25) % 1) * route.Length;
            int point = (int)cursor;
            Vector3 centre = Vector3.Lerp(route[point], route[(point + 1) % route.Length], cursor - point);
            Vector3 forward = (route[(point + 2) % route.Length] - route[point]).normalized;
            cameras[i].transform.SetPositionAndRotation(centre - forward * 8f + Vector3.up * 4f,
                Quaternion.LookRotation(forward - Vector3.up * .12f));
        }
        if (elapsed < 30) return;
        Frames.Sort();
        if (Frames.Count == 0)
        {
            File.AppendAllText(Result, "NOT MEASURED: Game view produced no rendered frames; foreground/unlock Unity and retry.\n");
            SessionState.SetBool(Prefix + "Advance", false);
            EditorApplication.isPlaying = false;
            return;
        }
        double mean = Frames.Average();
        double coverage = Frames.Sum() / 1000 / Math.Max(1, elapsed - 6);
        int sample = Mathf.Clamp((int)(Frames.Count * .99), 0, Frames.Count - 1);
        File.AppendAllText(Result, $"{SceneManager.GetActiveScene().name}, {cameras.Length}P: " +
            $"{Screen.width}x{Screen.height}, mean {1000 / mean:F1} FPS ({mean:F2}ms), " +
            $"p99 frame {Frames[sample]:F2}ms, avg draws {draws / Math.Max(1, renderedFrames)}, " +
            $"avg triangles {triangles / Math.Max(1, renderedFrames)}, frames {Frames.Count}; " +
            $"coverage {coverage:P0}; GPU {SystemInfo.graphicsDeviceName}" +
            (coverage < .9 ? " [PARTIAL SAMPLE: not a valid full-route FPS]" : "") + "\n");
        SessionState.SetBool(Prefix + "Advance", true);
        EditorApplication.isPlaying = false;
    }

    static void EndFrame(UnityEngine.Rendering.ScriptableRenderContext context, List<Camera> views)
    {
        if (cameras != null && views.Any(c => Array.IndexOf(cameras, c) >= 0)) Sample();
    }
    static void CameraRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (cameras != null && camera == cameras.LastOrDefault()) Sample();
    }
    static void Rendered(Camera camera) { if (camera == cameras?.LastOrDefault()) Sample(); }
    static void Sample()
    {
        if (cameras == null || !EditorApplication.isPlaying) return;
        if (lastRenderedFrame == Time.frameCount) return;
        lastRenderedFrame = Time.frameCount;
        double now = EditorApplication.timeSinceStartup;
        if (now - began > 6 && lastFrame > 0)
        {
            Frames.Add((now - lastFrame) * 1000);
            draws += UnityStats.drawCalls;
            triangles += UnityStats.triangles;
            renderedFrames++;
        }
        lastFrame = now;
    }

    static void ModeChanged(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Prefix + "Running", false)) return;
        cameras = null; route = null;
        QualitySettings.vSyncCount = SessionState.GetInt(Prefix + "VSync", 0);
        Application.targetFrameRate = SessionState.GetInt(Prefix + "Target", -1);
        if (!SessionState.GetBool(Prefix + "Advance", false)) { Restore(); return; }
        SessionState.SetBool(Prefix + "Advance", false);
        int next = SessionState.GetInt(Prefix + "Case", 0) + 1;
        SessionState.SetInt(Prefix + "Case", next);
        if (next < 6) { SessionState.SetBool(Prefix + "TransitionPending", true); EditorApplication.delayCall += LoadCase; }
        else { File.AppendAllText(Result, "COMPLETE\n"); Restore(); }
    }

    static void Restore()
    {
        cameras = null;
        route = null;
        SessionState.SetBool(Prefix + "Running", false);
        SessionState.SetBool(Prefix + "TransitionPending", false);
        Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
        PlayerPrefs.SetInt("Rally.LocalPlayers", SessionState.GetInt(Prefix + "Players", 1));
        string start = SessionState.GetString(Prefix + "PreviousStart", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(start) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
        string scene = SessionState.GetString(Prefix + "PreviousScene", "");
        if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
    }
}
