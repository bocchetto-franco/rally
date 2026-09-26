using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RallyBotPrefabSetup
{
    private const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    private const string PrefabPath = "Assets/Prefabs/Bot_Car.prefab";
    private const string RequestPath = "Logs/bot-setup-request.txt";

    static RallyBotPrefabSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;
        EditorApplication.update -= ProcessRequest;
        File.Move(RequestPath, RequestPath + ".consumed-" + DateTime.Now.Ticks);
        try { Install(); }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/bot-setup-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Rally/Create Bot Car In Circuit 01")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before creating the bot.");
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save or discard the currently open scene before creating the bot.");
        if (File.Exists(PrefabPath))
            throw new InvalidOperationException("Bot_Car.prefab already exists; refusing to overwrite it.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Porsche 911 SC Rally");
        GameObject dynamicsObject = GameObject.Find("Porsche Rally Dynamics");
        GameObject route = GameObject.Find("AI_Waypoints");
        if (player == null || dynamicsObject == null || route == null || route.transform.childCount < 3)
            throw new InvalidOperationException("Circuit_01 is missing the Porsche, its dynamics or AI_Waypoints.");
        if (GameObject.Find("Bot_Car") != null)
            throw new InvalidOperationException("Circuit_01 already contains Bot_Car.");

        JrsVehicleController playerController = player.GetComponent<JrsVehicleController>();
        RallyVehicleDynamics playerDynamics = dynamicsObject.GetComponent<RallyVehicleDynamics>();
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        if (playerController == null || playerDynamics == null || playerBody == null)
            throw new InvalidOperationException("The player Porsche has incomplete vehicle components.");

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject prefabRoot = new GameObject("Bot_Car");
        try
        {
            GameObject clonedPorsche = Object.Instantiate(player, prefabRoot.transform, false);
            clonedPorsche.name = "Porsche 911 SC Rally - Bot";
            clonedPorsche.transform.localPosition = Vector3.zero;
            clonedPorsche.transform.localRotation = Quaternion.identity;
            clonedPorsche.transform.localScale = Vector3.one;
            JrsVehicleController botPhysics = clonedPorsche.GetComponent<JrsVehicleController>();
            Rigidbody botBody = clonedPorsche.GetComponent<Rigidbody>();
            if (botPhysics == null || botBody == null || botPhysics.frontLeftWheel == null ||
                botPhysics.frontRightWheel == null || botPhysics.rearLeftWheel == null || botPhysics.rearRightWheel == null ||
                botPhysics.frontLeftWheel == playerController.frontLeftWheel)
                throw new InvalidOperationException("The Porsche clone did not retain its own four WheelColliders.");

            RallyBotController botDriver = prefabRoot.AddComponent<RallyBotController>();
            botDriver.Configure(botPhysics, botBody);
            botPhysics.SetBotInput(botDriver);
            RallyVehicleDynamics botDynamics = prefabRoot.AddComponent<RallyVehicleDynamics>();
            EditorUtility.CopySerialized(playerDynamics, botDynamics);
            SerializedObject tuning = new SerializedObject(botDynamics);
            tuning.FindProperty("controller").objectReferenceValue = botPhysics;
            tuning.FindProperty("vehicleBody").objectReferenceValue = botBody;
            tuning.FindProperty("frontLeft").objectReferenceValue = botPhysics.frontLeftWheel;
            tuning.FindProperty("frontRight").objectReferenceValue = botPhysics.frontRightWheel;
            tuning.FindProperty("rearLeft").objectReferenceValue = botPhysics.rearLeftWheel;
            tuning.FindProperty("rearRight").objectReferenceValue = botPhysics.rearRightWheel;
            tuning.FindProperty("rearLeftDriftSmoke").objectReferenceValue = botPhysics.rearLeftDustParticleSystem;
            tuning.FindProperty("rearRightDriftSmoke").objectReferenceValue = botPhysics.rearRightDustParticleSystem;
            tuning.FindProperty("botInput").objectReferenceValue = botDriver;
            tuning.ApplyModifiedPropertiesWithoutUndo();
            botDynamics.SetBotInput(botDriver);
            botDynamics.enabled = true;
            botPhysics.enabled = true;
            botDynamics.ApplySetup();

            if (Mathf.Abs(botBody.mass - playerBody.mass) > 0.01f ||
                Mathf.Abs(botBody.angularDamping - playerBody.angularDamping) > 0.001f ||
                Mathf.Abs(botPhysics.rearLeftWheel.sidewaysFriction.extremumValue -
                          playerController.rearLeftWheel.sidewaysFriction.extremumValue) > 0.001f)
                throw new InvalidOperationException("Bot physics tuning differs from the player Porsche.");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Failed to save Bot_Car prefab.");
        }
        finally
        {
            Object.DestroyImmediate(prefabRoot);
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject sceneBot = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
        sceneBot.transform.SetPositionAndRotation(
            player.transform.position + player.transform.forward * 12f + player.transform.right * 2.2f,
            player.transform.rotation);
        sceneBot.GetComponent<RallyBotController>().Configure(
            sceneBot.GetComponentInChildren<JrsVehicleController>(),
            sceneBot.GetComponentInChildren<Rigidbody>(), route.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Circuit_01 with Bot_Car.");
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/bot-setup-result.txt",
            $"Prefab: {PrefabPath}\nScene: {ScenePath}\nBot position: {sceneBot.transform.position}\nWaypoints: {route.transform.childCount}\nPlayer mass: {playerBody.mass}\nBot mass: {sceneBot.GetComponentInChildren<Rigidbody>().mass}\n");
        Debug.Log("BOT_CAR_SETUP_COMPLETE");
    }
}
