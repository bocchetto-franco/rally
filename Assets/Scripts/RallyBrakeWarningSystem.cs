using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RallyBrakeWarningSystem : MonoBehaviour
{
    [SerializeField] Rigidbody vehicleBody;
    [SerializeField, Min(1f)] float fullIntensitySpeedExcessKph = 30f;

    CanvasGroup warningGroup;
    TMP_Text warningText;
    RallyBrakeWarningTrigger activeTrigger;

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
        warningText.text = $"<size=70%>▲</size>  BRAKE\n<size=38%>CURVA {Mathf.RoundToInt(activeTrigger.TargetSpeedKph)} km/h</size>";
    }

    public void Enter(RallyBrakeWarningTrigger trigger)
    {
        if (trigger != null) activeTrigger = trigger;
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
        if (warningGroup != null) warningGroup.alpha = 0f;
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

        GameObject panel = new GameObject("Brake Warning", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -105f);
        rect.sizeDelta = new Vector2(430f, 145f);
        panel.GetComponent<Image>().color = new Color(.12f, .015f, .005f, .88f);
        warningGroup = panel.GetComponent<CanvasGroup>();

        GameObject label = new GameObject("Brake Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);
        warningText = label.GetComponent<TextMeshProUGUI>();
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.fontSize = 42f;
        warningText.fontStyle = FontStyles.Bold;
        warningText.color = new Color(1f, .72f, .08f, 1f);
        warningText.enableWordWrapping = false;
    }
}
