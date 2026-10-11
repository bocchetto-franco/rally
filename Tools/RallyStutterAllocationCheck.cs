using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using TMPro;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated method allocation test, not an FPS/driver benchmark. No scene assets are saved.
public static class RallyStutterAllocationCheck
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(string label)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning || BuildPipeline.isBuildingPlayer)
            throw new Exception("Unity must be idle.");
        string path = "Logs/stutter-allocations-" + label + ".txt";
        File.WriteAllText(path, "Isolated allocation check (Unity Editor, warmed methods, 2000 calls)\n");
        Scene scene = EditorSceneManager.NewPreviewScene();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        bool wasProfiling = UnityEngine.Profiling.Profiler.enabled;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            UnityEngine.Profiling.Profiler.enabled = true;
            byte[] retained = null;
            long control = Measure(path, "Positive control: byte[128]", () => retained = new byte[128]);
            if (control < 2000 || retained == null) throw new Exception("Allocation counter failed its positive control.");
            GameObject root = new GameObject("Allocation check");
            SceneManager.MoveGameObjectToScene(root, scene);
            Rigidbody body = root.AddComponent<Rigidbody>();
            RallyCheckpointManager manager = root.AddComponent<RallyCheckpointManager>();
            manager.BindVehicle(body);
            RallyRaceHud hud = root.AddComponent<RallyRaceHud>();
            TMP_Text speed = Text(root, "Speed"), time = Text(root, "Time"), checkpoint = Text(root, "Checkpoint"), position = Text(root, "Position");
            hud.Configure(manager, body, speed, time, checkpoint);
            hud.ConfigurePositions(null, position);
            Action refresh = (Action)typeof(RallyRaceHud).GetMethod("Refresh", Fields).CreateDelegate(typeof(Action), hud);
            Action<float> elapsed = Setter<float>(manager, "elapsedTime");
            int frame = 0;
            Action moving = () => {
                elapsed(10f + (++frame % 120) / 60f);
                body.linearVelocity = Vector3.forward * (frame % 100);
                refresh();
            };
            Measure(path, "HUD moving speed + timer", moving);
            elapsed(65.123f);
            refresh();
            Measure(path, "HUD unchanged displayed values", refresh);
            if (time.text != "TIEMPO  01:05.123") throw new Exception("Timer formatting failed: " + time.text);
            elapsed(59.9998f);
            refresh();
            File.AppendAllText(path, "Minute-boundary display: " + time.text + "\n");
            elapsed(65.123f);
            Setter<bool>(manager, "finished")(true);
            refresh();
            if (time.text != "META  01:05.123") throw new Exception("Final timer failed: " + time.text);
            // Getter must also remain reusable for results/debug consumers.
            string lastTime = null;
            Measure(path, "Unchanged FormattedTime getter", () => lastTime = manager.FormattedTime);
            GameObject zone = new GameObject("Note", typeof(BoxCollider), typeof(RallyBrakeWarningTrigger));
            zone.transform.SetParent(root.transform);
            RallyBrakeWarningTrigger note = zone.GetComponent<RallyBrakeWarningTrigger>();
            note.ConfigureNote(RallyBrakeWarningTrigger.TurnDirection.Right, 2, true, Vector3.zero);
            string lastNote = null;
            Measure(path, "Unchanged pace-note label getter", () => lastNote = note.NoteLabel);
            note.ConfigureNote(RallyBrakeWarningTrigger.TurnDirection.Left, 4, false, Vector3.zero);
            if (note.NoteLabel != "IZQUIERDA 4") throw new Exception("Pace-note cache invalidation failed.");
            if (lastTime != "01:05.123" || lastNote != "DERECHA 2") throw new Exception("Unexpected cached labels.");
            File.AppendAllText(path, "PASS: HUD values, final time and note updates. Editor TMP retains one inspector-string allocation per text change.\n");
        }
        finally { UnityEngine.Profiling.Profiler.enabled = wasProfiling; CultureInfo.CurrentCulture = originalCulture; EditorSceneManager.ClosePreviewScene(scene); }
    }

    static TMP_Text Text(GameObject root, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root.transform);
        return go.GetComponent<TextMeshProUGUI>();
    }

    static Action<T> Setter<T>(object target, string name)
    {
        var value = Expression.Parameter(typeof(T));
        var field = Expression.Field(Expression.Constant(target), target.GetType().GetField(name, Fields));
        return Expression.Lambda<Action<T>>(Expression.Assign(field, value), value).Compile();
    }

    static long Measure(string path, string name, Action action)
    {
        for (int i = 0; i < 512; i++) action();
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 32000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        if (!recorder.Valid) throw new Exception("GC.Alloc recorder unavailable.");
        for (int i = 0; i < 2000; i++) action();
        recorder.Stop();
        if (recorder.Count >= recorder.Capacity) throw new Exception("Allocation recorder overflow.");
        // GC.Alloc is an event marker, not the per-frame byte counter. Count
        // allocation events; do not mislabel its timing Value as byte sizes.
        int allocations = recorder.Count;
        File.AppendAllText(path, name + ": " + allocations + " GC.Alloc events total, " + (allocations / 2000.0).ToString("F2") + " allocations/call\n");
        return allocations;
    }
}
