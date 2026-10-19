using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// External build utility. Never included in the game or used to regenerate scenes.
public static class RallyCurrentWindowsBuild
{
    const string Output = "Build/Windows_2026-10-18/Rally1.exe";
    const string Report = "Logs/windows-build-2026-10-18.txt";
    static readonly string[] SceneNames = { "MainMenu", "VehicleCircuitSelection", "Circuit_01", "Circuit_02", "Circuit_03", "RaceResults" };
    static readonly string[] Generated = {
        "Assets/Resources/MenuFonts/Oswald.asset", "Assets/Resources/MenuFonts/Roboto.asset", "Assets/Settings/RallyHUD_Font.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset", "Assets/Settings/PC_RPAsset.asset",
        "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset", "ProjectSettings/UnityConnectSettings.asset", "ProjectSettings/ProjectSettings.asset"
    };
    static readonly HashSet<string> Messages = new HashSet<string>();

    public static void BuildNow()
    {
        // A minimized Editor may not service delayCall until an inspector repaint.
        // Remove callbacks from earlier external assemblies before building directly.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var method = assembly.GetType(nameof(RallyCurrentWindowsBuild))?.GetMethod("Build", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)method.CreateDelegate(typeof(EditorApplication.CallbackFunction));
        }
        Start();
        EditorApplication.delayCall -= Build;
        Build();
    }

    public static string Start()
    {
        RequireIdle();
        if (File.Exists(Output)) throw new Exception("Preserve existing output before rebuilding: " + Output);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new Exception("Windows x64 module missing");
        var expected = SceneNames.Select(n => "Assets/Scenes/" + n + ".unity").ToArray();
        if (!EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(expected))
            throw new Exception("Build scene order differs from expected menu/selection/three circuits/results");
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        if (settings.FindProperty("activeInputHandler").intValue != 2) throw new Exception("Input must remain Both");
        foreach (string model in new[] { "LanciaS4", "Peugeot205T16", "AudiQuattro" })
            if (Resources.Load<GameObject>("Vehicles/" + model) == null || Resources.Load<GameObject>("MenuVehicles/" + model) == null)
                throw new Exception("Missing race or menu model " + model);
        RallyCreditsSetup.Sync();
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Report, "QUEUED " + DateTime.UtcNow.ToString("O") + "\n");
        EditorApplication.delayCall += Build;
        return "Queued Windows release: " + Path.GetFullPath(Output);
    }

    static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer || StaticOcclusionCulling.isRunning)
            throw new Exception("Editor must be idle");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
    }
    static void Capture(string message, string stack, LogType type)
    {
        if (type == LogType.Log) return;
        string item = type + ": " + message;
        if (Messages.Add(item)) File.AppendAllText(Report, item + "\n");
    }
    static void Build()
    {
        string backup = "Logs/SceneBackups/WindowsBuild_20261018_" + DateTime.UtcNow.ToString("HHmmss");
        var preloaded = PlayerSettings.GetPreloadedAssets();
        var bytes = Generated.Where(File.Exists).ToDictionary(p => p, File.ReadAllBytes);
        var scenes = EditorSceneManager.GetSceneManagerSetup();
        Messages.Clear(); Application.logMessageReceived += Capture;
        try
        {
            RequireIdle();
            foreach (var pair in bytes)
            {
                string path = backup + "/" + pair.Key; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pair.Value);
            }
            File.AppendAllText(Report, "START " + DateTime.UtcNow.ToString("O") + "\nProduct=" + PlayerSettings.productName +
                "; Version=" + PlayerSettings.bundleVersion + "; Input=Both; Resolution=" + PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight +
                "; Mode=" + PlayerSettings.fullScreenMode + "; Development=false; Backend=" + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) + "\n" +
                string.Join("\n", SceneNames.Select((n,i) => "Scene " + i + ": " + n)) + "\n");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = SceneNames.Select(n => "Assets/Scenes/" + n + ".unity").ToArray(),
                locationPathName = Output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
            });
            foreach (var step in result.steps) foreach (var message in step.messages)
                if (message.type != LogType.Log) Capture(message.content, "", message.type);
            var summary = result.summary;
            File.AppendAllText(Report, $"Result={summary.result}; Errors={summary.totalErrors}; Warnings={summary.totalWarnings}; Bytes={summary.totalSize}; Duration={summary.totalTime}\nOutput={Path.GetFullPath(Output)}\n");
            if (summary.result != BuildResult.Succeeded || !File.Exists(Output)) throw new Exception("Windows build failed");
            File.Copy(Report, Path.GetDirectoryName(Output) + "/BuildReport.txt", true);
            File.WriteAllText(Path.GetDirectoryName(Output) + "/BuildInfo.txt", "Rally1 Windows x64 release\nBuilt UTC: " + DateTime.UtcNow.ToString("O") +
                "\nUnity: " + Application.unityVersion + "\nVersion: " + PlayerSettings.bundleVersion +
                "\nCars: Lancia Delta S4 / Peugeot 205 T16 / Audi Quattro S1\nIncludes speed/steering dust trail (2026-10-18).\nEntry scene: MainMenu\nInput: Both\nKeep Rally1_Data, UnityPlayer.dll and other adjacent files with Rally1.exe.\n");
            File.AppendAllText(Report, "COMPLETE: PASS\n");
        }
        catch (Exception error) { File.AppendAllText(Report, "FAILED: " + error + "\n"); Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= Capture;
            PlayerSettings.SetPreloadedAssets(preloaded);
            foreach (var pair in bytes)
                if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) File.WriteAllBytes(pair.Key, pair.Value);
            if (!EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).SequenceEqual(scenes.Select(s => s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            AssetDatabase.Refresh();
            File.AppendAllText(Report, "RESTORED generated caches and scene setup; backup " + backup + "\n");
            if (File.Exists(Output)) File.Copy(Report, Path.GetDirectoryName(Output) + "/BuildReport.txt", true);
        }
    }
}
