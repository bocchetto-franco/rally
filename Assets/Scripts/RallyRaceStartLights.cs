using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One shared start gate per race, after loading and grid placement.</summary>
[DisallowMultipleComponent]
public sealed class RallyRaceStartLights : MonoBehaviour
{
    [SerializeField, Min(.1f)] float lightInterval = .8f;
    [SerializeField, Range(.5f, 1f)] float allRedHold = .7f;
    [SerializeField, Min(0f)] float greenDuration = 3f;
    static RallyRaceStartLights active;
    public static bool IsBlocking => active != null && !active.released;
    public bool Released => released;
    public int LitCount { get; private set; }
    public bool IsGreen => released && overlay != null;
    Image[] lights;
    TMP_Text caption;
    GameObject overlay;
    Sprite disc;
    Texture2D discTexture;
    bool begun, released, previousAudioPause;
    float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active = null;

    void Awake()
    {
        if (active != null && active != this) { Destroy(this); return; }
        active = this;
    }

    void Start()
    {
        // Normal loading calls BeginPrepared synchronously before releasing physics.
        // This also covers direct Play with the diagnostic no-load-prep flag.
        if (!RallyRaceLoading.IsPreparing) BeginPrepared();
    }

    public static void BeginPrepared()
    {
        if (active == null || active.begun) return;
        active.begun = true;
        active.previousAudioPause = AudioListener.pause;
        AudioListener.pause = true;
        Time.timeScale = 0f;
        active.BuildOverlay();
    }

    void Update()
    {
        if (!begun || overlay == null) return;
        elapsed += Time.unscaledDeltaTime;
        float greenAt = 5f * lightInterval + allRedHold;
        if (!released)
        {
            Time.timeScale = 0f;
            int count = Mathf.Clamp(Mathf.FloorToInt(elapsed / lightInterval), 0, 5);
            if (count != LitCount)
            {
                LitCount = count;
                for (int i = 0; i < 5; i++) lights[i].color = i < count ? new Color(1f, .035f, .025f) : new Color(.16f, .035f, .03f);
            }
            if (elapsed >= greenAt)
            {
                released = true;
                for (int i = 0; i < 5; i++) lights[i].color = new Color(.1f, 1f, .25f);
                caption.SetText("¡SALIDA!");
                AudioListener.pause = previousAudioPause;
                Time.timeScale = 1f;
            }
        }
        // Scaled time keeps the green display's three seconds intact during pause.
        else
        {
            greenElapsed += Time.deltaTime;
            if (greenElapsed >= greenDuration) { Destroy(overlay); overlay = null; }
        }
    }
    float greenElapsed;

    void BuildOverlay()
    {
        overlay = new GameObject("Shared Start Lights Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 800;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Start Light Housing", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(0, -24); rect.sizeDelta = new Vector2(560, 128);
        Image background = panel.GetComponent<Image>(); background.color = new Color(.015f, .02f, .03f, .92f); background.raycastTarget = false;
        // Tiny antialiased disc, allocated once at scene start; no external asset needed.
        const int size = 64;
        discTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        discTexture.name = "Start light disc";
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32));
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(31.5f - distance));
        }
        discTexture.SetPixels(pixels); discTexture.Apply(false, true);
        disc = Sprite.Create(discTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        lights = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("Start Light " + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rect, false);
            var light = go.GetComponent<Image>(); light.sprite = disc; light.raycastTarget = false;
            light.color = new Color(.16f, .035f, .03f);
            light.rectTransform.sizeDelta = new Vector2(68, 68);
            light.rectTransform.anchoredPosition = new Vector2((i - 2) * 94, 12);
            lights[i] = light;
        }
        var label = new GameObject("Start Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(rect, false); caption = label.GetComponent<TMP_Text>();
        caption.font = TMP_Settings.defaultFontAsset; caption.fontSize = 23; caption.fontStyle = FontStyles.Bold;
        caption.alignment = TextAlignmentOptions.Center; caption.raycastTarget = false;
        caption.rectTransform.sizeDelta = new Vector2(540, 32); caption.rectTransform.anchoredPosition = new Vector2(0, -40);
        caption.SetText("PREPARATE");
    }

    void OnDestroy()
    {
        if (active != this) return;
        if (begun && !released) { Time.timeScale = 1f; AudioListener.pause = previousAudioPause; }
        active = null;
        if (disc != null) Destroy(disc);
        if (discTexture != null) Destroy(discTexture);
    }
}
