using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Waypoint overview without a camera, RenderTexture or per-frame route regeneration.</summary>
[DisallowMultipleComponent]
public sealed class RallyMiniMap : MonoBehaviour
{
    struct Marker { public Rigidbody body; public RectTransform icon; }
    static readonly Color PlayerOneColor = new Color(1f, .58f, .12f);
    static readonly Color PlayerTwoColor = new Color(.15f, .85f, 1f);
    static readonly Color BotColor = new Color(1f, .32f, .38f);
    Marker[] markers;
    RectTransform mapArea, panel;
    Rigidbody player;
    Vector2 worldCenter;
    float projectionScale, nextUpdate;
    RallyMiniMapRoute routeGraphic;
    const float UpdateInterval = .05f;

    public Rigidbody Player => player;
    public RectTransform Panel => panel;
    public RallyMiniMapRoute Route => routeGraphic;
    public int MarkerCount => markers == null ? 0 : markers.Length;
    public Vector2 ProjectPosition(Vector3 position) =>
        new Vector2(position.x - worldCenter.x, position.z - worldCenter.y) * projectionScale;
    public RectTransform MarkerFor(Rigidbody body)
    {
        if (markers != null) foreach (var marker in markers) if (marker.body == body) return marker.icon;
        return null;
    }

    public void Configure(Rigidbody owner, TMP_FontAsset font)
    {
        if (panel != null) return;
        player = owner;
        Transform waypoints = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (root.name == "AI_Waypoints") { waypoints = root.transform; break; }
        if (waypoints == null || waypoints.childCount < 3 || player == null) { enabled = false; return; }
        bool split = RallyGameSession.LocalPlayerCount == 2;
        Transform parent = transform.Find("Player Viewport");
        if (parent == null) parent = transform;
        panel = Rect(parent, "Mini Map", split ? new Vector2(186, 178) : new Vector2(246, 234));
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(split ? 18 : 28, split ? 16 : 28);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(.035f, .045f, .055f, .78f); background.raycastTarget = false;
        Label(panel, "Map Title", "CIRCUITO", font, split ? 13 : 16, new Vector2(0, panel.sizeDelta.y * .5f - 17));
        Label(panel, "Map Legend", split ? "<color=#FF941F>J1</color>   <color=#26D9FF>J2</color>" :
            "<color=#FF941F>TÚ</color>   <color=#FF5261>BOTS</color>", font, split ? 12 : 14,
            new Vector2(0, -panel.sizeDelta.y * .5f + 16));
        mapArea = Rect(panel, "Map Area", panel.sizeDelta - new Vector2(34, 68));
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int i = 0; i < waypoints.childCount; i++)
        {
            Vector3 position = waypoints.GetChild(i).position;
            var flat = new Vector2(position.x, position.z); min = Vector2.Min(min, flat); max = Vector2.Max(max, flat);
        }
        worldCenter = (min + max) * .5f;
        Vector2 size = max - min;
        projectionScale = Mathf.Min(mapArea.sizeDelta.x / Mathf.Max(1, size.x), mapArea.sizeDelta.y / Mathf.Max(1, size.y));
        var points = new Vector2[waypoints.childCount];
        for (int i = 0; i < points.Length; i++) points[i] = ProjectPosition(waypoints.GetChild(i).position);
        routeGraphic = Rect(mapArea, "Static Circuit Route", mapArea.sizeDelta).gameObject.AddComponent<RallyMiniMapRoute>();
        routeGraphic.color = new Color(.87f, .89f, .86f, .95f);
        routeGraphic.Configure(points, split ? 2.5f : 3.5f);
        var roster = RallyVehiclePresentationCache.ForScene(gameObject.scene);
        markers = new Marker[roster.Length];
        int markerIndex = 0;
        // Bots first: human markers are always drawn above the bot dots.
        for (int pass = 0; pass < 2; pass++) foreach (var vehicle in roster)
        {
            bool bot = vehicle.car.GetComponentInParent<RallyBotController>() != null;
            if ((pass == 0) != bot) continue;
            bool own = vehicle.body == player;
            int playerIndex = vehicle.input == null ? 0 : vehicle.input.PlayerIndex;
            Color color = bot ? BotColor : playerIndex == 0 ? PlayerOneColor : PlayerTwoColor;
            float diameter = bot ? 7 : own ? 12 : 10;
            var border = Rect(mapArea, "Marker - " + vehicle.car.name, Vector2.one * (diameter + 3));
            var outline = border.gameObject.AddComponent<Image>(); outline.color = new Color(.02f, .025f, .03f); outline.raycastTarget = false;
            var dot = Rect(border, "Dot", Vector2.one * diameter).gameObject.AddComponent<Image>(); dot.color = color; dot.raycastTarget = false;
            markers[markerIndex++] = new Marker { body = vehicle.body, icon = border };
        }
        RefreshMarkers();
    }

    void LateUpdate()
    {
        if (markers == null || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + UpdateInterval;
        RefreshMarkers();
    }

    void RefreshMarkers()
    {
        Vector2 bounds = mapArea.sizeDelta * .5f + new Vector2(7, 7);
        foreach (var marker in markers)
        {
            if (marker.body == null || marker.icon == null) continue;
            Vector2 position = ProjectPosition(marker.body.position);
            position.x = Mathf.Clamp(position.x, -bounds.x, bounds.x);
            position.y = Mathf.Clamp(position.y, -bounds.y, bounds.y);
            if ((position - marker.icon.anchoredPosition).sqrMagnitude > .0001f) marker.icon.anchoredPosition = position;
        }
    }

    static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; return rect;
    }
    static void Label(RectTransform parent, string name, string text, TMP_FontAsset font, float size, Vector2 position)
    {
        var label = Rect(parent, name, new Vector2(parent.sizeDelta.x - 12, 24)).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(.83f, .85f, .82f); label.text = text; label.raycastTarget = false;
        label.rectTransform.anchoredPosition = position;
    }
}
