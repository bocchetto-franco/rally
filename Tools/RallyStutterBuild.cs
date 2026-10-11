using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyStutterBuild
{
    static string label;
    static readonly string[] Scenes = { "MainMenu", "VehicleCircuitSelection", "Circuit_01", "Circuit_02", "Circuit_03", "RaceResults" };
    static readonly string[] Generated = {
        "Assets/Resources/MenuFonts/Oswald.asset", "Assets/Resources/MenuFonts/Roboto.asset", "Assets/Settings/RallyHUD_Font.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset", "Assets/Settings/PC_RPAsset.asset",
        "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset", "ProjectSettings/UnityConnectSettings.asset"
    };
    public static string Baseline() => Queue("Baseline");
    public static string After() => Queue("After");
    public static void BaselineNow() => RunNow("Baseline");
    public static void AfterNow() => RunNow("After");
    public static void ReleaseNow() => RunNow("Release");
    public static void DiagnosticNow() => RunNow("Diagnostic");
    static void RunNow(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("RallyStutterBuild");
            var method = type?.GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null) EditorApplication.delayCall -= (EditorApplication.CallbackFunction)method.CreateDelegate(typeof(EditorApplication.CallbackFunction));
        }
        label = name;
        if (File.Exists("Build/Stutter_" + name + "_20261010/Rally1.exe")) throw new Exception("Build output already exists; preserve before rebuilding.");
        Build();
    }
    static string Queue(string name)
    {
        RequireIdle();
        label = name;
        string output = "Build/Stutter_" + name + "_20261010/Rally1.exe";
        if (File.Exists(output)) throw new Exception("Preserve previous output first: " + output);
        File.WriteAllText("Logs/stutter-build-" + label + ".txt", "QUEUED " + DateTime.Now.ToString("O") + "\n");
        EditorApplication.delayCall += Build;
        return "Queued " + output;
    }
    static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new Exception("Editor must be idle.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
    }
    static void Build()
    {
        RequireIdle();
        var assets = PlayerSettings.GetPreloadedAssets();
        string report = "Logs/stutter-build-" + label + ".txt";
        string backup = "Logs/SceneBackups/Stutter" + label + "Build_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        foreach (string path in Generated)
            if (File.Exists(path)) { string destination = backup + "/" + path; Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination); }
        try
        {
            File.AppendAllText(report, "START " + DateTime.Now.ToString("O") + "\n" +
                (label == "Release" ? "Release build; diagnostic instrumentation remains opt-in only. " : "Diagnostic Development build: same options baseline/after; instrumentation opt-in. ") +
                "Physics/scenes/quality unchanged.\n");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = Scenes.Select(s => "Assets/Scenes/" + s + ".unity").ToArray(), target = BuildTarget.StandaloneWindows64,
                locationPathName = "Build/Stutter_" + label + "_20261010/Rally1.exe",
                options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport |
                    (label == "Release" ? BuildOptions.None : BuildOptions.Development)
            });
            foreach (var step in result.steps)
                foreach (var message in step.messages)
                    if (message.type != LogType.Log) File.AppendAllText(report, message.type + ": " + message.content + "\n");
            File.AppendAllText(report, "Result=" + result.summary.result + "; Errors=" + result.summary.totalErrors + "; Warnings=" + result.summary.totalWarnings +
                "; Duration=" + result.summary.totalTime + "; Bytes=" + result.summary.totalSize + "\n" +
                (result.summary.result == BuildResult.Succeeded ? "COMPLETE: PASS\n" : "COMPLETE: FAIL\n"));
        }
        catch (Exception error) { File.AppendAllText(report, "FAIL: " + error + "\n"); }
        finally
        {
            // Restore only known generated caches/fonts; project locked against concurrent writes during this build.
            PlayerSettings.SetPreloadedAssets(assets);
            foreach (string path in Generated)
                if (File.Exists(backup + "/" + path)) File.Copy(backup + "/" + path, path, true);
            AssetDatabase.Refresh();
            File.AppendAllText(report, "Generated assets restored from " + backup + "\n");
        }
    }
}
