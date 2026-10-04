using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Places three independently tuned instances of the existing bot prefab.</summary>
public static class RallyMultiBotSetup
{
    private const string PrefabPath = "Assets/Prefabs/Bot_Car.prefab";
    private const string GripProfilePath = "Assets/Prefabs/Bot_Grip_Profile.asset";
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Circuit_01.unity",
        "Assets/Scenes/Circuit_02.unity",
        "Assets/Scenes/Circuit_03.unity"
    };
    private static readonly string[] BotNames = { "Bot_Car", "Bot_Car_02", "Bot_Car_03" };
    private static readonly float[] SpeedsKph = { 110f, 118f, 126f };
    private static readonly float[] LaneOffsets = { -0.9f, 0f, 0.9f };

    [MenuItem("Tools/Rally/Place Three Bots In All Circuits")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the open scene and leave Play before placing bots.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        RallyBotGripProfile profile = AssetDatabase.LoadAssetAtPath<RallyBotGripProfile>(GripProfilePath);
        if (prefab == null || profile == null)
            throw new InvalidOperationException("The existing Bot_Car prefab or bot grip profile is missing.");

        var results = new List<string>();
        foreach (string path in ScenePaths)
            InstallScene(path, prefab, profile, results);
        Debug.Log("MULTI_BOT_SETUP_COMPLETE: " + string.Join(" | ", results));
    }

    private static void InstallScene(string path, GameObject prefab, RallyBotGripProfile profile,
        List<string> results)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject route = FindRoot(scene, "AI_Waypoints");
        GameObject player = FindRoot(scene, "Porsche 911 SC Rally");
        if (route == null || route.transform.childCount < 3 || player == null)
            throw new InvalidOperationException(scene.name + " needs its Porsche and AI_Waypoints.");

        GameObject[] bots = new GameObject[BotNames.Length];
        for (int i = 0; i < bots.Length; i++)
            bots[i] = FindRoot(scene, BotNames[i]);
        if (bots[0] == null)
            throw new InvalidOperationException(scene.name + " is missing its original Bot_Car.");
        if (bots[1] != null && bots[2] != null)
        {
            if (FindRoot(scene, "Starting Grid") == null)
            {
                RallyStartingGridSetup.PlaceGrid(scene, new[] { player, bots[0], bots[1], bots[2] }, route.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save " + path);
            }
            VerifyBots(scene, bots, player, route.transform, prefab, profile);
            results.Add(scene.name + ": three bots already present");
            return;
        }

        for (int i = 0; i < bots.Length; i++)
        {
            if (bots[i] == null)
                bots[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            bots[i].name = BotNames[i];

            RallyBotController driver = bots[i].GetComponent<RallyBotController>();
            JrsVehicleController vehicle = bots[i].GetComponentInChildren<JrsVehicleController>();
            Rigidbody body = vehicle != null ? vehicle.GetComponent<Rigidbody>() : null;
            if (driver == null || body == null)
                throw new InvalidOperationException(BotNames[i] + " has no bot driver or vehicle Rigidbody.");

            driver.Configure(vehicle, body, route.transform);
            driver.SetDifficulty(SpeedsKph[i], 0.88f);
            driver.SetLaneOffset(LaneOffsets[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bots[i]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
        }

        RallyStartingGridSetup.PlaceGrid(scene, new[] { player, bots[0], bots[1], bots[2] }, route.transform);
        VerifyBots(scene, bots, player, route.transform, prefab, profile);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + path);
        results.Add(scene.name + ": 3 bots at 110/118/126 km/h");
    }

    private static void VerifyBots(Scene scene, GameObject[] bots, GameObject player, Transform route, GameObject prefab,
        RallyBotGripProfile profile)
    {
        for (int i = 0; i < bots.Length; i++)
        {
            GameObject bot = bots[i];
            if (bot == null || bot.scene != scene ||
                PrefabUtility.GetCorrespondingObjectFromSource(bot) != prefab)
                throw new InvalidOperationException(scene.name + " has a missing or non-prefab bot.");
            RallyBotController driver = bot.GetComponent<RallyBotController>();
            RallyVehicleDynamics dynamics = bot.GetComponent<RallyVehicleDynamics>();
            if (driver == null || dynamics == null ||
                new SerializedObject(driver).FindProperty("waypointRoot").objectReferenceValue != route ||
                new SerializedObject(dynamics).FindProperty("botGripProfile").objectReferenceValue != profile)
                throw new InvalidOperationException(bot.name + " lost its route or bot-only grip profile.");
        }
        RallyStartingGridSetup.VerifyGrid(scene, new[] { player, bots[0], bots[1], bots[2] });
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

}
