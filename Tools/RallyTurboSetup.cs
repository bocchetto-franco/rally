using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RallyTurboSetup
{
    const string Folder = "Assets/Art/RallyTurbo";
    const string RootName = "Rally Turbo Zones";
    class Route
    {
        public Vector3[] p;
        public float[] d;
        public float Length => d[d.Length - 1];
        public Vector3 At(float distance)
        {
            distance = Mathf.Repeat(distance, Length);
            for (int i = 0; i < p.Length; i++)
                if (distance <= d[i + 1]) return Vector3.Lerp(p[i], p[(i + 1) % p.Length], Mathf.InverseLerp(d[i], d[i + 1], distance));
            return p[0];
        }
    }
    struct Placement { public float station, offset; public Vector3 point, forward, normal; }
    static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    static Collider[] Roads(Scene scene) => Components<Collider>(scene).Where(c => c.enabled && !c.isTrigger &&
        (c.name == "Rally_Road_Start_to_Finish" || c.name == "Circuit02_Road" || c.name == "Circuit03_Road" || c.name == "Continuous smooth deck collider")).ToArray();
    static bool Ground(Collider[] road, Vector3 point, out RaycastHit best)
    {
        best = default; bool found = false;
        foreach (var c in road)
            if (c.Raycast(new Ray(point + Vector3.up * 30, Vector3.down), out var hit, 65) && (!found || hit.point.y > best.point.y))
            { best = hit; found = true; }
        return found;
    }
    static Route ReadRoute(Scene scene)
    {
        var root = scene.GetRootGameObjects().Single(g => g.name == "AI_Waypoints").transform;
        var r = new Route { p = Enumerable.Range(0, root.childCount).Select(i => root.GetChild(i).position).ToArray(), d = new float[root.childCount + 1] };
        for (int i = 0; i < r.p.Length; i++) r.d[i + 1] = r.d[i] + Vector3.Distance(r.p[i], r.p[(i + 1) % r.p.Length]);
        return r;
    }
    static List<Placement> Choose(Scene scene)
    {
        var route = ReadRoute(scene); var road = Roads(scene);
        if (road.Length == 0) throw new Exception("No authored road: " + scene.name);
        var water = Components<RallyPuddleSlowZone>(scene).Select(z => z.GetComponent<Collider>()).Where(c => c != null).ToArray();
        var chosen = new List<Placement>();
        foreach (float fraction in new[] { .18f, .48f, .77f })
        {
            float bestScore = float.PositiveInfinity; Placement best = default;
            for (float s = 110; s < route.Length - 100; s += 4)
            {
                Vector3 centre = route.At(s), forward = (route.At(s + 7) - route.At(s - 7)).normalized;
                float angle = Vector3.Angle(centre - route.At(s - 12), route.At(s + 32) - centre);
                if (angle > 16 || chosen.Any(c => Mathf.Abs(c.station - s) < 210 || Vector3.Distance(c.point, centre) < 85)) continue;
                float offset = chosen.Count == 1 ? -1.2f : 1.2f;
                Vector3 point = centre + Vector3.Cross(Vector3.up, forward).normalized * offset;
                if (water.Any(c => { var bounds = c.bounds; bounds.Expand(new Vector3(10, 10, 18)); return bounds.Contains(point); })) continue;
                if (!Ground(road, point, out var hit)) continue;
                if (Mathf.Abs(hit.point.y - centre.y) > 3 || hit.normal.y < .85f) continue;
                Vector3 right = Vector3.Cross(hit.normal, forward).normalized;
                if (!Ground(road, point + right * 1.15f, out _) || !Ground(road, point - right * 1.15f, out _)) continue;
                float score = Mathf.Abs(s - route.Length * fraction) + angle * 2;
                if (score >= bestScore) continue;
                bestScore = score;
                best = new Placement { station = s, offset = offset, point = hit.point, normal = hit.normal, forward = Vector3.ProjectOnPlane(forward, hit.normal).normalized };
            }
            if (float.IsPositiveInfinity(bestScore)) throw new Exception("Cannot find three safe straight zones in " + scene.name);
            chosen.Add(best);
        }
        return chosen;
    }
    public static string Plan()
    {
        string text = "Turbo placement plan from current authored waypoints/road colliders\n";
        foreach (string name in RallyGameSession.CircuitScenes)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try { foreach (var p in Choose(scene)) text += name + ": station=" + p.station + "m offset=" + p.offset + "m position=" + p.point.ToString("F2") + "\n"; }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/turbo-plan.txt", text); return text;
    }
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning) throw new Exception("Editor must be idle.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new Exception("Open scene has unsaved changes; no edits discarded.");
        Plan(); // Validate all placements before adding any scene objects.
        string backup = "Logs/SceneBackups/Turbo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        Material dark = Material("Turbo charcoal", new Color(.045f, .055f, .075f));
        Material yellow = Material("Turbo yellow", new Color(1f, .78f, .03f));
        File.WriteAllText("Logs/turbo-build.txt", "Backup: " + backup + "\n");
        try
        {
            foreach (string name in RallyGameSession.CircuitScenes)
            {
                string path = "Assets/Scenes/" + name + ".unity";
                File.Copy(path, backup + "/" + name + ".unity");
                Scene scene = EditorSceneManager.OpenScene(path);
                if (scene.GetRootGameObjects().Any(g => g.name == RootName)) throw new Exception("Turbo zones already exist in " + name + "; refusing duplicates.");
                var originals = Components<Component>(scene).Where(c => c != null).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
                var road = Roads(scene);
                var parent = new GameObject(RootName);
                int index = 0;
                foreach (var p in Choose(scene))
                {
                    var go = new GameObject("Turbo_" + (++index).ToString("00") + " - centre lane 1.4m");
                    go.transform.SetParent(parent.transform, false);
                    go.transform.SetPositionAndRotation(p.point, Quaternion.LookRotation(p.forward, p.normal));
                    var box = go.AddComponent<BoxCollider>(); box.isTrigger = true;
                    box.size = new Vector3(2.2f, 3f, 8f); box.center = new Vector3(0, 1.5f, 0);
                    go.AddComponent<RallyTurboZone>();
                    Mesh mesh = Marker(go.transform, road);
                    string meshPath = Folder + "/" + name + "_Turbo_" + index.ToString("00") + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) throw new Exception("Marker asset already exists: " + meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    var visual = new GameObject("Turbo arrows - visual only", typeof(MeshFilter), typeof(MeshRenderer));
                    visual.transform.SetParent(go.transform, false);
                    visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { dark, yellow };
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    File.AppendAllText("Logs/turbo-build.txt", name + "/" + go.name + ": station=" + p.station + ", offset=" + p.offset + ", position=" + p.point.ToString("F3") + "\n");
                }
                foreach (var pair in originals) if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new Exception("Original component changed: " + name);
                if (!Components<JrsVehicleController>(scene).Any(c => c.name == RallyGameSession.VehicleName && c.gameObject.activeInHierarchy)) throw new Exception("Player must remain active.");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets(); File.AppendAllText("Logs/turbo-build.txt", "COMPLETE: PASS - three zones per scene, all original components preserved\n");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        return "Turbo zones saved; backup=" + backup;
    }
    static Material Material(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(m, path); return m;
    }
    static Mesh Marker(Transform zone, Collider[] road)
    {
        var vertices = new List<Vector3>(); var baseTriangles = new List<int>(); var arrows = new List<int>();
        Action<Vector2[], List<int>, float> polygon = (points, triangles, lift) =>
        {
            int first = vertices.Count;
            foreach (var p in points)
            {
                Vector3 world = zone.TransformPoint(new Vector3(p.x, 0, p.y));
                if (!Ground(road, world, out var hit)) throw new Exception("Turbo marker is not fully supported by the road.");
                vertices.Add(zone.InverseTransformPoint(hit.point + hit.normal * lift));
            }
            for (int i = 1; i < points.Length - 1; i++) triangles.AddRange(new[] { first, first + i + 1, first + i });
        };
        // Small cells follow the road's slope instead of leaving a floating rectangle.
        for (int z = -4; z < 4; z++) polygon(new[] { new Vector2(-1.1f, z), new Vector2(1.1f, z), new Vector2(1.1f, z + 1), new Vector2(-1.1f, z + 1) }, baseTriangles, .022f);
        for (int z = -3; z <= 3; z += 2)
        {
            polygon(new[] { new Vector2(-.64f, z - .65f), new Vector2(0, z - .05f), new Vector2(0, z + .55f), new Vector2(-.64f, z - .05f) }, arrows, .03f);
            polygon(new[] { new Vector2(0, z - .05f), new Vector2(.64f, z - .65f), new Vector2(.64f, z - .05f), new Vector2(0, z + .55f) }, arrows, .03f);
        }
        var mesh = new Mesh { name = zone.gameObject.scene.name + " " + zone.name + " road arrows" };
        mesh.SetVertices(vertices); mesh.subMeshCount = 2; mesh.SetTriangles(baseTriangles, 0); mesh.SetTriangles(arrows, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
