using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.SceneManagement;

// Reuses the production integration fixture, but scopes this rerun to its
// one-controller/keyboard fallback. Run outside Assets via the Editor pipeline.
public static class RallySplitFallbackCheck
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly Type Fixture = typeof(RallySplitScreenVerification);
    static bool skipped, optionsEnabled;
    static EnterPlayModeOptions options;
    static FieldInfo Field(string name) => Fixture.GetField(name, Flags);
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new Exception("Unity must be idle for fallback check.");
        optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        options = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        skipped = false;
        EditorApplication.update += Tick;
        try { RallySplitScreenVerification.Run(); }
        catch { Restore(); throw; }
    }
    static void Tick()
    {
        if (!SessionState.GetBool("Rally.SplitTest.Active", false)) { Restore(); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        int phase = (int)Field("phase").GetValue(null);
        if (!skipped && phase == 20)
        {
            skipped = true;
            var second = (DualSenseGamepadHID)Field("pad").GetValue(null);
            InputSystem.RemoveDevice(second);
            Field("pad").SetValue(null, null);
            var first = (DualSenseGamepadHID)Field("padOne").GetValue(null);
            InputSystem.QueueStateEvent(first, new DualSenseHIDInputReport {
                leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128, buttons0 = 8
            });
            SceneManager.LoadScene(RallyGameSession.SelectionScene);
            Field("phase").SetValue(null, 43);
            Field("phaseAt").SetValue(null, EditorApplication.timeSinceStartup);
            Field("minimumFrame").SetValue(null, Time.frameCount + 3);
            File.AppendAllText("Logs/split-screen-test.txt", "SCOPE: focused one-gamepad/keyboard fallback rerun; full two-gamepad cases were run separately.\n");
        }
        else if (skipped && phase == 50)
            Fixture.GetMethod("Finish", Flags).Invoke(null, new object[] { null });
    }
    static void Restore()
    {
        EditorApplication.update -= Tick;
        EditorSettings.enterPlayModeOptionsEnabled = optionsEnabled;
        EditorSettings.enterPlayModeOptions = options;
        File.Copy("Logs/split-screen-test.txt", "Logs/split-fallback-test.txt", true);
    }
}
