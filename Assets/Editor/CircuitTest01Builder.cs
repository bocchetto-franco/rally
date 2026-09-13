using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

// Editor-only construction tool. The saved road remains editable with ProBuilder.
[InitializeOnLoad]
public static class CircuitTest01Builder
{
    const string ScenePath = "Assets/Scenes/Circuit_Test_01.unity";
    const string MaterialPath = "Assets/Scenes/Circuit_Test_01_Grey.mat";
    const float Radius = 40f;
    const float Width = 8f;
    const float Length = 100f;
    static readonly float Angle = Mathf.PI / 4f;
    static float Straight => (Length - Radius * Angle) / 2f;

    static CircuitTest01Builder()
    {
        EditorApplication.delayCall += CreateOnce;
    }

    static void CreateOnce()
    {
        if (File.Exists(ScenePath) || SessionState.GetBool("CircuitTest01.Attempted2", false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateOnce;
            return;
        }
        SessionState.SetBool("CircuitTest01.Attempted2", true);
        Create();
    }

    [MenuItem("Tools/Rally/Create Circuit Test 01")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Circuit_Test_01 already exists; it will not be overwritten.");
        // Persist the user's current edits before switching scenes.
        var active = SceneManager.GetActiveScene();
        bool unfinishedGeneratedScene = string.IsNullOrEmpty(active.path) && active.rootCount == 1
            && active.GetRootGameObjects()[0].name == "Road_ProBuilder_100m_8m_Right45";
        if (!unfinishedGeneratedScene && !EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Could not save the open scenes.");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var distances = new List<float>();
        for (int i = 0; i <= 7; i++) distances.Add(Straight * i / 7f);
        for (int i = 1; i <= 24; i++) distances.Add(Straight + Radius * Angle * i / 24f);
        for (int i = 1; i <= 7; i++) distances.Add(Straight + Radius * Angle + Straight * i / 7f);
        var vertices = new List<Vector3>();
        var faces = new List<Face>();
        for (int i = 0; i < distances.Count - 1; i++)
        {
            Sample(distances[i], out var a, out var ar);
            Sample(distances[i + 1], out var b, out var br);
            var al = a - ar * Width / 2; var rr = a + ar * Width / 2;
            var bl = b - br * Width / 2; var rb = b + br * Width / 2;
            var down = Vector3.down * .2f;
            Quad(vertices, faces, al, bl, rb, rr);
            Quad(vertices, faces, rr + down, rb + down, bl + down, al + down);
            Quad(vertices, faces, al + down, bl + down, bl, al);
            Quad(vertices, faces, rr, rb, rb + down, rr + down);
            if (i == 0) Quad(vertices, faces, al, rr, rr + down, al + down);
            if (i == distances.Count - 2) Quad(vertices, faces, bl + down, rb + down, rb, bl);
        }
        var road = ProBuilderMesh.Create(vertices, faces);
        road.name = "Road_ProBuilder_100m_8m_Right45";
        road.ToMesh();
        road.Refresh();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP Lit shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Circuit_Test_01_Grey" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetColor("_BaseColor", new Color(.45f, .45f, .45f, 1));
        material.SetFloat("_Smoothness", 0);
        road.GetComponent<MeshRenderer>().sharedMaterial = material;
        var collider = road.GetComponent<MeshCollider>();
        if (collider == null) collider = road.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = road.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = false;
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        var bounds = road.GetComponent<MeshRenderer>().bounds;
        camera.transform.position = bounds.center + Vector3.up * 100;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        camera.orthographic = true;
        camera.orthographicSize = 52;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .17f);
        camera.farClipPlane = 250;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.gray;
        Physics.SyncTransforms();
        ValidateRoad(collider);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
        AssetDatabase.SaveAssets();
        // Reload the actual saved scene and verify its persisted mesh/collision.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        road = UnityEngine.Object.FindAnyObjectByType<ProBuilderMesh>();
        Physics.SyncTransforms();
        ValidateRoad(road.GetComponent<MeshCollider>());
        Selection.activeGameObject = road.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(bounds.center, Quaternion.Euler(90, 0, 0), 60, true, true);
        Debug.Log("CIRCUIT_TEST_01_OK: saved and reloaded; ProBuilder road, 100m centerline, 8m width, right 45 degrees, radius 40m; 903 surface raycasts passed.");
    }

    static void Sample(float distance, out Vector3 center, out Vector3 right)
    {
        float arc = Mathf.Clamp(distance - Straight, 0, Radius * Angle);
        float yaw = arc / Radius;
        center = distance <= Straight ? new Vector3(0, 0, distance)
            : new Vector3(Radius * (1 - Mathf.Cos(yaw)), 0, Straight + Radius * Mathf.Sin(yaw));
        if (distance > Straight + Radius * Angle)
            center += new Vector3(Mathf.Sin(Angle), 0, Mathf.Cos(Angle)) * (distance - Straight - Radius * Angle);
        right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
    }

    static void Quad(List<Vector3> vertices, List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int n = vertices.Count;
        vertices.AddRange(new[] { a, b, c, d });
        faces.Add(new Face(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }));
    }

    static void ValidateRoad(MeshCollider collider)
    {
        if (collider == null || collider.sharedMesh == null) throw new InvalidOperationException("Missing saved road collider.");
        for (int i = 0; i <= 300; i++)
        {
            Sample(Mathf.Lerp(.02f, Length - .02f, i / 300f), out var center, out var right);
            foreach (float offset in new[] { -3.95f, 0f, 3.95f })
                if (!collider.Raycast(new Ray(center + right * offset + Vector3.up * 2, Vector3.down), out var hit, 3)
                    || Mathf.Abs(hit.point.y) > .001f || hit.normal.y < .99f)
                    throw new InvalidOperationException("Road continuity/normal check failed at sample " + i);
        }
    }
}
