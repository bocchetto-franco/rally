using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

// Construction runs in the editor only; the scene contains just a ProBuilder road.
[InitializeOnLoad]
public static class Circuit01Builder
{
    const string Path = "Assets/Scenes/Circuit_01.unity";
    const string RoadName = "Rally_Road_Start_to_Finish";
    sealed class Section
    {
        public float start, length, yaw, curvature, width;
        public Vector3 origin;
    }
    static readonly List<Section> Sections = new List<Section>();
    static float total;

    static Circuit01Builder() { EditorApplication.delayCall += BuildOnce; }
    static void BuildOnce()
    {
        if (File.Exists(Path) || SessionState.GetBool("Circuit01.Built", false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating || EditorApplication.isCompiling)
        { EditorApplication.delayCall += BuildOnce; return; }
        SessionState.SetBool("Circuit01.Built", true);
        Build();
    }

    static void DefineRoute()
    {
        Sections.Clear(); total = 0;
        var point = Vector3.zero;
        float heading = 0;
        // Preserve the first test's initial straight and 45-degree right bend.
        float[] straights = { (100 - 40 * Mathf.PI / 4) / 2, 70, 100, 45, 90, 0, 90, 45, 120 };
        float[] turns = { 45, -45, 110, -110, 35, -35, -100, 100 };
        float[] radii = { 40, 60, 25, 30, 45, 45, 25, 35 };
        for (int i = 0; i < straights.Length; i++)
        {
            if (straights[i] > 0) AddSection(straights[i], 0, 12, ref point, ref heading);
            if (i < turns.Length)
                AddSection(Mathf.Abs(turns[i]) * Mathf.Deg2Rad * radii[i], Mathf.Sign(turns[i]) / radii[i],
                    Mathf.Abs(turns[i]) >= 90 ? 7 : 9, ref point, ref heading);
        }
    }
    static void AddSection(float length, float curvature, float width, ref Vector3 point, ref float heading)
    {
        var s = new Section { start = total, length = length, yaw = heading, curvature = curvature, width = width, origin = point };
        Sections.Add(s);
        Position(s, length, out point, out heading);
        total += length;
    }
    static void Position(Section s, float d, out Vector3 p, out float yaw)
    {
        yaw = s.yaw + s.curvature * d;
        if (Mathf.Abs(s.curvature) < .00001f)
            p = s.origin + new Vector3(Mathf.Sin(s.yaw), 0, Mathf.Cos(s.yaw)) * d;
        else p = s.origin + new Vector3((Mathf.Cos(s.yaw) - Mathf.Cos(yaw)) / s.curvature,
            0, (Mathf.Sin(yaw) - Mathf.Sin(s.yaw)) / s.curvature);
    }
    static float Hill(float d, float start, float end, float height)
    {
        if (d <= start || d >= end) return 0;
        return height * .5f * (1 - Mathf.Cos(2 * Mathf.PI * (d - start) / (end - start)));
    }
    static void Sample(float d, out Vector3 center, out Vector3 right, out float width)
    {
        var section = Sections.FirstOrDefault(s => d <= s.start + s.length + .0001f) ?? Sections.Last();
        Position(section, Mathf.Clamp(d - section.start, 0, section.length), out center, out float yaw);
        center.y = Hill(d, 120, 280, 10) + Hill(d, 390, 550, -7) + Hill(d, 640, 850, 13);
        right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        width = 12;
        foreach (var bend in Sections.Where(s => s.curvature != 0))
        {
            float gap = Mathf.Max(bend.start - d, d - bend.start - bend.length, 0);
            width = Mathf.Min(width, Mathf.Lerp(bend.width, 12, Mathf.SmoothStep(0, 1, gap / 20)));
        }
    }

    [MenuItem("Tools/Rally/Create Circuit 01")]
    public static void Build()
    {
        if (File.Exists(Path)) throw new InvalidOperationException("Circuit_01 already exists; refusing to overwrite it.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        DefineRoute();
        if (!EditorSceneManager.SaveOpenScenes()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var distances = new SortedSet<float> { 0, total };
        foreach (var s in Sections)
        {
            int n = Mathf.CeilToInt(s.length / 2);
            for (int i = 0; i <= n; i++) distances.Add(s.start + s.length * i / n);
        }
        var samples = distances.ToArray();
        var vertices = new List<Vector3>();
        var faces = new List<Face>();
        float actualLength = 0;
        for (int i = 0; i < samples.Length - 1; i++)
        {
            Sample(samples[i], out var a, out var ar, out float aw);
            Sample(samples[i + 1], out var b, out var br, out float bw);
            if ((b - a).sqrMagnitude < .000001f) continue;
            actualLength += Vector3.Distance(a, b);
            var al = a - ar * aw / 2; var rr = a + ar * aw / 2;
            var bl = b - br * bw / 2; var rb = b + br * bw / 2;
            var down = Vector3.down * .3f;
            Quad(vertices, faces, al, bl, rb, rr);
            Quad(vertices, faces, rr + down, rb + down, bl + down, al + down);
            Quad(vertices, faces, al + down, bl + down, bl, al);
            Quad(vertices, faces, rr, rb, rb + down, rr + down);
            if (i == 0) Quad(vertices, faces, al, rr, rr + down, al + down);
            if (i == samples.Length - 2) Quad(vertices, faces, bl + down, rb + down, rb, bl);
        }
        var road = ProBuilderMesh.Create(vertices, faces);
        road.name = RoadName;
        road.ToMesh(); road.Refresh();
        road.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Circuit_Test_01_Grey.mat");
        var collider = road.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = road.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = false;
        Physics.SyncTransforms();
        Validate(collider);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, Path)) throw new InvalidOperationException("Could not save Circuit_01.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Path, OpenSceneMode.Single);
        road = UnityEngine.Object.FindAnyObjectByType<ProBuilderMesh>();
        Physics.SyncTransforms();
        Validate(road.GetComponent<MeshCollider>());
        if (scene.IsValid() && UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount != 1)
            throw new InvalidOperationException("Unexpected objects in geometry-only scene.");
        Selection.activeGameObject = road.gameObject;
        var bounds = road.GetComponent<MeshRenderer>().bounds;
        SceneView.lastActiveSceneView?.LookAt(bounds.center, Quaternion.Euler(65, 0, 0), bounds.extents.magnitude * .9f, true, true);
        Debug.Log($"CIRCUIT_01_OK: saved and reloaded, {actualLength:F1}m, 8 bends (4 >=90 degrees), connected S chicane, width 7-12m, 3 elevation sections (-7m to +13m), geometry only; surface validation passed.");
    }
    static void Quad(List<Vector3> vertices, List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int n = vertices.Count;
        vertices.AddRange(new[] { a, b, c, d });
        faces.Add(new Face(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }));
    }
    static void Validate(MeshCollider collider)
    {
        if (collider == null || collider.sharedMesh == null) throw new InvalidOperationException("Missing road collision mesh.");
        for (float d = .1f; d < total - .1f; d += 1.5f)
        {
            Sample(d, out var p, out var right, out float width);
            foreach (float offset in new[] { -.45f * width, 0, .45f * width })
            {
                var origin = p + right * offset + Vector3.up * 3;
                if (!collider.Raycast(new Ray(origin, Vector3.down), out var hit, 5)
                    || Mathf.Abs(hit.point.y - p.y) > .12f || hit.normal.y < .94f)
                    throw new InvalidOperationException("Surface gap/slope failure at " + d);
            }
        }
    }
}
