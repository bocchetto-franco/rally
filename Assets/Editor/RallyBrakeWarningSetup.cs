using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RallyBrakeWarningSetup
{
    const string RootName = "Rally Brake Warnings";
    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Circuit_01.unity",
        "Assets/Scenes/Circuit_02.unity",
        "Assets/Scenes/Circuit_03.unity"
    };

    [Serializable]
    sealed class Layout { public Vector3[] centerline = default; public float[] distances = default; public float[] roadWidths = default; }
    sealed class Route { public readonly List<Vector3> points = new List<Vector3>(); public readonly List<float> distance = new List<float>(); public readonly List<float> width = new List<float>(); public float length; }
    sealed class Curve { public float start, end, angle, radius; public int sign; }

    static RallyBrakeWarningSetup()
    {
        EditorApplication.update += ProcessRequest;
    }

    static void ProcessRequest()
    {
        const string request = "Logs/brake-warning-request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        EditorApplication.update -= ProcessRequest;
        File.Move(request, request + ".consumed-" + DateTime.Now.Ticks);
        try { InstallAll(); }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/brake-warning-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Rally/Install Brake Warnings In All Circuits")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before installing brake warnings.");
        Directory.CreateDirectory("Logs");
        var report = new List<string>();
        Scene active = SceneManager.GetActiveScene();
        string backup = "Logs/SceneBackups/PaceNotes_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        try
        {
            foreach (string path in ScenePaths)
            {
                Scene original = SceneManager.GetSceneByPath(path);
                bool dirty = original.isLoaded && original.isDirty;
                string temporary = "Assets/__PaceNotes_" + Path.GetFileName(path);
                Scene scene = original;
                bool opened = !original.isLoaded || dirty;
                if (dirty)
                {
                    if (!EditorSceneManager.SaveScene(original, backup + "/Unsaved_" + Path.GetFileName(path), true))
                        throw new IOException("Could not back up unsaved scene.");
                    if (File.Exists(temporary)) throw new IOException("A previous pace-note working scene exists: " + temporary);
                    File.Copy(path, temporary);
                    AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport);
                    scene = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
                }
                else if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (!EditorSceneManager.SaveScene(scene, backup + "/" + Path.GetFileName(path), true))
                        throw new IOException("Could not back up " + path);
                    SceneManager.SetActiveScene(scene);
                    InstallScene(scene, path, report);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                if (dirty)
                {
                    File.Copy(temporary, path, true);
                    AssetDatabase.DeleteAsset(temporary);
                    SceneManager.SetActiveScene(original);
                    InstallScene(original, path, new List<string>());
                    EditorSceneManager.MarkSceneDirty(original);
                    report.Add("PASS unrelated unsaved edits preserved in " + original.name);
                }
            }
            report.Add("Backup: " + backup);
            report.Add("COMPLETE: PASS");
        }
        catch (Exception e) { report.Add("FAIL: " + e); throw; }
        finally
        {
            if (active.isLoaded) SceneManager.SetActiveScene(active);
            File.WriteAllLines("Logs/brake-warning-setup.txt", report);
        }
        Debug.Log("PACE_NOTES_COMPLETE: " + string.Join(" | ", report));
    }

    static void InstallScene(Scene scene, string path, List<string> report)
    {
        GameObject previous = FindInScene(scene, RootName);
        var unchanged = scene.GetRootGameObjects().Where(g => g != previous)
            .SelectMany(g => g.GetComponentsInChildren<Component>(true))
            .Where(c => c != null && !(c is Transform)).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));

        GameObject car = FindInScene(scene, "Porsche 911 SC Rally");
        if (car == null) throw new InvalidOperationException("Porsche not found in " + path);
        Rigidbody body = car.GetComponent<Rigidbody>() ?? car.GetComponentInChildren<Rigidbody>(true);
        if (body == null) throw new InvalidOperationException("Porsche Rigidbody not found in " + path);

        string tuningBefore = TuningSignature(car);
        Route route = LoadRoute(scene, path);
        List<Curve> curves = FindWarningCurves(route);
        if (curves.Count == 0) throw new InvalidOperationException("No closed curves detected in " + path);

        GameObject root = new GameObject(RootName);
        RallyBrakeWarningSystem system = root.AddComponent<RallyBrakeWarningSystem>();
        system.Configure(body);
        GameObject zones = new GameObject("Rally Pace Notes");
        zones.transform.SetParent(root.transform, false);

        for (int i = 0; i < curves.Count; i++) CreateTrigger(zones.transform, system, route, curves[i], i + 1);

        if (previous != null) Object.DestroyImmediate(previous);
        if (tuningBefore != TuningSignature(car)) throw new InvalidOperationException("Vehicle tuning changed while adding brake warnings to " + path);
        foreach (var item in unchanged)
            if (item.Key == null || item.Value != EditorJsonUtility.ToJson(item.Key))
                throw new InvalidOperationException("Unrelated scene component changed: " + item.Key);
        report.Add($"PASS {Path.GetFileNameWithoutExtension(path)}: {curves.Count} pace notes; unrelated components and tuning preserved; " +
            string.Join(", ", curves.Select(c => $"{(c.sign > 0 ? "R" : "L")}{Grade(c)}{(IsHairpin(c) ? " hairpin" : "")}@{c.start:F0}m/r{c.radius:F0}")));
    }

    static GameObject FindInScene(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)?.gameObject;

    static void CreateTrigger(Transform parent, RallyBrakeWarningSystem system, Route route, Curve curve, int index)
    {
        int grade = Grade(curve);
        float approach = IsHairpin(curve) ? 95f : grade <= 2 ? 78f : grade <= 4 ? 60f : 42f;
        float endDistance = Wrap(curve.start + 10f, route.length);
        float startDistance = Wrap(curve.start - approach, route.length);
        float span = ForwardDistance(startDistance, endDistance, route.length);
        var direction = curve.sign > 0 ? RallyBrakeWarningTrigger.TurnDirection.Right : RallyBrakeWarningTrigger.TurnDirection.Left;
        Sample(route, curve.start, out Vector3 entry, out _, out _);
        var group = new GameObject($"Note {index:00} - {(curve.sign > 0 ? "Right" : "Left")} {grade}{(IsHairpin(curve) ? " - Hairpin" : "")}");
        group.transform.SetParent(parent, false);
        int pieces = Mathf.CeilToInt(span / 12f);
        for (int i = 0; i < pieces; i++)
        {
            float from = startDistance + span * i / pieces, to = startDistance + span * (i + 1) / pieces;
            Sample(route, from, out Vector3 a, out _, out float widthA);
            Sample(route, to, out Vector3 b, out Vector3 forward, out float widthB);
            Vector3 heading = b - a;
            if (heading.sqrMagnitude < .001f) heading = forward;
            var go = new GameObject($"Approach {i + 1:00}");
            go.transform.SetParent(group.transform, false);
            go.transform.SetPositionAndRotation((a + b) * .5f + Vector3.up * 2.25f, Quaternion.LookRotation(heading, Vector3.up));
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true;
            box.size = new Vector3(Mathf.Max(widthA, widthB) + 6f, 5f, heading.magnitude + 2f);
            var trigger = go.AddComponent<RallyBrakeWarningTrigger>();
            trigger.Configure(system, TargetSpeed(curve));
            trigger.ConfigureNote(direction, grade, IsHairpin(curve), entry);
        }
    }

    static int Grade(Curve curve)
    {
        if (IsHairpin(curve) || curve.radius <= 28f) return 1;
        if (curve.radius <= 38f) return 2;
        if (curve.radius <= 55f) return 3;
        if (curve.radius <= 85f) return 4;
        if (curve.radius <= 140f) return 5;
        return 6;
    }

    static bool IsHairpin(Curve curve) => curve.angle >= 150f && curve.radius <= 40f;

    static float TargetSpeed(Curve curve)
    {
        if (curve.angle >= 150f) return curve.radius <= 24f ? 32f : 38f;
        if (curve.angle >= 105f) return 45f;
        if (curve.angle >= 70f) return curve.radius <= 35f ? 48f : 55f;
        return curve.radius <= 40f ? 58f : 65f;
    }

    static Route LoadRoute(Scene scene, string scenePath)
    {
        if (scenePath.EndsWith("Circuit_02.unity", StringComparison.Ordinal))
            return FromLayout("Assets/Art/Environment/Circuit02/Circuit02Layout.json");
        if (scenePath.EndsWith("Circuit_03.unity", StringComparison.Ordinal))
            return FromLayout("Assets/Art/Forest/Circuit03/Circuit03Layout.json");

        GameObject roadObject = FindInScene(scene, "Rally_Road_Start_to_Finish");
        ProBuilderMesh road = roadObject == null ? null : roadObject.GetComponent<ProBuilderMesh>();
        if (road == null) throw new InvalidOperationException("Circuit_01 road mesh not found.");
        Vector3[] vertices = road.positions.Select(road.transform.TransformPoint).ToArray();
        var route = new Route();
        for (int i = 0; i + 3 < vertices.Length; i += 4)
        {
            Vector3 a = (vertices[i] + vertices[i + 3]) * .5f;
            Vector3 b = (vertices[i + 1] + vertices[i + 2]) * .5f;
            AddPoint(route, a, Vector3.Distance(vertices[i], vertices[i + 3]));
            AddPoint(route, b, Vector3.Distance(vertices[i + 1], vertices[i + 2]));
        }
        CloseRoute(route);
        return route;
    }

    static Route FromLayout(string path)
    {
        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
        if (layout.centerline == null || layout.centerline.Length < 3) throw new InvalidOperationException("Invalid route layout: " + path);
        var route = new Route();
        for (int i = 0; i < layout.centerline.Length; i++)
        {
            route.points.Add(layout.centerline[i]);
            route.distance.Add(layout.distances != null && i < layout.distances.Length ? layout.distances[i] : (i == 0 ? 0f : route.distance[i - 1] + Vector3.Distance(route.points[i - 1], route.points[i])));
            route.width.Add(layout.roadWidths != null && i < layout.roadWidths.Length ? layout.roadWidths[i] : 10f);
        }
        route.length = layout.distances != null && layout.distances.Length > 0 ? layout.distances[layout.distances.Length - 1] : route.distance[route.distance.Count - 1];
        return route;
    }

    static void AddPoint(Route route, Vector3 point, float width)
    {
        if (route.points.Count > 0 && Vector3.Distance(route.points[route.points.Count - 1], point) < .05f) return;
        route.points.Add(point);
        route.distance.Add(route.points.Count == 1 ? 0f : route.distance[route.distance.Count - 1] + Vector3.Distance(route.points[route.points.Count - 2], point));
        route.width.Add(width);
    }

    static void CloseRoute(Route route)
    {
        if (Vector3.Distance(route.points[0], route.points[route.points.Count - 1]) > .05f)
        {
            route.points.Add(route.points[0]);
            route.distance.Add(route.distance[route.distance.Count - 1] + Vector3.Distance(route.points[route.points.Count - 2], route.points[0]));
            route.width.Add(route.width[0]);
        }
        route.length = route.distance[route.distance.Count - 1];
    }

    static List<Curve> FindWarningCurves(Route route)
    {
        var raw = new List<Curve>();
        Curve current = null;
        int currentSign = 0;
        for (int i = 1; i < route.points.Count - 1; i++)
        {
            Vector3 a = Vector3.ProjectOnPlane(route.points[i] - route.points[i - 1], Vector3.up).normalized;
            Vector3 b = Vector3.ProjectOnPlane(route.points[i + 1] - route.points[i], Vector3.up).normalized;
            // Unity +Z to +X is a positive/right turn. Do not invert this with XZ Vector2 angles.
            float signed = Vector3.SignedAngle(a, b, Vector3.up);
            float step = Mathf.Max(.01f, route.distance[i + 1] - route.distance[i]);
            int sign = signed > .025f ? 1 : signed < -.025f ? -1 : 0;
            bool curved = sign != 0 && Mathf.Abs(signed) / step > .08f;
            if (!curved)
            {
                FinishCurve(raw, ref current);
                currentSign = 0;
                continue;
            }
            if (current == null || sign != currentSign)
            {
                FinishCurve(raw, ref current);
                current = new Curve { start = route.distance[i], end = route.distance[i], angle = 0f, sign = sign };
                currentSign = sign;
            }
            current.end = route.distance[i + 1];
            current.angle += Mathf.Abs(signed);
        }
        FinishCurve(raw, ref current);

        foreach (Curve curve in raw)
        {
            float arc = Mathf.Max(1f, curve.end - curve.start);
            curve.radius = arc / Mathf.Max(.01f, curve.angle * Mathf.Deg2Rad);
        }
        List<Curve> selected = raw.Where(c => c.angle >= 18f && c.radius <= 220f).ToList();
        var merged = new List<Curve>();
        foreach (Curve curve in selected)
        {
            Curve last = merged.LastOrDefault();
            // Opposite bends in a chicane must stay separate left/right calls.
            if (last != null && last.sign == curve.sign && curve.start - last.end < 8f)
            {
                float arc = curve.end - last.start;
                last.end = curve.end;
                last.angle += curve.angle;
                last.radius = arc / Mathf.Max(.01f, last.angle * Mathf.Deg2Rad);
            }
            else merged.Add(curve);
        }
        return merged;
    }

    static void FinishCurve(List<Curve> curves, ref Curve curve)
    {
        if (curve != null && curve.angle >= 8f) curves.Add(curve);
        curve = null;
    }

    static void Sample(Route route, float distance, out Vector3 point, out Vector3 forward, out float width)
    {
        distance = Wrap(distance, route.length);
        int index = route.distance.BinarySearch(distance);
        if (index < 0) index = Mathf.Clamp(~index - 1, 0, route.points.Count - 2);
        int next = Mathf.Min(index + 1, route.points.Count - 1);
        float span = Mathf.Max(.001f, route.distance[next] - route.distance[index]);
        float t = Mathf.Clamp01((distance - route.distance[index]) / span);
        point = Vector3.Lerp(route.points[index], route.points[next], t);
        forward = route.points[next] - route.points[index];
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        else forward.Normalize();
        width = Mathf.Lerp(route.width[index], route.width[next], t);
    }

    static float Wrap(float value, float length) => value < 0f ? value + length * Mathf.Ceil(-value / length) : value % length;
    static float ForwardDistance(float from, float to, float length) => to >= from ? to - from : length - from + to;

    static string TuningSignature(GameObject car)
    {
        return string.Join("|", car.GetComponentsInChildren<Component>(true)
            .Where(c => c is Rigidbody || c is WheelCollider || c is JrsVehicleController || c is RallyVehicleDynamics)
            .Select(EditorJsonUtility.ToJson));
    }
}
