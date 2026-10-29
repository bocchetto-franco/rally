using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

/// <summary>Read-only, owner-specific driving guide beside the minimap.</summary>
[DisallowMultipleComponent]
public sealed class RallyDrivingControlsHud : MonoBehaviour
{
    Rigidbody owner;
    JrsVehicleController car;
    RallyLocalPlayerInput input;
    RectTransform panel;
    TMP_Text leftTrigger, rightTrigger, turboLetter, driftLetter;
    RallyDrivingControlGlyph turboGlyph, driftGlyph;
    Gamepad displayedPad;
    bool keyboardLastUsed, styleReady;
    public RectTransform Panel => panel;
    public Rigidbody Owner => owner;
    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    public void Configure(Rigidbody body, RectTransform map, TMP_FontAsset font)
    {
        if (panel != null || body == null || map == null) return;
        owner = body;
        car = body.GetComponent<JrsVehicleController>();
        input = body.GetComponent<RallyLocalPlayerInput>();
        bool split = RallyGameSession.LocalPlayerCount == 2;
        float width = split ? 274 : 292;
        panel = Rect(map.parent, "Driving Controls", new Vector2(width, split ? 116 : 124));
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = map.anchoredPosition + new Vector2(map.sizeDelta.x + 10, 0);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(.035f, .045f, .055f, .68f);
        background.raycastTarget = false;
        Label("Guide Title", "CONTROLES", font, 12, new Vector2(12, -15), width - 24);
        float first = split ? -37 : -40, step = split ? 30 : 32;
        leftTrigger = Symbol("Brake Trigger", RallyDrivingControlGlyph.Shape.Trigger, font, new Vector2(28, first));
        rightTrigger = Symbol("Throttle Trigger", RallyDrivingControlGlyph.Shape.Trigger, font, new Vector2(68, first));
        turboLetter = Symbol("Turbo Button", RallyDrivingControlGlyph.Shape.Triangle, font, new Vector2(48, first - step));
        turboGlyph = turboLetter.transform.parent.GetComponent<RallyDrivingControlGlyph>();
        driftLetter = Symbol("Drift Button", RallyDrivingControlGlyph.Shape.Square, font, new Vector2(48, first - step * 2));
        driftGlyph = driftLetter.transform.parent.GetComponent<RallyDrivingControlGlyph>();
        float size = split ? 17 : 19;
        Label("Brake Throttle Label", "Frenar / Acelerar", font, size, new Vector2(98, first), width - 108);
        Label("Turbo Label", "Turbo", font, size, new Vector2(98, first - step), width - 108);
        Label("Drift Label", "Derrapar", font, size, new Vector2(98, first - step * 2), width - 108);
        RefreshPresentation();
    }

    void Update() => RefreshPresentation();
    void OnDisable() { if (panel != null) panel.gameObject.SetActive(false); }
    void OnEnable() => RefreshPresentation();

    public void RefreshPresentation()
    {
        if (panel == null || owner == null) return;
        // Never consult Gamepad.current for a paired local player.
        Gamepad pad = input != null ? input.AssignedGamepad : Gamepad.current;
        if (pad != displayedPad) keyboardLastUsed = false;
        bool keyboard = input != null
            ? input.Device == RallyLocalPlayerInput.InputDevice.Keyboard : pad == null;
        if (input == null && pad != null)
        {
            // Single-player accepts keyboard and pad together. Observe only;
            // do not consume button edges used by turbo/recovery/gameplay.
            if (RallyLocalKeyboard.Accelerate(0) || RallyLocalKeyboard.Brake(0) ||
                RallyLocalKeyboard.Left(0) || RallyLocalKeyboard.Right(0) ||
                RallyLocalKeyboard.Handbrake(0) || RallyLocalKeyboard.Turbo(0))
                keyboardLastUsed = true;
            else if (pad.leftStick.ReadValue().sqrMagnitude > .09f ||
                     pad.leftTrigger.ReadValue() > .1f || pad.rightTrigger.ReadValue() > .1f ||
                     pad.buttonNorth.isPressed || pad.buttonWest.isPressed)
                keyboardLastUsed = false;
            keyboard |= keyboardLastUsed;
        }
        if (!styleReady || pad != displayedPad)
        {
            bool playStation = pad is DualShockGamepad;
            leftTrigger.SetText(playStation ? "L2" : "LT");
            rightTrigger.SetText(playStation ? "R2" : "RT");
            turboLetter.SetText(playStation ? "" : "Y");
            driftLetter.SetText(playStation ? "" : "X");
            turboGlyph.SetShape(playStation ? RallyDrivingControlGlyph.Shape.Triangle : RallyDrivingControlGlyph.Shape.Circle,
                playStation ? new Color(.35f, .9f, .8f) : new Color(1f, .82f, .3f));
            driftGlyph.SetShape(playStation ? RallyDrivingControlGlyph.Shape.Square : RallyDrivingControlGlyph.Shape.Circle,
                playStation ? new Color(.95f, .55f, .76f) : new Color(.4f, .7f, 1f));
            displayedPad = pad;
            styleReady = true;
        }
        bool visible = pad != null && pad.added && !keyboard && Time.timeScale > 0 &&
            !RallyRaceLoading.IsPreparing && (car == null || car.isActiveAndEnabled);
        if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
    }

    TMP_Text Symbol(string name, RallyDrivingControlGlyph.Shape shape, TMP_FontAsset font, Vector2 position)
    {
        var rect = Rect(panel, name, new Vector2(32, 27));
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        var glyph = rect.gameObject.AddComponent<RallyDrivingControlGlyph>();
        glyph.SetShape(shape, new Color(.75f, .79f, .83f));
        var label = Rect(rect, "Button Letter", new Vector2(30, 25)).gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = 16; label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center; label.color = new Color(.91f, .92f, .9f);
        label.raycastTarget = false;
        return label;
    }

    void Label(string name, string value, TMP_FontAsset font, float size, Vector2 position, float width)
    {
        var text = Rect(panel, name, new Vector2(width, 26)).gameObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
        text.rectTransform.pivot = new Vector2(0, .5f);
        text.rectTransform.anchoredPosition = position;
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.alignment = TextAlignmentOptions.Left;
        text.color = new Color(.87f, .89f, .87f); text.text = value; text.raycastTarget = false;
    }

    static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; return rect;
    }
}
