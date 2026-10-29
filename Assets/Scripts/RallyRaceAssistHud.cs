using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

/// <summary>Owner-specific turbo inventory and route-aware water pace note.</summary>
public sealed class RallyRaceAssistHud : MonoBehaviour
{
    [SerializeField, Min(10f)] float waterWarningDistance = 65f;
    [SerializeField, Min(.02f)] float refreshInterval = .1f;
    Rigidbody body;
    JrsVehicleController car;
    RallyVehicleTurbo turbo;
    RallyLocalPlayerInput input;
    TMP_Text turboText, waterText;
    Image turboPanel;
    GameObject waterPanel;
    Vector3[] points;
    float[] distances, puddleStations;
    float routeLength, nextRefresh;
    RallyWaterSurface[] puddles;
    string cachedControl;
    int displayedTurboState=-1;
    CanvasGroup waterGroup;
    float waterAppearance;
    Vector3 waterBaseScale;
    [SerializeField, Min(.1f)] float appearanceDuration = .32f;
    [SerializeField, Min(1f)] float waterTargetSpeedKph = 35f;
    public bool WaterWarningVisible => waterPanel != null && waterPanel.activeSelf;
    public float WarningDistance => waterWarningDistance;
    public RallyWaterSurface WarnedPuddle { get; private set; }

    public void Configure(Rigidbody owner, TMP_FontAsset font)
    {
        body = owner;
        car = owner != null ? owner.GetComponent<JrsVehicleController>() : null;
        turbo = owner != null ? (owner.GetComponent<RallyVehicleTurbo>() ?? owner.gameObject.AddComponent<RallyVehicleTurbo>()) : null;
        input = owner != null ? owner.GetComponent<RallyLocalPlayerInput>() : null;
        if (turboText == null)
        {
            turboText = Panel("Turbo inventory", font, new Vector2(.5f, 0f), new Vector2(0, 18), new Vector2(540, 60), 27, out var panel);
            turboPanel = panel.GetComponent<Image>();
            waterText = Panel("Water pace note", font, new Vector2(.5f, 1f), new Vector2(0, -216), new Vector2(430, 176), 36, out waterPanel);
            waterPanel.GetComponent<Image>().color = new Color(.045f, .055f, .065f, .94f);
            waterGroup = waterPanel.GetComponent<CanvasGroup>();
            if(waterGroup == null) waterGroup = waterPanel.AddComponent<CanvasGroup>();
            waterGroup.blocksRaycasts = waterGroup.interactable = false;
            Color cyan = new Color(.25f,.88f,1f);
            waterText.font = Resources.Load<TMP_FontAsset>("MenuFonts/Oswald") ?? waterText.font;
            waterText.text = "!"; waterText.fontSize = 100; waterText.fontStyle = FontStyles.Bold; waterText.color = cyan;
            Place(waterText.rectTransform, new Vector2(.52f,.26f),new Vector2(.91f,.97f));
            var icon = new GameObject("Water Arrow",typeof(RectTransform),typeof(RallyPaceNoteArrow)).GetComponent<RallyPaceNoteArrow>();
            icon.transform.SetParent(waterPanel.transform,false);
            Place(icon.rectTransform,new Vector2(.08f,.22f),new Vector2(.49f,.91f));
            icon.SetNote(RallyBrakeWarningTrigger.TurnDirection.Right,3,false);icon.color=cyan;icon.raycastTarget=false;
            var accent = new GameObject("Note accent",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            accent.transform.SetParent(waterPanel.transform,false);Place(accent.rectTransform,Vector2.zero,new Vector2(.018f,1));accent.color=cyan;accent.raycastTarget=false;
            var caption=new GameObject("Water Label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            caption.transform.SetParent(waterPanel.transform,false);Place(caption.rectTransform,new Vector2(.06f,.04f),new Vector2(.94f,.25f));
            caption.font=waterText.font;caption.text="POZO / AGUA";caption.fontSize=25;caption.fontStyle=FontStyles.Bold;caption.alignment=TextAlignmentOptions.Center;caption.raycastTarget=false;
            waterBaseScale=waterPanel.transform.localScale;
            waterPanel.SetActive(false);
        }
        Transform route = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "AI_Waypoints") route = root.transform;
        }
        puddles = RallyWaterSurface.Read(gameObject.scene);
        if (route != null && route.childCount > 2)
        {
            points = new Vector3[route.childCount]; distances = new float[points.Length+1];
            for(int i=0;i<points.Length;i++) points[i] = route.GetChild(i).position;
            for(int i=0;i<points.Length;i++) distances[i+1] = distances[i] + Vector3.Distance(points[i], points[(i+1)%points.Length]);
            routeLength = distances[points.Length];
            puddleStations = new float[puddles.Length];
            for(int i=0;i<puddles.Length;i++)
            {
                puddleStations[i] = Mathf.Repeat(Station(puddles[i].Center)-puddles[i].Radius, routeLength);
            }
        }
        nextRefresh = 0; displayedTurboState=-1;cachedControl=null;
    }

