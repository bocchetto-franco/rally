using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// External ownership regression, outside Play. Temporary objects/devices only;
// no scene saves, PlayerPrefs writes, permanent bindings or InputSettings changes.
public static class RallyFinalMenuOwnershipCheck
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Report = "Logs/final-menu-ownership-check.txt";

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new Exception("Run outside Play after compilation.");
        var originalScene = SceneManager.GetActiveScene();
        bool originalDirty = originalScene.isDirty;
        var preview = EditorSceneManager.NewPreviewScene();
        var originalPads = Gamepad.all.ToArray();
        var enabledPads = originalPads.Where(p => p.enabled).ToArray();
        Gamepad originalCurrent = Gamepad.current;
        var slots = (Gamepad[])typeof(RallyLocalDevices).GetField("pads", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var previousSlots = (Gamepad[])slots.Clone();
        var playerCount = typeof(RallyGameSession).GetProperty("LocalPlayerCount");
        int originalPlayers = RallyGameSession.LocalPlayerCount;
        Gamepad first = null, second = null, replacement = null;
        GameObject sharedObject = null, firstObject = null, secondObject = null;
        Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "Shared and owned gamepad routing regression\n");
        try
        {
            foreach (var pad in enabledPads) InputSystem.DisableDevice(pad);
            first = InputSystem.AddDevice<Gamepad>(); second = InputSystem.AddDevice<Gamepad>();
            slots[0] = first; slots[1] = second;
            playerCount.SetValue(null, 2);
            var shared = Create("Temporary shared menu", preview, out sharedObject);
            var ownerOne = Create("Temporary J1 panel", preview, out firstObject); ownerOne.ConfigureForLocalPlayer(0);
            var ownerTwo = Create("Temporary J2 panel", preview, out secondObject); ownerTwo.ConfigureForLocalPlayer(1);
            first.MakeCurrent();
            Check(Pad(shared) == first, "shared menu accepts J1", Report);
            second.MakeCurrent();
            Check(Pad(shared) == second, "shared menu/results accepts J2 in 2P", Report);
            Check(Pad(ownerOne) == first && Pad(ownerTwo) == second, "local panels/owned pause retain exclusive pairing", Report);
            InputSystem.RemoveDevice(first);
            Check(Pad(shared) == second, "remaining J2 can use shared menu after unplugging J1", Report);
            Check(Pad(ownerOne) == null && Pad(ownerTwo) == second, "unplug does not steal J2 controller for J1", Report);
            Check(RallyLocalDevices.UsesKeyboard(0), "J1 keyboard fallback available after unplug", Report);
            replacement = InputSystem.AddDevice<Gamepad>(); replacement.MakeCurrent();
            Check(Pad(shared) == replacement, "replacement controller can navigate shared pages", Report);
            Check(Pad(ownerOne) == replacement && Pad(ownerTwo) == second, "replacement restores J1 without reassigning J2", Report);
            playerCount.SetValue(null, 1); second.MakeCurrent();
            Check(Pad(shared) == second, "single-player shared navigation still accepts current controller", Report);
            File.AppendAllText(Report, "COMPLETE: PASS (routing checks, not button navigation or hardware testing)\n");
        }
        finally
        {
            if (sharedObject != null) Object.DestroyImmediate(sharedObject);
            if (firstObject != null) Object.DestroyImmediate(firstObject);
            if (secondObject != null) Object.DestroyImmediate(secondObject);
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (var pad in new[] { first, second, replacement }) if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            foreach (var pad in enabledPads) if (pad.added) InputSystem.EnableDevice(pad);
            slots[0] = previousSlots[0]; slots[1] = previousSlots[1];
            playerCount.SetValue(null, originalPlayers);
            if (originalCurrent != null && originalCurrent.added) originalCurrent.MakeCurrent();
            File.AppendAllText(Report, "RESTORED: gamepads, device slots, local player count and temporary preview scene\n");
            if (originalScene.isDirty != originalDirty) throw new Exception("Unexpected original scene dirty-state change");
        }
    }

    static RallyMenuNavigation Create(string name, Scene scene, out GameObject go)
    {
        go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<RallyMenuNavigation>();
    }
    static Gamepad Pad(RallyMenuNavigation navigation) =>
        (Gamepad)typeof(RallyMenuNavigation).GetProperty("Pad", PrivateInstance).GetValue(navigation);
    static void Check(bool ok, string message, string report)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(report, "PASS: " + message + "\n");
    }
}
