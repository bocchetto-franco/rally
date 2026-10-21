using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repeatable Windows x64 release using the saved Build Settings scenes.</summary>
[InitializeOnLoad]
public static class BuildWindows
{
    public const string Executable = "Build/Windows/Rally.exe";
    public const string BuildLog = "Build/Windows/BuildLog.txt";
    const string PendingKey = "Rally.WindowsRelease.Pending";
    const string StartedKey = "Rally.WindowsRelease.Started";
    static bool capturing;

    static BuildWindows()
    {
        // A platform switch can reload Editor assemblies before the menu callback returns.
        if (SessionState.GetBool(PendingKey, false)) QueueBuild();
    }

    [MenuItem("Build/Windows Release")]
    public static void BuildRelease()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Absolute(Executable)));
        string started = DateTimeOffset.Now.ToString("O");
        File.WriteAllText(Absolute(BuildLog), "Rally Windows x64 Release\nStarted: " + started + "\n");
        SessionState.SetString(StartedKey, started);
        try
        {
            Validate();
            if (SessionState.GetBool(PendingKey, false)) throw new BuildFailedException("Ya hay un build de Windows pendiente.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Falta Windows Build Support. Agregalo a esta versión desde Unity Hub.");
            SessionState.SetBool(PendingKey, true);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                if (Application.isBatchMode) throw new BuildFailedException("En batch mode iniciá Unity con -buildTarget Win64.");
                Append("Switching build target to Windows x64.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new BuildFailedException("Unity no pudo cambiar a la plataforma Windows x64.");
            }
            if (Application.isBatchMode) BuildNow(); else QueueBuild();
        }
        catch (Exception error) { Fail(error); }
    }

    [MenuItem("Build/Windows Release", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer && !SessionState.GetBool(PendingKey, false);

    static string Absolute(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    static void Append(string line) => File.AppendAllText(Absolute(BuildLog), line + Environment.NewLine);

    /// <summary>Read-only preflight, also usable by external verification tools.</summary>
    public static string[] Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new BuildFailedException("Salí de Play y esperá a que termine el build anterior.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new BuildFailedException("Unity está importando o compilando. Esperá y volvé a ejecutar Build/Windows Release.");
        if (EditorUtility.scriptCompilationFailed)
            throw new BuildFailedException("Hay errores de compilación. Corregí los errores rojos de Console antes de generar el build.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new BuildFailedException("Guardá la escena antes del build: " + SceneManager.GetSceneAt(i).path);
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || scenes[0] != "Assets/Scenes/MainMenu.unity")
            throw new BuildFailedException("MainMenu debe ser la primera escena habilitada en Build Settings / Build Profiles.");
        foreach (string name in new[] { "MainMenu", "VehicleCircuitSelection", "Circuit_01", "Circuit_02", "Circuit_03", "RaceResults" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!scenes.Contains(path) || !File.Exists(Absolute(path)))
                throw new BuildFailedException("Falta una escena habilitada y guardada: " + path);
        }
        foreach (string scene in scenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                throw new BuildFailedException("Escena de Build Settings inválida: " + scene);
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        if (settings.FindProperty("activeInputHandler").intValue != 2)
            throw new BuildFailedException("Active Input Handling debe permanecer en Both.");
        return scenes;
    }

    static void QueueBuild()
    {
        EditorApplication.update -= WaitForReady;
        EditorApplication.update += WaitForReady;
    }
    static void WaitForReady()
    {
        if (!SessionState.GetBool(PendingKey, false)) { EditorApplication.update -= WaitForReady; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForReady;
        BuildNow();
    }

    static void Capture(string message, string stack, LogType type)
    {
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Append(type + ": " + message);
    }

    static void BuildNow()
    {
        try
        {
            string[] scenes = Validate();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("El cambio de plataforma no terminó en Windows x64.");
            SessionState.SetBool(PendingKey, false);
            // Keep credits in the selection menu and distributed notices synchronized.
            RallyCreditsSetup.Sync();
            Append("Unity: " + Application.unityVersion + "\nProduct: " + PlayerSettings.productName + "\nVersion: " + PlayerSettings.bundleVersion +
                "\nInput: Both\nDevelopment: false\nScenes:\n" + string.Join("\n", scenes) + "\nOutput: " + Absolute(Executable));
            capturing = true; Application.logMessageReceived += Capture;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = Absolute(Executable), target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
            });
            var result = report.summary;
            Append($"Finished: {DateTimeOffset.Now:O}\nResult: {result.result}\nErrors: {result.totalErrors}\nWarnings: {result.totalWarnings}\nDuration: {result.totalTime}\nBytes: {result.totalSize}");
            if (result.result != BuildResult.Succeeded || result.totalErrors != 0 || !File.Exists(Absolute(Executable)))
                throw new BuildFailedException("Falló el build de Windows. Revisá " + Absolute(BuildLog));
            File.WriteAllText(Absolute("Build/Windows/BuildInfo.txt"),
                "Generated: " + SessionState.GetString(StartedKey, "") + "\nUnity: " + Application.unityVersion +
                "\nVersion: " + PlayerSettings.bundleVersion + "\nExecutable: Rally.exe\nEntry: MainMenu\nInput: Both\n" +
                "Conservar Rally_Data, UnityPlayer.dll y todos los archivos vecinos junto a Rally.exe.\n");
            Debug.Log("Windows Release listo: " + Absolute(Executable) + "\nLog: " + Absolute(BuildLog));
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Rally — Windows Release", "Build generado correctamente:\n" + Absolute(Executable) + "\n\nLog:\n" + Absolute(BuildLog), "Aceptar");
        }
        catch (Exception error) { Fail(error); }
        finally { if (capturing) { Application.logMessageReceived -= Capture; capturing = false; } }
    }

    static void Fail(Exception error)
    {
        SessionState.SetBool(PendingKey, false); EditorApplication.update -= WaitForReady;
        Append("FAILED " + DateTimeOffset.Now.ToString("O") + ": " + error);
        Debug.LogError("Windows Release cancelado: " + error.Message);
        if (Application.isBatchMode) throw new BuildFailedException(error.Message);
        EditorUtility.DisplayDialog("Rally — no se generó el build", error.Message + "\n\nLog:\n" + Absolute(BuildLog), "Aceptar");
    }
}