    float Station(Vector3 position)
    {
        float best = float.PositiveInfinity, station = 0;
        for(int i=0;i<points.Length;i++)
        {
            Vector3 leg = points[(i+1)%points.Length]-points[i];
            float t = Mathf.Clamp01(Vector3.Dot(position-points[i],leg)/Mathf.Max(.001f,leg.sqrMagnitude));
            float error = (position-points[i]-leg*t).sqrMagnitude;
            if(error < best) {best=error;station=Mathf.Lerp(distances[i],distances[i+1],t);}
        }
        return station;
    }

    void Update()
    {
        if (body == null || turboText == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        bool active = car != null && car.isActiveAndEnabled && Time.timeScale > 0 && !RallyRaceLoading.IsPreparing;
        Gamepad pad = input != null ? input.AssignedGamepad : Gamepad.current;
        string key = input != null && input.Device == RallyLocalPlayerInput.InputDevice.Keyboard
            ? (RallyLocalKeyboard.Second(input.PlayerIndex) ? "CTRL DER." : "SHIFT")
            : pad is DualShockGamepad ? "TRIÁNGULO" : pad != null ? "Y" : "SHIFT";
        int state=turbo!=null && turbo.IsBoosting?2:turbo!=null && turbo.HasStoredTurbo?1:0;
        if(state != displayedTurboState || key != cachedControl)
        {
            string value=state==2?"TURBO ACTIVO":state==1?"TURBO LISTO · "+key:"TURBO · SIN CARGA";
            turboText.SetText(value); displayedTurboState=state;cachedControl=key;
            turboText.color = turbo != null && (turbo.HasStoredTurbo || turbo.IsBoosting) ? new Color(1,.85f,.18f) : new Color(.65f,.68f,.72f);
        }
        bool showTurbo = car != null && car.isActiveAndEnabled && !RallyRaceLoading.IsPreparing;
        if(turboPanel.gameObject.activeSelf != showTurbo) turboPanel.gameObject.SetActive(showTurbo);
        float nearest = float.PositiveInfinity;
        RallyWaterSurface upcoming = null;
        if(active && puddles != null)
        {
            float station = points != null ? Station(body.position) : 0;
            for(int i=0;i<puddles.Length;i++)
            {
                var puddle=puddles[i];
                if(!puddle.Active || puddle.Contains(body)) continue;
                float distance;
                if(points != null) distance=Mathf.Repeat(puddleStations[i]-station,routeLength);
                else {Vector3 delta=puddle.Center-body.position;if(Vector3.Dot(delta,body.transform.forward)<=0)continue;distance=delta.magnitude;}
                if(distance < nearest) {nearest=distance;upcoming=puddle;}
            }
        }
        bool visible = nearest <= waterWarningDistance;
        var next=visible?upcoming:null;
        if(next!=WarnedPuddle || !visible)waterAppearance=0;
        WarnedPuddle=next;
        if(waterPanel.activeSelf != visible) waterPanel.SetActive(visible);
    }

    void LateUpdate()
    {
        if(waterPanel==null || !waterPanel.activeSelf)return;
        if(Time.timeScale<=0 || RallyRaceLoading.IsPreparing){waterPanel.SetActive(false);waterAppearance=0;return;}
        waterAppearance=Mathf.Min(waterAppearance+Time.deltaTime,appearanceDuration);
        waterPanel.transform.localScale=waterBaseScale*RallyPaceNotePresentation.Scale(waterAppearance,appearanceDuration);
        waterGroup.alpha=RallyPaceNotePresentation.Opacity(body.linearVelocity.magnitude*3.6f,waterTargetSpeedKph,30f);
    }
    static void Place(RectTransform rect,Vector2 min,Vector2 max)
    {
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
    }

    TMP_Text Panel(string name, TMP_FontAsset font, Vector2 anchor, Vector2 offset, Vector2 size, float fontSize, out GameObject panel)
    {
        Transform parent=transform.Find("Player Viewport") ?? transform;
        bool split=parent!=transform;
        var existing=parent.Find(name);
        panel=existing!=null?existing.gameObject:new GameObject(name,typeof(RectTransform),typeof(Image));
        var rect=panel.GetComponent<RectTransform>();rect.SetParent(parent,false);
        // FitCanvas runs before this late-created HUD. Match its per-player
        // scaling, and leave room for the player label and recovery button.
        if(split && anchor.y==0f)offset.y=165f;
        rect.anchorMin=rect.anchorMax=rect.pivot=anchor;
        rect.anchoredPosition=offset*(split?.7f:1f);rect.sizeDelta=size;rect.localScale=Vector3.one*(split?.7f:1f);
        panel.GetComponent<Image>().color=new Color(.035f,.045f,.06f,.88f);panel.GetComponent<Image>().raycastTarget=false;
        var label=panel.GetComponentInChildren<TextMeshProUGUI>(true);
        if(label==null)label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(rect,false);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(10,5);label.rectTransform.offsetMax=new Vector2(-10,-5);
        label.font=font!=null?font:TMP_Settings.defaultFontAsset;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        return label;
    }
}
