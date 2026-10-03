using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RallyBrakeWarningSystem : MonoBehaviour
{
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField, Min(1f)] float fullIntensitySpeedExcessKph = 30f;
    [SerializeField, Min(.1f)] float appearanceDuration = .32f;

    CanvasGroup warningGroup;
    RectTransform warningRect;
    TMP_Text warningText;
    RallyBrakeWarningTrigger activeTrigger;
    float appearanceTime;
    bool wasVisible;
    int displayedTargetSpeed = -1;

    public Rigidbody VehicleBody => vehicleBody;

    public void Configure(Rigidbody body)
    {
        vehicleBody = body;
    }

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
        if (activeTrigger == null || vehicleBody == null)
        {
            HideImmediate();
            return;
        }

        float speedKph = vehicleBody.linearVelocity.magnitude * 3.6f;
        float excess = speedKph - activeTrigger.TargetSpeedKph;
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(excess / fullIntensitySpeedExcessKph));
        warningGroup.alpha = alpha;
        warningGroup.blocksRaycasts = false;
        warningGroup.interactable = false;
        int targetSpeed = Mathf.RoundToInt(activeTrigger.TargetSpeedKph);
        if (targetSpeed != displayedTargetSpeed)
        {
            warningText.text = $"<size=70%>▲</size>  BRAKE\n<size=38%>CURVA {targetSpeed} km/h</size>";
            displayedTargetSpeed = targetSpeed;
        }

        // Animate size only: opacity remains exactly the speed-based warning value.
        bool visible = alpha > 0f;
        if (visible && !wasVisible) appearanceTime = 0f;
        wasVisible = visible;
        if (!visible)
        {
            warningRect.localScale = Vector3.one;
            return;
        }
        appearanceTime = Mathf.Min(appearanceTime + Time.deltaTime, appearanceDuration);
        float progress = Mathf.Clamp01(appearanceTime / Mathf.Max(.1f, appearanceDuration));
        float scale = progress < .7f
            ? Mathf.Lerp(.78f, 1.08f, Mathf.SmoothStep(0f, 1f, progress / .7f))
            : Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0f, 1f, (progress - .7f) / .3f));
        warningRect.localScale = Vector3.one * scale;
    }

    public void Enter(RallyBrakeWarningTrigger trigger)
    {
        // OnTriggerStay calls Enter repeatedly; replay the pop only for a new zone.
        if (trigger != null && activeTrigger != trigger)
        {
            activeTrigger = trigger;
            wasVisible = false;
        }
    }

    public void Exit(RallyBrakeWarningTrigger trigger)
    {
        if (activeTrigger == trigger)
        {
            activeTrigger = null;
            HideImmediate();
        }
    }

    void HideImmediate()
    {
        if (warningGroup != null && warningGroup.alpha != 0f) warningGroup.alpha = 0f;
        if (warningRect != null && warningRect.localScale != Vector3.one) warningRect.localScale = Vector3.one;
        wasVisible = false;
        appearanceTime = 0f;
    }

    void BuildHud()
    {
        GameObject canvasObject = new GameObject("Brake Warning Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject panel = new GameObject("Brake Warning", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        warningRect = rect;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, -200f);
        rect.sizeDelta = new Vector2(620f, 190f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(.48f, .012f, .018f, .95f);
        background.raycastTarget = false;
        Outline border = panel.GetComponent<Outline>();
        border.effectColor = new Color(1f, .86f, .06f, 1f);
        border.effectDistance = new Vector2(4f, -4f);
        border.useGraphicAlpha = false;
        warningGroup = panel.GetComponent<CanvasGroup>();

        GameObject label = new GameObject("Brake Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(20f, 12f);
        labelRect.offsetMax = new Vector2(-20f, -12f);
        warningText = label.GetComponent<TextMeshProUGUI>();
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.fontSize = 66f;
        warningText.fontStyle = FontStyles.Bold;
        warningText.color = new Color(1f, .9f, .12f, 1f);
        warningText.raycastTarget = false;
        warningText.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
