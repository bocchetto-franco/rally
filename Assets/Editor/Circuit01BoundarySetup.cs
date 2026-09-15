using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Circuit01BoundarySetup
{
    private const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    private const string RootName = "Soft Track Boundaries";
    private const string MaterialPath = "Assets/Scenes/Circuit_01_SoftBoundary.physicMaterial";
    private const float EdgeMargin = 6.5f;
    private const float TargetSegmentLength = 5f;
    private const float WallHeight = 2.5f;
    private const float WallThickness = 1f;
    private const float SegmentOverlap = 0.8f;

    private sealed class Section
    {
        public float start;
        public float length;
        public float yaw;
        public float curvature;
        public Vector3 origin;
    }

    private static readonly List<Section> Sections = new List<Section>();
    private static float totalLength;

    static Circuit01BoundarySetup()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    private static void InstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallOnce;
            return;
        }

        if (SceneManager.GetActiveScene().path == ScenePath && GameObject.Find(RootName) == null)
            Install();
    }

    [MenuItem("Tools/Rally/Add Soft Track Boundaries to Circuit 01")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (GameObject.Find(Circuit01LoopSetup.Marker) != null)
        {
            Circuit01LoopSetup.RebuildBoundaries();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        DefineRoute();
        PhysicsMaterial material = GetOrCreateMaterial();
        GameObject root = new GameObject(RootName);
        int segmentCount = Mathf.CeilToInt(totalLength / TargetSegmentLength);

        for (int index = 0; index < segmentCount; index++)
        {
            float startDistance = totalLength * index / segmentCount;
            float endDistance = totalLength * (index + 1) / segmentCount;
            Sample(startDistance, out Vector3 startCenter, out Vector3 startRight, out float startWidth);
            Sample(endDistance, out Vector3 endCenter, out Vector3 endRight, out float endWidth);

            CreateBoundarySegment(root.transform, material, "Left", index,
                startCenter - startRight * (startWidth * 0.5f + EdgeMargin),
                endCenter - endRight * (endWidth * 0.5f + EdgeMargin));
            CreateBoundarySegment(root.transform, material, "Right", index,
                startCenter + startRight * (startWidth * 0.5f + EdgeMargin),
                endCenter + endRight * (endWidth * 0.5f + EdgeMargin));
        }

        BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>();
        if (colliders.Length != segmentCount * 2)
            throw new InvalidOperationException($"Expected {segmentCount * 2} boundary colliders, but created {colliders.Length}.");
        if (root.GetComponentsInChildren<Renderer>().Length != 0)
            throw new InvalidOperationException("Soft track boundaries must remain invisible and cannot contain renderers.");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Circuit_01 soft boundaries.");

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log($"CIRCUIT_01_BOUNDARIES_OK: {segmentCount * 2} invisible soft BoxColliders saved, {EdgeMargin:F1}m beyond each road edge.");
    }

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Install();
    }

    private static void CreateBoundarySegment(
        Transform parent,
        PhysicsMaterial material,
        string side,
        int index,
        Vector3 start,
        Vector3 end)
    {
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.001f)
            return;

        GameObject segment = new GameObject($"Boundary_{side}_{index:000}");
        segment.transform.SetParent(parent);
        segment.transform.SetPositionAndRotation(
            (start + end) * 0.5f + Vector3.up * (WallHeight * 0.5f),
            Quaternion.LookRotation(direction.normalized, Vector3.up));

        BoxCollider collider = segment.AddComponent<BoxCollider>();
        collider.size = new Vector3(WallThickness, WallHeight, direction.magnitude + SegmentOverlap);
        collider.sharedMaterial = material;
    }

    private static PhysicsMaterial GetOrCreateMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial("Circuit 01 Soft Boundary");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.dynamicFriction = 0.05f;
        material.staticFriction = 0.05f;
        material.bounciness = 0.15f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void DefineRoute()
    {
        Sections.Clear();
        totalLength = 0f;
        Vector3 point = Vector3.zero;
        float heading = 0f;
        float[] straights = { (100f - 40f * Mathf.PI / 4f) / 2f, 70f, 100f, 45f, 90f, 0f, 90f, 45f, 120f };
        float[] turns = { 45f, -45f, 110f, -110f, 35f, -35f, -100f, 100f };
        float[] radii = { 40f, 60f, 25f, 30f, 45f, 45f, 25f, 35f };

        for (int index = 0; index < straights.Length; index++)
        {
            if (straights[index] > 0f)
                AddSection(straights[index], 0f, ref point, ref heading);
            if (index < turns.Length)
                AddSection(Mathf.Abs(turns[index]) * Mathf.Deg2Rad * radii[index],
                    Mathf.Sign(turns[index]) / radii[index], ref point, ref heading);
        }
    }

    private static void AddSection(float length, float curvature, ref Vector3 point, ref float heading)
    {
        Section section = new Section
        {
            start = totalLength,
            length = length,
            yaw = heading,
            curvature = curvature,
            origin = point
        };
        Sections.Add(section);
        Position(section, length, out point, out heading);
        totalLength += length;
    }

    private static void Position(Section section, float distance, out Vector3 point, out float yaw)
    {
        yaw = section.yaw + section.curvature * distance;
        if (Mathf.Abs(section.curvature) < 0.00001f)
            point = section.origin + new Vector3(Mathf.Sin(section.yaw), 0f, Mathf.Cos(section.yaw)) * distance;
        else
            point = section.origin + new Vector3(
                (Mathf.Cos(section.yaw) - Mathf.Cos(yaw)) / section.curvature,
                0f,
                (Mathf.Sin(yaw) - Mathf.Sin(section.yaw)) / section.curvature);
    }

    private static void Sample(float distance, out Vector3 center, out Vector3 right, out float width)
    {
        Section section = Sections.FirstOrDefault(candidate => distance <= candidate.start + candidate.length + 0.0001f)
                          ?? Sections.Last();
        Position(section, Mathf.Clamp(distance - section.start, 0f, section.length), out center, out float yaw);
        center.y = Hill(distance, 120f, 280f, 10f)
                   + Hill(distance, 390f, 550f, -7f)
                   + Hill(distance, 640f, 850f, 13f);
        right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
        width = 12f;

        foreach (Section bend in Sections.Where(candidate => candidate.curvature != 0f))
        {
            float gap = Mathf.Max(bend.start - distance, distance - bend.start - bend.length, 0f);
            float bendWidth = Mathf.Abs(bend.curvature * bend.length * Mathf.Rad2Deg) >= 90f ? 7f : 9f;
            width = Mathf.Min(width, Mathf.Lerp(bendWidth, 12f, Mathf.SmoothStep(0f, 1f, gap / 20f)));
        }
    }

    private static float Hill(float distance, float start, float end, float height)
    {
        if (distance <= start || distance >= end)
            return 0f;
        return height * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * (distance - start) / (end - start)));
    }
}
