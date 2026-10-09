using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises actual menu controls and preferences across two Play sessions; restores user state.</summary>
[InitializeOnLoad]
public static class RallyMenuPagesVerification
{
    const string State = "Rally.MenuPagesTest.", Report = "Logs/menu-pages-test.txt";
    static Gamepad pad;
    static Keyboard keyboard;
    static double next, deadline;
    static int phase;
    static readonly string[] Keys = { "Rally.Options.MusicaVolume", "Rally.Options.EfectosVolume", "Rally.Options.MotorVolume", "Rally.Options.Quality" };

    static RallyMenuPagesVerification() => EditorApplication.update += Tick;

    [MenuItem("Tools/Rally/Verify Controls and Options Menus")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Leave Play before testing menus.");
        for (int i = 0; i < Keys.Length; i++)
        {
            SessionState.SetBool(State + "Had" + i, PlayerPrefs.HasKey(Keys[i]));
            if (i == 3) SessionState.SetInt(State + "Value" + i, PlayerPrefs.GetInt(Keys[i]));
            else SessionState.SetFloat(State + "Value" + i, PlayerPrefs.GetFloat(Keys[i]));
        }
        SessionState.SetInt(State + "Quality", QualitySettings.GetQualityLevel());
        SessionState.SetString(State + "Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(State + "Background", Application.runInBackground);
        SessionState.SetInt(State + "Session", 1);
        SessionState.SetBool(State + "Active", true);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, "Controls/options integration test, keyboard + virtual gamepad + pointer\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(State + "Active", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            if (SessionState.GetBool(State + "Restart", false))
            {
                SessionState.SetBool(State + "Restart", false);
                SessionState.SetInt(State + "Session", 2);
                EditorApplication.isPlaying = true;
            }
            else Restore();
            return;
        }
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "MainMenu") return;
        double now = EditorApplication.timeSinceStartup;
        if (deadline == 0) { deadline = now + 100; next = now + 2; }
        if (now > deadline) { Finish(new Exception("Timed out at phase " + phase)); return; }
        if (now < next) return;
        next = now + .45;
        try
        {
            Application.runInBackground = true;
            if (SessionState.GetInt(State + "Session", 1) == 2)
            {
                CheckMix(0, SessionState.GetFloat(State + "Music", 0f));
                CheckMix(1, SessionState.GetFloat(State + "Effects", 0f));
                CheckMix(2, SessionState.GetFloat(State + "Engine", 0f));
                Require(QualitySettings.names[QualitySettings.GetQualityLevel()] == "Media", "Saved quality applied on next launch");
                Finish(null);
                return;
            }
            switch (phase++)
            {
                case 0:
                    pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
                    PressKey(Key.Enter); break;
                case 1: Release(); break;
                case 2:
                    Require(GameObject.Find("Controls Button") != null && GameObject.Find("Options Button") != null, "Main menu exposes both pages after intro");
                    Pad(GamepadButton.DpadDown); break;
                case 3: Release(); break;
                case 4: Pad(GamepadButton.South); break;
                case 5:
                    Release();
                    Require(GameObject.Find("Keyboard Controls") != null && GameObject.Find("Gamepad Controls") != null, "D-Pad + Cross opens Controls");
                    Require(GameObject.Find("Keyboard Controls").GetComponentsInChildren<TMP_Text>().Length == 19, "Nine keyboard mappings displayed");
                    Require(GameObject.Find("Gamepad Controls").GetComponentsInChildren<TMP_Text>().Length == 19, "Nine gamepad mappings displayed");
                    ScreenCapture.CaptureScreenshot("Logs/Menu_Controls.png"); break;
                case 6: Pad(GamepadButton.East); break;
                case 7:
                    Release();
                    Require(GameObject.Find("Options Button") != null, "Circle returns to menu without replaying intro");
                    var button = GameObject.Find("Options Button").GetComponent<Button>();
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    break;
                case 8:
                    Require(GameObject.Find("Audio Options") != null, "Mouse click opens Options");
                    foreach (var volume in Object.FindObjectsByType<Slider>())
                        Require(volume.fillRect.sizeDelta == Vector2.zero && volume.fillRect.anchoredPosition == Vector2.zero,
                            "Slider fill stays within its track: " + volume.name);
                    SessionState.SetFloat(State + "InitialMusic", RallyUserSettings.Volume(0));
                    PressKey(Key.RightArrow); break;
                case 9:
                    Release();
                    Require(RallyUserSettings.Volume(0) > SessionState.GetFloat(State + "InitialMusic", 0f), "Keyboard right increases selected music slider");
                    PressKey(Key.DownArrow); break;
                case 10: Release(); break;
                case 11: Pad(GamepadButton.DpadRight); break;
                case 12:
                    Release(); CheckMix(1, RallyUserSettings.Volume(1)); Pad(GamepadButton.DpadDown); break;
                case 13: Release(); break;
                case 14: Pad(GamepadButton.DpadRight); break;
                case 15:
                    Release(); CheckMix(2, RallyUserSettings.Volume(2)); Pad(GamepadButton.DpadDown); break;
                case 16: Release(); break;
                case 17: Pad(GamepadButton.South); break;
                case 18:
                    Release(); Require(QualitySettings.names[QualitySettings.GetQualityLevel()] == "Baja", "Gamepad selects Baja quality");
                    Pad(GamepadButton.DpadRight); break;
                case 19: Release(); break;
                case 20: Pad(GamepadButton.South); break;
                case 21:
                    Release(); Require(QualitySettings.names[QualitySettings.GetQualityLevel()] == "Media", "Gamepad selects Media quality");
                    Pad(GamepadButton.DpadRight); break;
                case 22: Release(); break;
                case 23: Pad(GamepadButton.South); break;
                case 24:
                    Release(); Require(QualitySettings.names[QualitySettings.GetQualityLevel()] == "Alta", "Gamepad selects Alta quality");
                    var slider = GameObject.Find("MÚSICA Slider").GetComponent<Slider>();
                    var rect = slider.GetComponent<RectTransform>();
                    var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .15f, 0f, 0f)));
                    ExecuteEvents.Execute(slider.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = point }, ExecuteEvents.pointerDownHandler);
                    Require(slider.value > .5f, "Pointer adjusts music slider");
                    // Verify mute and restoration don't couple the independent channels.
                    RallyUserSettings.SetVolume(0, 0f); CheckMix(0, 0f);
                    slider.value = .12f;
                    CheckMix(0, .12f); CheckMix(1, RallyUserSettings.Volume(1)); CheckMix(2, RallyUserSettings.Volume(2));
                    GameObject.Find("Quality Media").GetComponent<Button>().onClick.Invoke();
                    SessionState.SetFloat(State + "Music", RallyUserSettings.Volume(0));
                    SessionState.SetFloat(State + "Effects", RallyUserSettings.Volume(1));
                    SessionState.SetFloat(State + "Engine", RallyUserSettings.Volume(2));
                    ScreenCapture.CaptureScreenshot("Logs/Menu_Options.png"); break;
                case 25: PressKey(Key.Escape); break;
                case 26:
                    Release(); Require(GameObject.Find("Controls Button") != null, "Escape returns from Options");
                    RallyUserSettings.Save();
                    SessionState.SetBool(State + "Restart", true);
                    EditorApplication.isPlaying = false; break;
            }
        }
        catch (Exception error) { Finish(error); }
    }

    static void Pad(GamepadButton button) => InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
    static void PressKey(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
    static void Release()
    {
        InputSystem.QueueStateEvent(pad, new GamepadState());
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }
    static void CheckMix(int channel, float linear)
    {
        var mixer = RallyAudioLibrary.Load().mixer;
        Require(mixer.GetFloat(RallyUserSettings.AudioParameters[channel], out float db) &&
            Mathf.Abs(db - (linear <= 0f ? -80f : 20f * Mathf.Log10(linear))) < .01f, "Mixer channel " + RallyUserSettings.AudioParameters[channel] + " follows saved slider/mute");
    }
    static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    static void Finish(Exception error)
    {
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        File.AppendAllText(Report, error == null ? "COMPLETE: PASS\n" : "FAIL: " + error + "\n");
        SessionState.SetBool(State + "Restart", false);
        EditorApplication.isPlaying = false;
    }
    static void Restore()
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!SessionState.GetBool(State + "Had" + i, false)) PlayerPrefs.DeleteKey(Keys[i]);
            else if (i == 3) PlayerPrefs.SetInt(Keys[i], SessionState.GetInt(State + "Value" + i, 0));
            else PlayerPrefs.SetFloat(Keys[i], SessionState.GetFloat(State + "Value" + i, 0f));
        }
        PlayerPrefs.Save();
        QualitySettings.SetQualityLevel(SessionState.GetInt(State + "Quality", 1));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(State + "Start", ""));
        Application.runInBackground = SessionState.GetBool(State + "Background", false);
        RallyUserSettings.ApplyAudio();
        SessionState.SetBool(State + "Active", false);
        phase = 0; next = deadline = 0;
    }
}
