using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Release snapshot without overwriting a running older player.</summary>
public static class RallyStartLightsBuild
{
    const string Folder = "Build/Windows_StartLights_20261029";
    static void Capture(string text, string stack, LogType type)
    {
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception)
            File.AppendAllText(Folder + "/BuildLog.txt", type + ": " + text + "\n");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        if (File.Exists(Folder + "/Rally.exe")) throw new Exception("Preserve previous snapshot before rebuilding this folder.");
        File.WriteAllText(Folder + "/BuildLog.txt", "Rally Windows x64 Release — start lights\nStarted: " + DateTimeOffset.Now.ToString("O") + "\n");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var scenes = BuildWindows.Validate();
        for (int i = 0; i < 6; i++)
            if (!File.ReadAllText("Logs/start-lights-case-" + i + ".txt").Contains("COMPLETE: PASS"))
                throw new Exception("Incomplete start-lights check: " + i);
        RallyCreditsSetup.Sync();
        File.AppendAllText(Folder + "/BuildLog.txt", "Unity: " + Application.unityVersion + "\nProduct: " + PlayerSettings.productName +
            "\nVersion: " + PlayerSettings.bundleVersion + "\nInput: Both\nScenes:\n" + string.Join("\n", scenes) + "\n");
        Application.logMessageReceived += Capture;
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = Folder + "/Rally.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
            });
            var result = report.summary;
            File.AppendAllText(Folder + "/BuildLog.txt", "Finished: " + DateTimeOffset.Now.ToString("O") +
                "\nResult: " + result.result + "\nErrors: " + result.totalErrors + "\nWarnings: " + result.totalWarnings +
                "\nDuration: " + result.totalTime + "\nBytes: " + result.totalSize + "\n");
            if (result.result != BuildResult.Succeeded || result.totalErrors != 0) throw new Exception("Windows build failed.");
            File.WriteAllText(Folder + "/BuildInfo.txt", "Generated: " + DateTimeOffset.Now.ToString("O") +
                "\nUnity: " + Application.unityVersion + "\nVersion: " + PlayerSettings.bundleVersion +
                "\nEntry: MainMenu\nInput: Both\nStart lights: five reds, shared 1P/2P, slow-frame protection\nKeep Rally_Data and DLLs beside Rally.exe.\n");
        }
        finally { Application.logMessageReceived -= Capture; }
    }
}
