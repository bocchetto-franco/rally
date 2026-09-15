using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Circuit01CheckpointSetup
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const int GateCount = 7;
    sealed class Section
    {
        public float start, length, yaw, curvature;
        public Vector3 origin;
    }
    static readonly List<Section> Sections = new List<Section>();
    static float total;

    static Circuit01CheckpointSetup()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    static void InstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallOnce;
            return;
        }
        if (SceneManager.GetActiveScene().path != ScenePath || GameObject.Find("Checkpoint System") != null)
            return;
        Install();
    }

    [MenuItem("Tools/Rally/Add Checkpoints and Timer to Circuit 01")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (GameObject.Find(Circuit01LoopSetup.Marker) != null)
        {
            Circuit01LoopSetup.RebuildCheckpoints();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }
        if (GameObject.Find("Checkpoint System") != null)
            throw new InvalidOperationException("Circuit_01 already has a checkpoint system.");

        ProBuilderMesh road = UnityEngine.Object.FindAnyObjectByType<ProBuilderMesh>();
        if (road == null || road.name != "Rally_Road_Start_to_Finish")
            throw new InvalidOperationException("Circuit_01 road was not found.");
        Mesh roadMesh = road.GetComponent<MeshFilter>().sharedMesh;
        int vertexCount = roadMesh.vertexCount;
        int triangleCount = roadMesh.triangles.Length;
        Bounds roadBounds = roadMesh.bounds;

        DefineRoute();
        var root = new GameObject("Checkpoint System");
        var manager = root.AddComponent<RallyCheckpointManager>();
        MeshCollider roadCollider = road.GetComponent<MeshCollider>();
        var gates = new RallyCheckpointTrigger[GateCount];

        for (int i = 0; i < GateCount; i++)
        {
            // Leave a short run-up before the start gate so the vehicle crosses
            // it after Play begins instead of spawning already inside it.
            const float startDistance = 10f;
            float distance = Mathf.Lerp(startDistance, total, i / (GateCount - 1f));
            Sample(distance, out Vector3 position, out Vector3 tangent, out float width);
            var ray = new Ray(position + Vector3.up * 5f, Vector3.down);
            if (!roadCollider.Raycast(ray, out RaycastHit hit, 10f))
                throw new InvalidOperationException("Could not place checkpoint " + i + " on the road.");

            string suffix = i == 0 ? "Start" : i == GateCount - 1 ? "Finish" : $"{distance:000}m";
            var gate = new GameObject($"Checkpoint_{i:00}_{suffix}");
            gate.transform.SetParent(root.transform);
            gate.transform.SetPositionAndRotation(hit.point + Vector3.up * 2f, Quaternion.LookRotation(tangent, Vector3.up));
            var box = gate.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width + 1.5f, 4f, 2f);
            gates[i] = gate.AddComponent<RallyCheckpointTrigger>();
            gates[i].Configure(manager, i);
        }

        manager.Configure(gates);
        if (roadMesh.vertexCount != vertexCount || roadMesh.triangles.Length != triangleCount || roadMesh.bounds != roadBounds)
            throw new InvalidOperationException("Road geometry changed while adding checkpoints.");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Circuit_01 checkpoints.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log($"CIRCUIT_01_CHECKPOINTS_OK: {GateCount} invisible trigger gates, {total / (GateCount - 1):F1}m spacing, start-to-finish timer and UI saved; road mesh unchanged.");
    }

    static void DefineRoute()
    {
        Sections.Clear(); total = 0;
        Vector3 point = Vector3.zero;
        float heading = 0;
        float[] straights = { (100 - 40 * Mathf.PI / 4) / 2, 70, 100, 45, 90, 0, 90, 45, 120 };
        float[] turns = { 45, -45, 110, -110, 35, -35, -100, 100 };
        float[] radii = { 40, 60, 25, 30, 45, 45, 25, 35 };
        for (int i = 0; i < straights.Length; i++)
        {
            if (straights[i] > 0) Add(straights[i], 0, ref point, ref heading);
            if (i < turns.Length)
                Add(Mathf.Abs(turns[i]) * Mathf.Deg2Rad * radii[i], Mathf.Sign(turns[i]) / radii[i], ref point, ref heading);
        }
    }

    static void Add(float length, float curvature, ref Vector3 point, ref float heading)
    {
        var section = new Section { start = total, length = length, yaw = heading, curvature = curvature, origin = point };
        Sections.Add(section);
        Position(section, length, out point, out heading);
        total += length;
    }

    static void Position(Section section, float distance, out Vector3 point, out float yaw)
    {
        yaw = section.yaw + section.curvature * distance;
        if (Mathf.Abs(section.curvature) < .00001f)
            point = section.origin + new Vector3(Mathf.Sin(section.yaw), 0, Mathf.Cos(section.yaw)) * distance;
        else
            point = section.origin + new Vector3((Mathf.Cos(section.yaw) - Mathf.Cos(yaw)) / section.curvature,
                0, (Mathf.Sin(yaw) - Mathf.Sin(section.yaw)) / section.curvature);
    }

    static void Sample(float distance, out Vector3 center, out Vector3 tangent, out float width)
    {
        Section section = Sections.FirstOrDefault(s => distance <= s.start + s.length + .0001f) ?? Sections.Last();
        Position(section, Mathf.Clamp(distance - section.start, 0, section.length), out center, out float yaw);
        center.y = Hill(distance, 120, 280, 10) + Hill(distance, 390, 550, -7) + Hill(distance, 640, 850, 13);
        tangent = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        width = 12;
        foreach (Section bend in Sections.Where(s => s.curvature != 0))
        {
            float gap = Mathf.Max(bend.start - distance, distance - bend.start - bend.length, 0);
            float bendWidth = Mathf.Abs(bend.curvature * bend.length * Mathf.Rad2Deg) >= 90 ? 7 : 9;
            width = Mathf.Min(width, Mathf.Lerp(bendWidth, 12, Mathf.SmoothStep(0, 1, gap / 20)));
        }
    }

    static float Hill(float distance, float start, float end, float height)
    {
        if (distance <= start || distance >= end) return 0;
        return height * .5f * (1 - Mathf.Cos(2 * Mathf.PI * (distance - start) / (end - start)));
    }
}
