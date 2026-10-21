using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RallyWindowsVerification
{
    public static void GenerateRelease()
    {
        if (!File.ReadAllText("Logs/graphics-atmosphere-play.txt").Contains("COMPLETE: PASS"))
            throw new Exception("Finish graphics Play verification before building");
        if (!File.ReadAllText("Logs/windows-preparation-verification.txt").Contains("COMPLETE: PASS"))
            throw new Exception("Finish saved-project verification before building");
        BuildWindows.BuildRelease();
    }
    const string Report = "Logs/windows-preparation-verification.txt";
    static void Check(bool pass, string message)
    {
        File.AppendAllText(Report, (pass ? "PASS: " : "FAIL: ") + message + "\n");
        if (!pass) throw new Exception(message);
    }
    public static void Verify()
    {
        File.WriteAllText(Report, "Windows preparation " + DateTimeOffset.Now.ToString("O") + "\n");
        var scenes = BuildWindows.Validate();
        Check(scenes[0] == "Assets/Scenes/MainMenu.unity", "saved MainMenu is first");
        File.AppendAllText(Report, string.Join("\n", scenes) + "\n");
        foreach (string path in new[] {
            "Assets/Scripts/RallyVehicleExhaust.cs", "Assets/Scripts/RallyCameraRain.cs", "Assets/Scripts/RallyCircuitAtmosphere.cs",
            "Assets/Scripts/RallyMenuVehiclePreview.cs", "Assets/Scripts/RallySplitScreen.cs", "Assets/Scripts/RallyMenuNavigation.cs",
            "Assets/Scripts/RallyPauseMenu.cs", "Assets/Scripts/RallyRaceHud.cs", "Assets/Editor/BuildWindows.cs" })
            Check(File.Exists(path) && File.Exists(path + ".meta"), "source + meta saved: " + path);
        Check(File.ReadAllBytes("CREDITS.md").SequenceEqual(File.ReadAllBytes("Assets/Resources/Credits/Credits.txt")) &&
            File.ReadAllBytes("CREDITS.md").SequenceEqual(File.ReadAllBytes("Assets/StreamingAssets/CREDITS.md")), "credits screen/distributed notices match canonical CREDITS.md");
        foreach (string model in new[] { "LanciaS4", "Peugeot205T16", "AudiQuattro" })
        {
            var race = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/" + model + ".prefab");
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MenuVehicles/" + model + ".prefab");
            Check(race != null && preview != null && race.GetComponent<RallyVehicleVisual>().hasExhaustAnchor, "race model, preview and measured exhaust anchor: " + model);
        }
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        Check(settings.FindProperty("m_FogStripping").intValue == 1 && settings.FindProperty("m_FogKeepLinear").boolValue, "runtime Linear fog shader variant retained for Windows");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "Circuit_01", "Circuit_02", "Circuit_03" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var profiles = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RallyCircuitAtmosphere>(true)).ToArray();
                if (name == "Circuit_02") Check(profiles.Length == 0, "Sierras climate unchanged");
                else
                {
                    Check(profiles.Length == 1 && profiles[0].profile != null && profiles[0].enabled, "saved atmosphere root/reference: " + name);
                    var p = profiles[0].profile;
                    Check(p.skybox != null && p.sunIntensity > 0 && p.fogEnd > p.fogStart, "sky/sun/fog configured: " + name);
                    if (name == "Circuit_03") Check(p.rain && p.rainMaterial != null && p.wetRoadMaterial != null && p.originalRoadMaterial != null, "rain and wet road references saved in forest profile");
                    else Check(!p.rain && p.sunEuler.x < 20, "separate warm sunset profile saved in desert");
                }
                Check(!scene.isDirty, "scene read directly from disk, no unsaved changes: " + name);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        File.AppendAllText(Report, "COMPLETE: PASS\n");
    }
}
