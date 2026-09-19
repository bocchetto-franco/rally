using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Circuit01RallyDynamicsSetup
{
    private const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    private const string SetupName = "Porsche Rally Dynamics";
    private const string MarkerRootName = "Race Start Finish Markers";

    static Circuit01RallyDynamicsSetup()
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

        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        GameObject setup = GameObject.Find(SetupName);
        RallyVehicleDynamics dynamics = setup != null ? setup.GetComponent<RallyVehicleDynamics>() : null;
        if (dynamics == null || !dynamics.HasConfiguredHighSpeedEffects)
            Install();
    }

    [MenuItem("Tools/Rally/Configure Porsche Rally Dynamics")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject car = scene.GetRootGameObjects().Single(root => root.name == "Porsche 911 SC Rally");
        JrsVehicleController controller = car.GetComponent<JrsVehicleController>();
        Rigidbody body = car.GetComponent<Rigidbody>();
        if (controller == null || body == null)
            throw new InvalidOperationException("The Porsche vehicle controller or Rigidbody is missing.");

        GameObject setup = GameObject.Find(SetupName);
        if (setup == null)
            setup = new GameObject(SetupName);

        RallyVehicleDynamics dynamics = setup.GetComponent<RallyVehicleDynamics>();
        if (dynamics == null)
            dynamics = setup.AddComponent<RallyVehicleDynamics>();

        dynamics.Configure(
            controller,
            body,
            controller.frontLeftWheel,
            controller.frontRightWheel,
            controller.rearLeftWheel,
            controller.rearRightWheel);

        CreateStartAndFinishMarkers(scene);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(dynamics);
        foreach (WheelCollider wheel in new[]
                 {
                     controller.frontLeftWheel, controller.frontRightWheel,
                     controller.rearLeftWheel, controller.rearRightWheel
                 })
            EditorUtility.SetDirty(wheel);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Circuit_01 rally dynamics.");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = setup;
        Debug.Log("CIRCUIT_01_RALLY_DYNAMICS_OK: 70/30 ABS-modulated service braking, later spin limit, stronger quadratic downforce and existing rear smoke saved.");
    }

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Install();
    }

    private static void CreateStartAndFinishMarkers(Scene scene)
    {
        Transform checkpointRoot = scene.GetRootGameObjects()
            .Single(root => root.name == "Checkpoint System").transform;
        Transform start = FindChild(checkpointRoot, "Checkpoint_00_Start");
        Transform finish = FindChild(checkpointRoot, "Checkpoint_06_Finish");

        GameObject markerRoot = GameObject.Find(MarkerRootName);
        if (markerRoot == null)
            markerRoot = new GameObject(MarkerRootName);

        ClearChildren(markerRoot.transform);
        Material startMaterial = GetOrCreateMaterial("Assets/Scenes/Circuit_01_StartLine.mat", new Color(1f, 0.55f, 0.03f));
        Material whiteMaterial = GetOrCreateMaterial("Assets/Scenes/Circuit_01_FinishWhite.mat", Color.white);
        Material blackMaterial = GetOrCreateMaterial("Assets/Scenes/Circuit_01_FinishBlack.mat", new Color(0.025f, 0.025f, 0.025f));

        CreateStripe(markerRoot.transform, "Start Line", start, 12f, 0.8f, startMaterial);
        CreateCheckerLine(markerRoot.transform, finish, 12f, whiteMaterial, blackMaterial);
    }

    private static Transform FindChild(Transform root, string exactName)
    {
        Transform result = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == exactName);
        if (result == null)
            throw new InvalidOperationException(exactName + " was not found.");
        return result;
    }

    private static void CreateCheckerLine(Transform root, Transform gate, float width, Material white, Material black)
    {
        const int columns = 12;
        const int rows = 2;
        float tileWidth = width / columns;
        const float tileLength = 0.55f;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            GameObject tile = CreateVisualCube($"Finish_{row:0}_{column:00}", root, (row + column) % 2 == 0 ? white : black);
            tile.transform.SetPositionAndRotation(
                gate.position - Vector3.up * 1.96f + gate.right * (-width * 0.5f + tileWidth * (column + 0.5f)) + gate.forward * (tileLength * (row - 0.5f)),
                gate.rotation);
            tile.transform.localScale = new Vector3(tileWidth, 0.04f, tileLength);
        }
    }

    private static void CreateStripe(Transform root, string name, Transform gate, float width, float length, Material material)
    {
        GameObject stripe = CreateVisualCube(name, root, material);
        stripe.transform.SetPositionAndRotation(gate.position - Vector3.up * 1.96f, gate.rotation);
        stripe.transform.localScale = new Vector3(width, 0.04f, length);
    }

    private static GameObject CreateVisualCube(string name, Transform parent, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube;
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader was not found.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ClearChildren(Transform root)
    {
        for (int index = root.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(root.GetChild(index).gameObject);
    }
}
