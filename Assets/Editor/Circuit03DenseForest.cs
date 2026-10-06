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

/// <summary>Additive forest dressing; never regenerates terrain or the track.</summary>
[InitializeOnLoad]
public static class Circuit03DenseForest
{
    const string ScenePath = "Assets/Scenes/Circuit_03.unity";
    const string Request = "Logs/circuit03-dense-forest.request";
    const string Report = "Logs/circuit03-dense-forest.txt";
    const string Group = "Circuit 03 - Dense Pine Groves";
    const string BakeKey = "Rally.DenseForest.Baking";
    [Serializable] sealed class Layout
    {
        public Vector3[] centerline = Array.Empty<Vector3>();
        public float[] roadWidths = Array.Empty<float>();
        public Vector3[] crowdCenters = Array.Empty<Vector3>();
    }

    static Circuit03DenseForest() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (SessionState.GetBool(BakeKey, false))
            {
                if (StaticOcclusionCulling.isRunning) return;
                SessionState.SetBool(BakeKey, false);
                string data = "Assets/Scenes/Circuit_03/OcclusionCullingData.asset";
                long ticks = long.Parse(SessionState.GetString(BakeKey + "Ticks", "0"));
                if (!File.Exists(data) || File.GetLastWriteTimeUtc(data).Ticks < ticks)
                    throw new InvalidOperationException("Occlusion bake did not produce fresh data.");
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                File.AppendAllText(Report, "BAKE OK: Circuit_03\nCOMPLETE: PASS\n");
                return;
            }
            if (!File.Exists(Request) || StaticOcclusionCulling.isRunning ||
                SessionState.GetBool("Rally.RenderBenchmark.Running", false) ||
                SessionState.GetBool("Rally.SplitTest.Active", false)) return;
            File.Move(Request, Request + ".consumed-" + DateTime.UtcNow.Ticks);
            Build();
        }
        catch (Exception e) { SessionState.SetBool(BakeKey, false); File.AppendAllText(Report, "FAILED: " + e + "\n"); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Circuit 03/Add Dense Pine Groves")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before adding the forest.");
        string backup = "Logs/SceneBackups/DenseForest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/Circuit_03.unity");
        File.Copy("Assets/Scenes/Circuit_03/OcclusionCullingData.asset", backup + "/OcclusionCullingData.asset");
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        if (GameObject.Find(Group) != null) throw new InvalidOperationException("Dense forest already exists; refusing to duplicate it.");
        string gameplay = Signature(scene);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Art/Forest/Circuit03/Circuit03Layout.json"));
        if (layout.centerline.Length < 3 || layout.roadWidths.Length != layout.centerline.Length)
            throw new InvalidOperationException("Invalid circuit layout.");
        var terrain = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>()).Single();
        var forest = GameObject.Find("Circuit 03 - Forest").transform;
        var sources = forest.Cast<Transform>().Where(t => t.name.StartsWith("Pine", StringComparison.Ordinal)).ToArray();
        if (sources.Length < 3 || sources.Any(t => t.GetComponent<LODGroup>() == null))
            throw new InvalidOperationException("Expected existing optimized pine instances with LODs.");
        var occupied = sources.Select(t => t.position).ToList();
        var parent = new GameObject(Group);
        parent.transform.SetParent(forest.parent, false);
        var random = new System.Random(31042026);
        int added = 0;
        float minimum = float.MaxValue;
        File.WriteAllText(Report, "Dense forest, Circuit_03. Backup: " + backup + "\nOriginal pines: " + sources.Length + "\n");
        try
        {
            for (int attempt = 0; attempt < 120000 && added < 1140; attempt++)
            {
                int index = random.Next(layout.centerline.Length);
                Vector3 along = layout.centerline[(index + 1) % layout.centerline.Length] - layout.centerline[index];
                Vector3 right = Vector3.Cross(Vector3.up, along).normalized;
                float side = random.Next(2) == 0 ? -1 : 1;
                // More trees close to the visible roadside; irregular groves continue uphill.
                float offset = layout.roadWidths[index] * .5f + 17 + (float)Math.Pow(random.NextDouble(), 1.5) * 68;
                Vector3 p = layout.centerline[index] + right * side * offset + along.normalized * ((float)random.NextDouble() * 8 - 4);
                Vector3 local = p - terrain.transform.position;
                TerrainData td = terrain.terrainData;
                float x = local.x / td.size.x, z = local.z / td.size.z;
                if (x < .01f || z < .01f || x > .99f || z > .99f || td.GetSteepness(x, z) > 32) continue;
                if (EdgeClearance(layout, p) < 14 || occupied.Any(q => FlatDistance(p, q) < 4.2f)) continue;
                if (layout.crowdCenters.Any(q => FlatDistance(p, q) < 21)) continue;
                Transform source = sources[random.Next(sources.Length)];
                GameObject tree = Object.Instantiate(source.gameObject, parent.transform);
                tree.name = "Dense_Pine_" + (added + 1).ToString("0000");
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                Vector3 normal = Vector3.Slerp(Vector3.up, td.GetInterpolatedNormal(x, z), .2f);
                tree.transform.SetPositionAndRotation(p, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
                tree.transform.localScale = source.localScale * (.85f + (float)random.NextDouble() * .3f);
                Renderer[] renderers = tree.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                float clearance = EdgeClearance(layout, bounds.center) - radius;
                if (clearance < 10 || layout.crowdCenters.Any(q => FlatDistance(bounds.center, q) < 18 + radius))
                { Object.DestroyImmediate(tree); continue; }
                tree.transform.position += Vector3.up * (p.y - bounds.min.y - .03f);
                if (tree.GetComponentsInChildren<Collider>().Length != 0 || tree.GetComponentsInChildren<MonoBehaviour>().Length != 0)
                    throw new InvalidOperationException("Tree clone unexpectedly contains physics or scripts.");
                foreach (Renderer r in renderers)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic);
                }
                tree.GetComponent<LODGroup>().RecalculateBounds();
                occupied.Add(p); minimum = Mathf.Min(minimum, clearance); added++;
            }
            if (added != 1140) throw new InvalidOperationException("Only " + added + " safe positions found.");
            if (gameplay != Signature(scene)) throw new InvalidOperationException("Gameplay signature changed.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            File.AppendAllText(Report, $"Added {added}; total {sources.Length + added}. Minimum full-canopy road-edge clearance {minimum:F2}m. Crowd clearance >=18m. Shared original meshes/materials and optimized LODs, no added scripts/colliders. Existing gameplay/transforms unchanged.\n");
            Capture(layout, 80, "Logs/Circuit03_DenseForest_Road.png");
            Capture(layout, layout.centerline.Length / 2, "Logs/Circuit03_DenseForest_Hills.png");
            SessionState.SetString(BakeKey + "Ticks", DateTime.UtcNow.Ticks.ToString());
            if (!StaticOcclusionCulling.Compute()) throw new InvalidOperationException("Occlusion bake failed to start.");
            SessionState.SetBool(BakeKey, true);
            File.AppendAllText(Report, "BAKING Circuit_03\n");
        }
        catch { if (added != 1140) Object.DestroyImmediate(parent); throw; }
    }

    static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    static float EdgeClearance(Layout layout, Vector3 p)
    {
        float minimum = float.MaxValue;
        for (int i = 0; i < layout.centerline.Length; i++)
        {
            int j = (i + 1) % layout.centerline.Length;
            Vector2 a = new Vector2(layout.centerline[i].x, layout.centerline[i].z);
            Vector2 b = new Vector2(layout.centerline[j].x, layout.centerline[j].z);
            Vector2 q = new Vector2(p.x, p.z), d = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, d) / Mathf.Max(.0001f, d.sqrMagnitude));
            minimum = Mathf.Min(minimum, Vector2.Distance(q, a + d * t) - Mathf.Lerp(layout.roadWidths[i], layout.roadWidths[j], t) * .5f);
        }
        return minimum;
    }
    static string Signature(Scene scene) => string.Join("\n", scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
        .Where(c => c is Rigidbody || c is Collider || c is JrsVehicleController || c is RallyVehicleDynamics || c is RallyCheckpointManager || c is RallyCheckpointTrigger || c is RallyBotController)
        .Select(c => c.GetEntityId() + ":" + EditorJsonUtility.ToJson(c) + ":" + EditorJsonUtility.ToJson(c.transform)));

    static void Capture(Layout layout, int index, string path)
    {
        var go = new GameObject("Dense Forest Preview");
        var camera = go.AddComponent<Camera>();
        Vector3 forward = (layout.centerline[index + 3] - layout.centerline[index]).normalized;
        go.transform.SetPositionAndRotation(layout.centerline[index] + Vector3.up * 2.8f, Quaternion.LookRotation(forward));
        camera.farClipPlane = 450; camera.fieldOfView = 70;
        var target = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(go); }
    }
}
