using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RallyBrakeWarningSystem : MonoBehaviour
{
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField, Min(1f)] float fullIntensitySpeedExcessKph = 30f;
    [SerializeField, Min(.1f)] float appearanceDuration = .32f;

    readonly HashSet<RallyBrakeWarningTrigger> zones = new HashSet<RallyBrakeWarningTrigger>();
    CanvasGroup warningGroup;
    RectTransform warningRect;
    TMP_Text warningText, directionText;
    RallyPaceNoteArrow arrow;
    Image accent;
    RallyBrakeWarningTrigger activeTrigger, displayedTrigger;
    string displayedLabel;
    bool displayedHairpin;
    float appearanceTime;
    bool wasVisible;

    public Rigidbody VehicleBody => vehicleBody;
    public RallyBrakeWarningTrigger ActiveNote => activeTrigger;
    public float NoteOpacity => warningGroup == null ? 0f : warningGroup.alpha;

    public void Configure(Rigidbody body) => vehicleBody = body;

    void Awake()
    {
        if (vehicleBody == null)
        {
            RallyVehicleDynamics dynamics = FindAnyObjectByType<RallyVehicleDynamics>();
            if (dynamics != null) vehicleBody = dynamics.GetComponentInParent<Rigidbody>();
        }
        BuildHud();
        HideImmediate();
    }

    void Update()
    {
        SelectUpcoming();
        if (activeTrigger == null || vehicleBody == null || Time.timeScale == 0f)
        {
            HideImmediate();
            return;
        }
        if (displayedTrigger == null || displayedTrigger.transform.parent != activeTrigger.transform.parent ||
            displayedLabel != activeTrigger.NoteLabel || displayedHairpin != activeTrigger.IsHairpin)
        {
            displayedTrigger = activeTrigger;
            displayedLabel = activeTrigger.NoteLabel;
            displayedHairpin = activeTrigger.IsHairpin;
            warningText.text = activeTrigger.CornerGrade.ToString();
            directionText.text = activeTrigger.Direction == RallyBrakeWarningTrigger.TurnDirection.Right ? "DERECHA" : "IZQUIERDA";
            if (activeTrigger.IsHairpin) directionText.text += " · HORQUILLA";
            Color color = activeTrigger.CornerGrade <= 2 ? new Color(1f, .48f, .13f) :
                activeTrigger.CornerGrade <= 4 ? new Color(1f, .83f, .2f) : new Color(.42f, .93f, .57f);
            arrow.SetNote(activeTrigger.Direction, activeTrigger.CornerGrade, activeTrigger.IsHairpin);
            arrow.color = warningText.color = accent.color = color;
            wasVisible = false;
        }
        float speedKph = vehicleBody.linearVelocity.magnitude * 3.6f;
        float excess = Mathf.Clamp01((speedKph - activeTrigger.TargetSpeedKph) / fullIntensitySpeedExcessKph);
        // A pace note remains readable after braking to the recommended speed.
        warningGroup.alpha = Mathf.Lerp(.78f, 1f, Mathf.SmoothStep(0f, 1f, excess));
        if (!wasVisible) appearanceTime = 0f;
        wasVisible = true;
        appearanceTime = Mathf.Min(appearanceTime + Time.deltaTime, appearanceDuration);
        float progress = Mathf.Clamp01(appearanceTime / Mathf.Max(.1f, appearanceDuration));
        float scale = progress < .7f
            ? Mathf.Lerp(.84f, 1.04f, Mathf.SmoothStep(0f, 1f, progress / .7f))
            : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (progress - .7f) / .3f));
        warningRect.localScale = Vector3.one * scale;
    }

    public void Enter(RallyBrakeWarningTrigger trigger)
    {
        if (trigger != null) zones.Add(trigger);
        SelectUpcoming();
    }

    public void Exit(RallyBrakeWarningTrigger trigger)
    {
        zones.Remove(trigger);
        SelectUpcoming();
        if (activeTrigger == null) HideImmediate();
    }

    void SelectUpcoming()
    {
        RallyBrakeWarningTrigger next = null;
        float nearest = float.PositiveInfinity;
        foreach (var zone in zones)
        {
            if (zone == null || !zone.isActiveAndEnabled) continue;
            float distance = vehicleBody == null ? 0f : (zone.CurveEntry - vehicleBody.position).sqrMagnitude;
            if (distance < nearest) { nearest = distance; next = zone; }
        }
        activeTrigger = next;
    }

    void HideImmediate()
    {
        if (warningGroup != null && warningGroup.alpha != 0f) warningGroup.alpha = 0f;
        if (warningRect != null && warningRect.localScale != Vector3.one) warningRect.localScale = Vector3.one;
        wasVisible = false;
        appearanceTime = 0f;
    }

    void OnDisable() { zones.Clear(); activeTrigger = displayedTrigger = null; HideImmediate(); }

    void BuildHud()
    {
        var canvasObject = new GameObject("Brake Warning Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Brake Warning", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        warningRect = panel.GetComponent<RectTransform>();
        warningRect.anchorMin = warningRect.anchorMax = new Vector2(.5f, 1f);
        warningRect.pivot = new Vector2(.5f, .5f);
        warningRect.anchoredPosition = new Vector2(0f, -270f);
        warningRect.sizeDelta = new Vector2(430f, 176f);
        panel.GetComponent<Image>().color = new Color(.045f, .055f, .065f, .94f);
        panel.GetComponent<Image>().raycastTarget = false;
        warningGroup = panel.GetComponent<CanvasGroup>();
        warningGroup.blocksRaycasts = warningGroup.interactable = false;

        var strip = MakeRect("Note accent", panel.transform, new Vector2(0f, 0f), new Vector2(.018f, 1f));
        accent = strip.gameObject.AddComponent<Image>(); accent.raycastTarget = false;
        var icon = MakeRect("Curve Arrow", panel.transform, new Vector2(.08f, .22f), new Vector2(.49f, .91f));
        arrow = icon.gameObject.AddComponent<RallyPaceNoteArrow>(); arrow.raycastTarget = false;
        warningText = Label("Corner Grade", panel.transform, new Vector2(.52f, .26f), new Vector2(.91f, .97f), 100f);
        directionText = Label("Corner Direction", panel.transform, new Vector2(.06f, .04f), new Vector2(.94f, .25f), 25f);
        directionText.color = new Color(.95f, .96f, .98f);
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, float size)
    {
        var label = MakeRect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("MenuFonts/Oswald") ?? TMP_Settings.defaultFontAsset;
        label.alignment = TextAlignmentOptions.Center; label.fontSize = size;
        label.fontStyle = FontStyles.Bold; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
        return label;
    }
}
