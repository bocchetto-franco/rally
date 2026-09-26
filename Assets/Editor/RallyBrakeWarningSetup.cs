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
    sealed class Layout { public Vector3[] centerline; public float[] distances; public float[] roadWidths; }
    sealed class Route { public readonly List<Vector3> points = new List<Vector3>(); public readonly List<float> distance = new List<float>(); public readonly List<float> width = new List<float>(); public float length; }
    sealed class Curve { public float start, end, angle, radius; }

    [MenuItem("Tools/Rally/Install Brake Warnings In All Circuits")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before installing brake warnings.");
        Directory.CreateDirectory("Logs");
        var report = new List<string>();
        foreach (string path in ScenePaths) Install(path, report);
        File.WriteAllLines("Logs/brake-warning-setup.txt", report.Concat(new[] { DateTime.Now.ToString("O") }));
        AssetDatabase.SaveAssets();
        Debug.Log("BRAKE_WARNINGS_COMPLETE: " + string.Join(" | ", report));
    }

    static void Install(string path, List<string> report)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Object.DestroyImmediate(previous);

        GameObject car = GameObject.Find("Porsche 911 SC Rally");
        if (car == null) throw new InvalidOperationException("Porsche not found in " + path);
        Rigidbody body = car.GetComponent<Rigidbody>() ?? car.GetComponentInChildren<Rigidbody>(true);
        if (body == null) throw new InvalidOperationException("Porsche Rigidbody not found in " + path);

        string tuningBefore = TuningSignature(car);
        Route route = LoadRoute(path);
        List<Curve> curves = FindWarningCurves(route);
        if (curves.Count == 0) throw new InvalidOperationException("No closed curves detected in " + path);

        GameObject root = new GameObject(RootName);
        RallyBrakeWarningSystem system = root.AddComponent<RallyBrakeWarningSystem>();
        system.Configure(body);
        GameObject zones = new GameObject("Brake Warning Triggers");
        zones.transform.SetParent(root.transform, false);

        for (int i = 0; i < curves.Count; i++) CreateTrigger(zones.transform, system, route, curves[i], i + 1);

        if (tuningBefore != TuningSignature(car)) throw new InvalidOperationException("Vehicle tuning changed while adding brake warnings to " + path);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
        report.Add($"{Path.GetFileNameWithoutExtension(path)}: {curves.Count} braking zones; " + string.Join(", ", curves.Select(c => $"{c.angle:F0}deg/{TargetSpeed(c):F0}kmh")));
    }

    static void CreateTrigger(Transform parent, RallyBrakeWarningSystem system, Route route, Curve curve, int index)
    {
        float approach = curve.angle >= 150f ? 95f : curve.angle >= 100f ? 75f : curve.angle >= 70f ? 58f : 45f;
        float endDistance = Wrap(curve.start - 7f, route.length);
        float startDistance = Wrap(curve.start - approach, route.length);
        float span = ForwardDistance(startDistance, endDistance, route.length);
        float middleDistance = Wrap(startDistance + span * .5f, route.length);
        Sample(route, middleDistance, out Vector3 position, out Vector3 forward, out float width);

        GameObject go = new GameObject($"Brake Zone {index:00} - {curve.angle:F0} deg");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position + Vector3.up * 2.35f, Quaternion.LookRotation(forward, Vector3.up));
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width + 4f, 5f, Mathf.Max(12f, span));
        RallyBrakeWarningTrigger trigger = go.AddComponent<RallyBrakeWarningTrigger>();
        trigger.Configure(system, TargetSpeed(curve));
    }

    static float TargetSpeed(Curve curve)
    {
        if (curve.angle >= 150f) return curve.radius <= 24f ? 32f : 38f;
        if (curve.angle >= 105f) return 45f;
        if (curve.angle >= 70f) return curve.radius <= 35f ? 48f : 55f;
        return curve.radius <= 40f ? 58f : 65f;
    }

    static Route LoadRoute(string scenePath)
    {
        if (scenePath.EndsWith("Circuit_02.unity", StringComparison.Ordinal))
            return FromLayout("Assets/Art/Environment/Circuit02/Circuit02Layout.json");
        if (scenePath.EndsWith("Circuit_03.unity", StringComparison.Ordinal))
            return FromLayout("Assets/Art/Forest/Circuit03/Circuit03Layout.json");

        GameObject roadObject = GameObject.Find("Rally_Road_Start_to_Finish");
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
            Vector2 a = Flat(route.points[i] - route.points[i - 1]).normalized;
            Vector2 b = Flat(route.points[i + 1] - route.points[i]).normalized;
            float signed = Vector2.SignedAngle(a, b);
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
                current = new Curve { start = route.distance[i], end = route.distance[i], angle = 0f };
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
        List<Curve> selected = raw.Where(c => c.angle >= 68f || (c.angle >= 22f && c.radius <= 45f)).ToList();
        var merged = new List<Curve>();
        foreach (Curve curve in selected)
        {
            Curve last = merged.LastOrDefault();
            if (last != null && curve.start - last.end < 32f)
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

    static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);
    static float Wrap(float value, float length) => value < 0f ? value + length * Mathf.Ceil(-value / length) : value % length;
    static float ForwardDistance(float from, float to, float length) => to >= from ? to - from : length - from + to;

    static string TuningSignature(GameObject car)
    {
        return string.Join("|", car.GetComponentsInChildren<Component>(true)
            .Where(c => c is Rigidbody || c is WheelCollider || c is JrsVehicleController || c is RallyVehicleDynamics)
            .Select(EditorJsonUtility.ToJson));
    }
}
