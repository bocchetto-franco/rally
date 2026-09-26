using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RallyBotsAndCheckpointsSetup
{
    private const string RequestPath = "Logs/bots-checkpoints-request.txt";
    private const string ProfilePath = "Assets/Prefabs/Bot_Grip_Profile.asset";
    private const string PrefabPath = "Assets/Prefabs/Bot_Car.prefab";
    private const string HudSourcePath = "Assets/Scenes/Circuit_01.unity";

    [Serializable]
    private sealed class Layout
    {
        public float lengthMeters;
        public Vector3[] centerline;
        public float[] distances;
        public float[] roadWidths;
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

    static RallyBotsAndCheckpointsSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;
        EditorApplication.update -= ProcessRequest;
        File.Move(RequestPath, RequestPath + ".consumed-" + DateTime.Now.Ticks);
        try { InstallAll(); }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/bots-checkpoints-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Rally/Install Bot Grip and Circuit 02-03 Race Systems")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Leave Play and save the current scene before installing race systems.");

        RallyBotGripProfile profile = AssetDatabase.LoadAssetAtPath<RallyBotGripProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<RallyBotGripProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        UpdateBotPrefab(profile);

        var report = new List<string>();
        foreach (Circuit circuit in Circuits)
            InstallCircuit(circuit, report);
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Logs/bots-checkpoints-result.txt", report);
        Debug.Log("BOTS_AND_CHECKPOINTS_COMPLETE: " + string.Join(" | ", report));
    }

    private static void UpdateBotPrefab(RallyBotGripProfile profile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
            throw new InvalidOperationException("Bot_Car prefab is missing.");
        try
        {
            RallyBotController driver = root.GetComponent<RallyBotController>();
            RallyVehicleDynamics dynamics = root.GetComponent<RallyVehicleDynamics>();
            if (driver == null || dynamics == null || root.GetComponentInChildren<JrsVehicleController>() == null)
                throw new InvalidOperationException("Bot_Car prefab has incomplete vehicle components.");
            driver.SetDifficulty(80f, 0.95f);
            SerializedObject tuning = new SerializedObject(dynamics);
            tuning.FindProperty("botGripProfile").objectReferenceValue = profile;
            tuning.ApplyModifiedPropertiesWithoutUndo();
            dynamics.ApplySetup();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void InstallCircuit(Circuit circuit, List<string> report)
    {
        Scene scene = EditorSceneManager.OpenScene(circuit.scenePath, OpenSceneMode.Single);
        GameObject existingCheckpoints = FindRoot(scene, "Checkpoint System");
        GameObject existingBot = FindRoot(scene, "Bot_Car");
        if (existingCheckpoints != null && existingBot != null && FindRoot(scene, "Race HUD") != null)
        {
            report.Add(scene.name + ": already installed");
            return;
        }
        if (existingCheckpoints != null || existingBot != null || FindRoot(scene, "Race HUD") != null)
            throw new InvalidOperationException(scene.name + " is partially configured; inspect it before rerunning setup.");

        GameObject player = FindRoot(scene, "Porsche 911 SC Rally");
        GameObject route = FindRoot(scene, "AI_Waypoints");
        GameObject road = GameObject.Find(circuit.roadName);
        if (player == null || route == null || route.transform.childCount < 3 || road == null)
            throw new InvalidOperationException(scene.name + " is missing the player Porsche, AI_Waypoints or road.");
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        MeshCollider roadCollider = road.GetComponent<MeshCollider>();
        if (playerBody == null || roadCollider == null || roadCollider.sharedMesh == null)
            throw new InvalidOperationException(scene.name + " has no player Rigidbody or road MeshCollider.");
        int roadVertexCount = roadCollider.sharedMesh.vertexCount;
        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(circuit.layoutPath));
        if (layout == null || layout.centerline == null || layout.distances == null || layout.roadWidths == null ||
            layout.centerline.Length < 3 || layout.centerline.Length != layout.distances.Length ||
            layout.centerline.Length != layout.roadWidths.Length || layout.lengthMeters < 100f)
            throw new InvalidOperationException("Invalid circuit layout: " + circuit.layoutPath);

        GameObject checkpointRoot = new GameObject("Checkpoint System");
        RallyCheckpointManager manager = checkpointRoot.AddComponent<RallyCheckpointManager>();
        int gateCount = Mathf.CeilToInt((layout.lengthMeters - 24f) / 180f) + 1;
        var gates = new RallyCheckpointTrigger[gateCount];
        for (int i = 0; i < gateCount; i++)
        {
            float distance = Mathf.Lerp(12f, layout.lengthMeters - 12f, i / (gateCount - 1f));
            Sample(layout, distance, out Vector3 point, out Vector3 tangent, out float width);
            Ray ray = new Ray(point + Vector3.up * 25f, Vector3.down);
            if (!roadCollider.Raycast(ray, out RaycastHit hit, 65f))
                throw new InvalidOperationException($"{scene.name} gate {i} missed its road at {point}.");
            string label = i == 0 ? "Start" : i == gateCount - 1 ? "Finish" : $"{distance:000}m";
            GameObject gate = new GameObject($"Checkpoint_{i:00}_{label}");
            gate.transform.SetParent(checkpointRoot.transform, false);
            gate.transform.SetPositionAndRotation(hit.point + Vector3.up * 1.9f,
                Quaternion.LookRotation(tangent, Vector3.up));
            BoxCollider box = gate.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width + 0.8f, 4f, 2.4f);
            gates[i] = gate.AddComponent<RallyCheckpointTrigger>();
            gates[i].Configure(manager, i);
        }
        manager.Configure(gates);

        GameObject botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (botPrefab == null)
            throw new InvalidOperationException("Bot_Car prefab is missing.");
        GameObject bot = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab, scene);
        bot.transform.SetPositionAndRotation(
            player.transform.position + player.transform.forward * 12f + player.transform.right * 2.2f,
            player.transform.rotation);
        bot.GetComponent<RallyBotController>().Configure(
            bot.GetComponentInChildren<JrsVehicleController>(),
            bot.GetComponentInChildren<Rigidbody>(), route.transform);

        // Reuse the actual Circuit_01 Canvas/layout instead of making a second HUD implementation.
        Scene hudSource = EditorSceneManager.OpenScene(HudSourcePath, OpenSceneMode.Additive);
        GameObject sourceHud = FindRoot(hudSource, "Race HUD");
        if (sourceHud == null)
            throw new InvalidOperationException("Circuit_01 Race HUD template is missing.");
        GameObject hudObject = Object.Instantiate(sourceHud);
        hudObject.name = "Race HUD";
        if (hudObject.scene != scene)
            SceneManager.MoveGameObjectToScene(hudObject, scene);
        RallyRaceHud hud = hudObject.GetComponent<RallyRaceHud>();
        SerializedObject hudFields = new SerializedObject(hud);
        TMP_Text speed = hudFields.FindProperty("speedText").objectReferenceValue as TMP_Text;
        TMP_Text time = hudFields.FindProperty("timeText").objectReferenceValue as TMP_Text;
        TMP_Text checkpoint = hudFields.FindProperty("checkpointText").objectReferenceValue as TMP_Text;
        if (speed == null || time == null || checkpoint == null ||
            speed.gameObject.scene != scene || time.gameObject.scene != scene || checkpoint.gameObject.scene != scene)
            throw new InvalidOperationException("HUD text references were not cloned into " + scene.name);
        hud.Configure(manager, playerBody, speed, time, checkpoint);
        EditorSceneManager.CloseScene(hudSource, true);
        SceneManager.SetActiveScene(scene);

        GameObject flowObject = new GameObject("Race Flow");
        flowObject.AddComponent<RallyRaceFlow>().Configure(manager);
        if (roadCollider.sharedMesh.vertexCount != roadVertexCount)
            throw new InvalidOperationException("Road geometry changed in " + scene.name);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + circuit.scenePath);
        report.Add($"{scene.name}: {gateCount} gates, HUD/timer/results, 1 bot, {route.transform.childCount} waypoints");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static void Sample(Layout layout, float distance, out Vector3 point, out Vector3 tangent, out float width)
    {
        int index = Array.BinarySearch(layout.distances, distance);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 1, layout.distances.Length - 1);
        int before = index - 1;
        float span = layout.distances[index] - layout.distances[before];
        float blend = span > 0.001f ? (distance - layout.distances[before]) / span : 0f;
        point = Vector3.Lerp(layout.centerline[before], layout.centerline[index], blend);
        width = Mathf.Lerp(layout.roadWidths[before], layout.roadWidths[index], blend);
        tangent = (layout.centerline[index] - layout.centerline[before]).normalized;
        if (tangent.sqrMagnitude < 0.01f)
            tangent = Vector3.forward;
    }
}
