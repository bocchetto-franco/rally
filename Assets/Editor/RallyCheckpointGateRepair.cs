using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repairs and validates the race gates of Circuit_02 and Circuit_03 only.</summary>
public static class RallyCheckpointGateRepair
{
    [Serializable]
    private sealed class Layout
    {
        public float lengthMeters = default;
        public Vector3[] centerline = default;
        public float[] distances = default;
        public float[] roadWidths = default;
    }

    private struct Circuit
    {
        public string scenePath;
        public string layoutPath;
        public string roadName;
    }

    private static readonly Circuit[] Circuits =
    {
        new Circuit { scenePath = "Assets/Scenes/Circuit_02.unity", layoutPath = "Assets/Art/Environment/Circuit02/Circuit02Layout.json", roadName = "Circuit02_Road" },
        new Circuit { scenePath = "Assets/Scenes/Circuit_03.unity", layoutPath = "Assets/Art/Forest/Circuit03/Circuit03Layout.json", roadName = "Circuit03_Road" }
    };

    [MenuItem("Tools/Rally/Repair Circuit 02-03 Checkpoint Gates")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the active scene and leave Play before repairing checkpoints.");

        foreach (Circuit circuit in Circuits)
        {
            Scene scene = EditorSceneManager.OpenScene(circuit.scenePath, OpenSceneMode.Single);
            GameObject root = FindRoot(scene, "Checkpoint System");
            GameObject routeObject = FindRoot(scene, "AI_Waypoints");
            MeshCollider road = GameObject.Find(circuit.roadName)?.GetComponent<MeshCollider>();
            RallyCheckpointManager manager = root != null ? root.GetComponent<RallyCheckpointManager>() : null;
            if (manager == null || routeObject == null || road == null)
                throw new InvalidOperationException(scene.name + " needs its checkpoint system, AI route and road collider.");

            Layout layout = JsonUtility.FromJson<Layout>(System.IO.File.ReadAllText(circuit.layoutPath));
            if (layout == null || layout.distances == null || layout.roadWidths == null ||
                layout.distances.Length != layout.roadWidths.Length || layout.distances.Length < 2)
                throw new InvalidOperationException("Invalid circuit layout: " + circuit.layoutPath);
            SerializedProperty references = new SerializedObject(manager).FindProperty("checkpoints");
            int count = Mathf.CeilToInt((layout.lengthMeters - 24f) / 180f) + 1;
            if (references.arraySize != count || root.transform.childCount != count)
                throw new InvalidOperationException(scene.name + " has a missing or extra checkpoint; inspect before repairing.");

            var gates = new RallyCheckpointTrigger[count];
            float previousDistance = -1f;
            for (int i = 0; i < count; i++)
            {
                RallyCheckpointTrigger gate = references.GetArrayElementAtIndex(i).objectReferenceValue as RallyCheckpointTrigger;
                if (gate == null || gate.gameObject.scene != scene || gate.transform.parent != root.transform)
                    throw new InvalidOperationException(scene.name + " has a missing or foreign checkpoint reference at index " + i);
                BoxCollider box = gate.GetComponent<BoxCollider>();
                if (box == null)
                    throw new InvalidOperationException(gate.name + " has no BoxCollider.");

                float expectedDistance = Mathf.Lerp(12f, layout.lengthMeters - 12f, i / (count - 1f));
                float routeDistance = Project(routeObject.transform, gate.transform.position, out float lateralError, out Vector3 direction);
                float alignment = Vector3.Dot(direction, Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up).normalized);
                if (routeDistance <= previousDistance || Mathf.Abs(routeDistance - expectedDistance) > 6f ||
                    lateralError > 1f || alignment < 0.9f)
                    throw new InvalidOperationException(scene.name + " has a misplaced or unordered gate: " + gate.name);
                previousDistance = routeDistance;

                Ray ray = new Ray(gate.transform.position + Vector3.up * 80f, Vector3.down);
                if (!road.Raycast(ray, out RaycastHit roadHit, 200f))
                    throw new InvalidOperationException(gate.name + " is not over the road.");
                gate.transform.position = new Vector3(gate.transform.position.x, roadHit.point.y + 3.5f, gate.transform.position.z);

                float roadWidth = SampleWidth(layout, expectedDistance);
                // The old width had only 0.4 m of margin per side. A 3 m shoulder
                // catches normal rally excursions without reaching the barriers.
                box.size = new Vector3(roadWidth + 6f, 8f, 6f);
                gate.Configure(manager, i);
                gate.transform.SetSiblingIndex(i);
                gates[i] = gate;
            }
            Physics.SyncTransforms();
            for (int i = 0; i < gates.Length; i++)
            {
                BoxCollider first = gates[i].GetComponent<BoxCollider>();
                for (int j = i + 1; j < gates.Length; j++)
                {
                    BoxCollider second = gates[j].GetComponent<BoxCollider>();
                    if (Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                            second, second.transform.position, second.transform.rotation, out _, out _))
                        throw new InvalidOperationException(scene.name + " has overlapping checkpoints: " + i + " and " + j);
                }
            }
            manager.Configure(gates);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + circuit.scenePath);
            Debug.Log($"CHECKPOINT_GATES_REPAIRED: {scene.name} {count} ordered gates, road width + 6 m, height 8 m, depth 6 m");
        }
    }

    private static float SampleWidth(Layout layout, float distance)
    {
        int index = Array.BinarySearch(layout.distances, distance);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 1, layout.distances.Length - 1);
        int before = index - 1;
        float span = layout.distances[index] - layout.distances[before];
        float blend = span > 0.001f ? (distance - layout.distances[before]) / span : 0f;
        return Mathf.Lerp(layout.roadWidths[before], layout.roadWidths[index], blend);
    }

    private static float Project(Transform route, Vector3 position, out float lateralError, out Vector3 direction)
    {
        float bestError = float.PositiveInfinity;
        float bestDistance = 0f;
        Vector3 bestDirection = Vector3.forward;
        float cumulative = 0f;
        Vector3 point = Vector3.ProjectOnPlane(position, Vector3.up);
        for (int i = 0; i < route.childCount; i++)
        {
            Vector3 a = Vector3.ProjectOnPlane(route.GetChild(i).position, Vector3.up);
            Vector3 b = Vector3.ProjectOnPlane(route.GetChild((i + 1) % route.childCount).position, Vector3.up);
            Vector3 leg = b - a;
            float length = leg.magnitude;
            if (length < 0.01f) continue;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, leg) / leg.sqrMagnitude);
            float error = (point - (a + leg * t)).sqrMagnitude;
            if (error < bestError)
            {
                bestError = error;
                bestDistance = cumulative + length * t;
                bestDirection = leg / length;
            }
            cumulative += length;
        }
        lateralError = Mathf.Sqrt(bestError);
        direction = bestDirection;
        return bestDistance;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }
}
