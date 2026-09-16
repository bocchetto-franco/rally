using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RallyFrontendSetup
{
    const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    const string SelectionPath = "Assets/Scenes/VehicleCircuitSelection.unity";
    const string RacePath = "Assets/Scenes/Circuit_01.unity";
    const string ResultsPath = "Assets/Scenes/RaceResults.unity";
    const string RaceFlowName = "Race Flow";

    static readonly string[] BuildScenePaths = { MainMenuPath, SelectionPath, RacePath, ResultsPath };
    static bool busy;

    static RallyFrontendSetup() => EditorApplication.delayCall += InstallOnce;

    static void InstallOnce()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallOnce;
            return;
        }

        bool scenesMissing = !File.Exists(MainMenuPath) || !File.Exists(SelectionPath) || !File.Exists(ResultsPath);
        bool buildSettingsMissing = !BuildScenePaths.SequenceEqual(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
        bool raceFlowMissing = SceneManager.GetActiveScene().path == RacePath && GameObject.Find(RaceFlowName) == null;
        if (scenesMissing || buildSettingsMissing || raceFlowMissing)
            BuildAll();
    }

    [MenuItem("Tools/Rally/Build Frontend Flow")]
    public static void BuildAll()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        busy = true;
        try
        {
            Scene originalScene = SceneManager.GetActiveScene();
            string originalPath = originalScene.path;
            if (originalScene.isDirty && !string.IsNullOrEmpty(originalPath))
                EditorSceneManager.SaveScene(originalScene);

            CreateFrontendScene(MainMenuPath, RallyMenuScreen.MainMenu, "Rally Main Menu");
            CreateFrontendScene(SelectionPath, RallyMenuScreen.Selection, "Rally Vehicle and Circuit Selection");
            CreateFrontendScene(ResultsPath, RallyMenuScreen.Results, "Rally Race Results");

            Scene raceScene = EditorSceneManager.OpenScene(RacePath, OpenSceneMode.Single);
            GameObject raceFlow = GameObject.Find(RaceFlowName);
            if (raceFlow == null)
                raceFlow = new GameObject(RaceFlowName);
            RallyRaceFlow flow = raceFlow.GetComponent<RallyRaceFlow>();
            if (flow == null)
                flow = raceFlow.AddComponent<RallyRaceFlow>();
            RallyCheckpointManager manager = UnityEngine.Object.FindAnyObjectByType<RallyCheckpointManager>();
            if (manager == null)
                throw new InvalidOperationException("Circuit_01 requires its checkpoint manager before frontend setup.");
            flow.Configure(manager);
            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(raceScene);
            if (!EditorSceneManager.SaveScene(raceScene))
                throw new InvalidOperationException("Could not save Circuit_01 race flow.");

            EditorBuildSettings.scenes = BuildScenePaths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            AssetDatabase.SaveAssets();

            string sceneToRestore = !string.IsNullOrEmpty(originalPath) && File.Exists(originalPath) ? originalPath : MainMenuPath;
            EditorSceneManager.OpenScene(sceneToRestore, OpenSceneMode.Single);
            Debug.Log("RALLY_FRONTEND_OK: MainMenu -> VehicleCircuitSelection -> Circuit_01 -> RaceResults; build settings updated.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            busy = false;
        }
    }

    static void CreateFrontendScene(string path, RallyMenuScreen screen, string rootName)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.027f, 0.043f);
        camera.cullingMask = 0;
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        GameObject root = new GameObject(rootName);
        RallyMenuController controller = root.AddComponent<RallyMenuController>();
        controller.Configure(screen);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Could not save frontend scene: " + path);
    }
}
