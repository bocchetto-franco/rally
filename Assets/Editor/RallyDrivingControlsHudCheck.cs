using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated UI/device check. Does not load/save a circuit or drive cars.</summary>
public static class RallyDrivingControlsHudCheck
{
    const string Output = "Logs/DrivingControlsHud";
    static System.Text.StringBuilder report;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        report.AppendLine("PASS: " + message);
    }
    [MenuItem("Tools/Rally/Verify Driving Controls Guide")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first");
        Directory.CreateDirectory(Output);
        report = new System.Text.StringBuilder("Driving guide: isolated canvas/device checks; not hardware or a full lap.\n");
        bool had = PlayerPrefs.HasKey("Rally.LocalPlayers");
        int previous = PlayerPrefs.GetInt("Rally.LocalPlayers", 1);
        float oldScale = Time.timeScale;
        Gamepad oldPad = Gamepad.current;
        Gamepad ps = null, xbox = null;
        Keyboard keys = null;
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        RenderTexture target = null;
        try
        {
            Time.timeScale = 1;
            ps = InputSystem.AddDevice<DualSenseGamepadHID>();
            xbox = InputSystem.AddDevice<Gamepad>();
            keys = InputSystem.AddDevice<Keyboard>();
            var root = new GameObject("Driving Guide QA");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var camera = new GameObject("QA camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.cameraType = CameraType.Game;
            camera.orthographic = true; camera.orthographicSize = 540;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .1f, .13f);
            target = new RenderTexture(1920, 1080, 24); target.Create(); camera.targetTexture = target;
            var canvas = new GameObject("QA canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            var font = Resources.Load<TMP_FontAsset>("MenuFonts/Roboto") ?? TMP_Settings.defaultFontAsset;
            RallyGameSession.SelectLocalPlayers(1);
            var owner = new GameObject("QA owner").AddComponent<Rigidbody>();
            owner.transform.SetParent(root.transform); owner.isKinematic = true;
            var local = owner.gameObject.AddComponent<RallyLocalPlayerInput>();
            local.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, 82); local.PairGamepad(ps);
            var map = Map(canvas.transform, false);
            var guide = canvas.gameObject.AddComponent<RallyDrivingControlsHud>();
            guide.Configure(owner, map, font); guide.Configure(owner, map, font);
            Check(guide.IsVisible && guide.Owner == owner, "DualSense guide visible and bound to its owner");
            Check(canvas.transform.Cast<Transform>().Count(t => t.name == "Driving Controls") == 1, "repeat Configure does not duplicate panel");
            Check(Has(guide, "L2") && Has(guide, "R2") && !Has(guide, "Y"), "PlayStation trigger labels and vector button symbols");
            BoundsCheck(guide, map);
            Capture(camera, target, guide, "DualSense_1P.png");
            local.PairGamepad(xbox); guide.RefreshPresentation();
            Check(Has(guide, "LT") && Has(guide, "RT") && Has(guide, "Y") && Has(guide, "X"), "Xbox labels replace PlayStation symbols");
            Capture(camera, target, guide, "Xbox_1P.png");
            local.Configure(RallyLocalPlayerInput.InputDevice.Keyboard, 82); guide.RefreshPresentation();
            Check(!guide.IsVisible, "keyboard owner hides guide despite connected pads");
            local.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, 82); local.PairGamepad(ps); guide.RefreshPresentation();
            Time.timeScale = 0; guide.RefreshPresentation(); Check(!guide.IsVisible, "hidden in pause");
            Time.timeScale = 1; guide.RefreshPresentation(); Check(guide.IsVisible, "returns after pause");
            // Two independent viewport roots, each with its own input and guide.
            Object.DestroyImmediate(guide.Panel.gameObject); Object.DestroyImmediate(guide); Object.DestroyImmediate(map.gameObject);
            RallyGameSession.SelectLocalPlayers(2);
            var one = Region(canvas.transform, "J1", .5f, 1);
            var two = Region(canvas.transform, "J2", 0, .5f);
            var mapOne = Map(one, true); var mapTwo = Map(two, true);
            var guideOne = one.gameObject.AddComponent<RallyDrivingControlsHud>(); guideOne.Configure(owner, mapOne, font);
            var ownerTwo = new GameObject("QA owner 2").AddComponent<Rigidbody>(); ownerTwo.transform.SetParent(root.transform); ownerTwo.isKinematic = true;
            var inputTwo = ownerTwo.gameObject.AddComponent<RallyLocalPlayerInput>();
            inputTwo.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, 82); inputTwo.PairGamepad(xbox);
            var guideTwo = two.gameObject.AddComponent<RallyDrivingControlsHud>(); guideTwo.Configure(ownerTwo, mapTwo, font);
            xbox.MakeCurrent(); guideOne.RefreshPresentation(); guideTwo.RefreshPresentation();
            Check(Has(guideOne, "L2") && Has(guideTwo, "LT"), "Gamepad.current cannot steal either player's icon style");
            BoundsCheck(guideOne, mapOne); BoundsCheck(guideTwo, mapTwo);
            Capture(camera, target, guideOne, "MixedPads_2P.png");
            inputTwo.Configure(RallyLocalPlayerInput.InputDevice.Keyboard, 82); guideTwo.RefreshPresentation();
            Check(guideOne.IsVisible && !guideTwo.IsVisible, "J2 keyboard hides only J2 guide");
            InputSystem.RemoveDevice(ps); guideOne.RefreshPresentation(); Check(!guideOne.IsVisible, "disconnect hides owner guide"); ps = null;
            // Single-player fallback (no local input component) observes keyboard without consuming it.
            RallyGameSession.SelectLocalPlayers(1);
            Object.DestroyImmediate(local);
            var fallback = root.AddComponent<RallyDrivingControlsHud>(); fallback.Configure(owner, mapOne, font);
            xbox.MakeCurrent(); fallback.RefreshPresentation(); Check(fallback.IsVisible, "single-player global pad fallback");
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W)); InputSystem.Update(); fallback.RefreshPresentation();
            Check(!fallback.IsVisible && keys.wKey.isPressed, "keyboard use hides guide without consuming input");
            InputSystem.QueueStateEvent(keys, new KeyboardState()); InputSystem.Update();
            InputSystem.QueueStateEvent(xbox, new GamepadState { rightTrigger = .7f }); InputSystem.Update(); fallback.RefreshPresentation();
            Check(fallback.IsVisible && xbox.rightTrigger.ReadValue() > .6f, "pad use restores guide without consuming throttle");
            Check(owner.mass == 1 && ownerTwo.mass == 1 && owner.linearVelocity == Vector3.zero, "UI never changes Rigidbody tuning or velocity");
            report.AppendLine("COMPLETE: PASS");
        }
        catch (Exception ex) { report.AppendLine("FAIL: " + ex); throw; }
        finally
        {
            if (ps != null && ps.added) InputSystem.RemoveDevice(ps);
            if (xbox != null && xbox.added) InputSystem.RemoveDevice(xbox);
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            if (oldPad != null && oldPad.added) oldPad.MakeCurrent();
            RallyGameSession.SelectLocalPlayers(previous);
            if (!had) PlayerPrefs.DeleteKey("Rally.LocalPlayers");
            PlayerPrefs.Save();
            Time.timeScale = oldScale;
            EditorSceneManager.CloseScene(scene, true);
            if (originalScene.IsValid() && originalScene.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            File.WriteAllText(Output + "/check.txt", report.ToString());
        }
    }
    static bool Has(RallyDrivingControlsHud guide, string value) => guide.Panel.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == value);
    static RectTransform Region(Transform parent, string name, float bottom, float top)
    {
        var r = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = new Vector2(0, bottom); r.anchorMax = new Vector2(1, top); r.offsetMin = r.offsetMax = Vector2.zero; return r;
    }
    static RectTransform Map(Transform parent, bool split)
    {
        var r = new GameObject("Mini Map", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.anchoredPosition = split ? new Vector2(18, 16) : new Vector2(28, 28);
        r.sizeDelta = split ? new Vector2(186, 178) : new Vector2(246, 234);
        r.GetComponent<Image>().color = new Color(.035f, .045f, .055f, .78f); return r;
    }
    static void BoundsCheck(RallyDrivingControlsHud guide, RectTransform map)
    {
        Check(guide.Panel.parent == map.parent && guide.Panel.anchoredPosition.x >= map.anchoredPosition.x + map.sizeDelta.x + 9,
            "guide beside map in the same viewport, no overlap");
        Check(guide.Panel.anchoredPosition.x + guide.Panel.sizeDelta.x < 600 && guide.Panel.sizeDelta.y <= 124,
            "small bottom-left footprint, outside central turbo and upper pace notes");
        Check(guide.Panel.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "guide never intercepts pointer controls");
        Canvas.ForceUpdateCanvases();
        foreach (var t in guide.Panel.GetComponentsInChildren<TMP_Text>(true)) { t.ForceMeshUpdate(); Check(!t.isTextOverflowing, "text fits: " + t.name); }
    }
    static void Capture(Camera camera, RenderTexture target, RallyDrivingControlsHud guide, string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        Color32 sample = texture.GetPixel(0, 0);
        int changed = texture.GetPixels32().Count(p => Math.Abs(p.r - sample.r) + Math.Abs(p.g - sample.g) + Math.Abs(p.b - sample.b) > 24);
        Check(changed > 1000, "non-empty UI rendering: " + name);
        File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        RenderTexture.active = old; Object.DestroyImmediate(texture);
    }
}
