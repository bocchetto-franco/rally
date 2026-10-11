using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// External editor utility: never compiled into the player. Run through Unity Pipeline.
public static class RallyWindowsTestBuild
{
    const string ReportPath = "Logs/windows-test-build.txt";
    const string Output = "Build/Windows_Test_2026-10-10/Rally1.exe";
    static readonly string[] Scenes = {
        "Assets/Scenes/MainMenu.unity", "Assets/Scenes/VehicleCircuitSelection.unity",
        "Assets/Scenes/Circuit_01.unity", "Assets/Scenes/Circuit_02.unity",
        "Assets/Scenes/Circuit_03.unity", "Assets/Scenes/RaceResults.unity"
    };
    static readonly List<string> Messages = new List<string>();

    public static object Prepare()
    {
        RequireIdle();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new Exception("Windows x64 build module is not installed.");
        foreach (string scene in Scenes)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new Exception("Missing scene: " + scene);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        if (renderer == null) throw new Exception("Missing PC renderer.");
        if (renderer.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion && !f.isActive))
            throw new Exception("Remove inactive SSAO from the PC renderer before building: URP 17.6 strips its resources but still calls Create().");
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        if (settings.FindProperty("activeInputHandler").intValue != 2) throw new Exception("Active Input Handling must remain Both.");
        // Preserve existing product/company/version and quality. Only configure a practical test window.
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        EditorBuildSettings.scenes = Scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
        AssetDatabase.SaveAssets();
        return new { product = PlayerSettings.productName, version = PlayerSettings.bundleVersion,
            company = PlayerSettings.companyName, width = PlayerSettings.defaultScreenWidth,
            height = PlayerSettings.defaultScreenHeight, mode = PlayerSettings.fullScreenMode.ToString(),
            backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString(),
            bothInput = true, scenes = Scenes, output = Path.GetFullPath(Output) };
    }

    public static string Start()
    {
        RequireIdle();
        if (File.Exists(Output)) throw new Exception("Output already exists. Preserve it before starting a new build: " + Output);
        Prepare();
        Directory.CreateDirectory("Logs");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(ReportPath, "Windows x64 test build\nQUEUED " + DateTime.Now.ToString("O") + "\n");
        EditorApplication.delayCall += Build;
        return "Queued build; report " + Path.GetFullPath(ReportPath);
    }

    static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            BuildPipeline.isBuildingPlayer || StaticOcclusionCulling.isRunning)
            throw new Exception($"Editor must be idle: Play={EditorApplication.isPlayingOrWillChangePlaymode}; compiling={EditorApplication.isCompiling}; build={BuildPipeline.isBuildingPlayer}; bake={StaticOcclusionCulling.isRunning}");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
    }

    static void Capture(string message, string stack, LogType type)
    {
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string item = type + ": " + message;
            Messages.Add(item);
            File.AppendAllText(ReportPath, item + "\n");
        }
    }

    static void Build()
    {
        Messages.Clear();
        Application.logMessageReceived += Capture;
        try
        {
            RequireIdle();
            File.AppendAllText(ReportPath, "START " + DateTime.Now.ToString("O") + "\n" +
                "Product=" + PlayerSettings.productName + "; version=" + PlayerSettings.bundleVersion +
                "; input=Both; resolution=1280x720 Windowed; development=false\n" +
                string.Join("\n", Scenes.Select((s, i) => "Scene " + i + ": " + s)) + "\n");
            // Regular Mono x64 player: no Development/ScriptDebugging overhead, useful for performance testing.
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = Scenes, locationPathName = Output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
            });
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Warning || message.type == LogType.Error || message.type == LogType.Exception)
                    {
                        string item = message.type + ": " + message.content;
                        if (!Messages.Contains(item)) { Messages.Add(item); File.AppendAllText(ReportPath, item + "\n"); }
                    }
            var summary = report.summary;
            File.AppendAllText(ReportPath, $"RESULT {summary.result}\nWarnings={summary.totalWarnings}; Errors={summary.totalErrors}; Bytes={summary.totalSize}; Duration={summary.totalTime}\nOutput={Path.GetFullPath(Output)}\n");
            if (summary.result != BuildResult.Succeeded || !File.Exists(Output)) throw new Exception("Build did not succeed.");
            File.AppendAllText(ReportPath, "COMPLETE: PASS\n");
        }
        catch (Exception e) { File.AppendAllText(ReportPath, "FAILED " + e + "\n"); Debug.LogException(e); }
        finally { Application.logMessageReceived -= Capture; }
    }
}
