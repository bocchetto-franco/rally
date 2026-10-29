using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

/// <summary>Owner-specific turbo inventory. Water warnings intentionally removed.</summary>
public sealed class RallyRaceAssistHud : MonoBehaviour
{
    [SerializeField, Min(.02f)] float refreshInterval = .1f;
    JrsVehicleController car;
    RallyVehicleTurbo turbo;
    RallyLocalPlayerInput input;
    TMP_Text turboText;
    Image turboPanel;
    float nextRefresh;
    string cachedControl;
    int displayedTurboState = -1;
    // Compatibility for editor diagnostics; no water warning is created or updated.
    public bool WaterWarningVisible => false;
    public float WarningDistance => 0f;
    public RallyWaterSurface WarnedPuddle => null;

    public void Configure(Rigidbody owner, TMP_FontAsset font)
    {
        car = owner != null ? owner.GetComponent<JrsVehicleController>() : null;
        turbo = owner != null ? (owner.GetComponent<RallyVehicleTurbo>() ?? owner.gameObject.AddComponent<RallyVehicleTurbo>()) : null;
        input = owner != null ? owner.GetComponent<RallyLocalPlayerInput>() : null;
        if (turboText == null)
        {
            turboText = Panel("Turbo inventory", font, new Vector2(.5f, 0f), new Vector2(0, 18), new Vector2(540, 60), 27, out var panel);
            turboPanel = panel.GetComponent<Image>();
        }
        nextRefresh = 0; displayedTurboState=-1;cachedControl=null;
    }

    void Update()
    {
        if (car == null || turboText == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
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
