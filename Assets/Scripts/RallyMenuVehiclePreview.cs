using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>One on-demand mesh-only vehicle per panel, isolated from race/backdrop cameras.</summary>
public sealed class RallyMenuVehiclePreview : MonoBehaviour
{
    static readonly string[] Paths = { "MenuVehicles/Porsche911", "MenuVehicles/BmwE30", "MenuVehicles/AudiQuattro" };
    static readonly HashSet<RallyMenuVehiclePreview> views = new HashSet<RallyMenuVehiclePreview>();
    static AsyncOperation unusedCleanup;
    [SerializeField, Range(4f, 30f)] float rotationSpeed = 12f;
    [SerializeField, Range(.1f, .6f)] float fadeDuration = .22f;
    RawImage image;
    TMP_Text title, details;
    CanvasGroup fade;
    GameObject studio, model;
    Transform turntable;
    Camera studioCamera;
    RenderTexture target;
    int requested = -1, shown = -1, layer;
    float lastChange;
    Coroutine transition;
    ResourceRequest activeRequest;
    bool cleaning;

    public int RequestedIndex => requested;
    public int ShownIndex => shown;
    public bool IsTransitioning => transition != null;
    public Camera PreviewCamera => studioCamera;
    public GameObject VisibleModel => model;
    public float Opacity => fade != null ? fade.alpha : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { views.Clear(); unusedCleanup = null; }

    public void Configure(RawImage display, TMP_Text nameLabel, TMP_Text description, int player)
    {
        image = display; title = nameLabel; details = description;
        fade = gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0;
        layer = 30 - player; // Backdrop uses 31; the two studios cannot see each other.
        CreateStudio(player);
        views.Add(this);
    }

    public void Show(int index)
    {
        if (index < 0 || index >= Paths.Length || requested == index) return;
        requested = index; lastChange = Time.unscaledTime;
        if (transition == null) transition = StartCoroutine(Change());
    }

    IEnumerator Change()
    {
        // A single worker coalesces rapid D-Pad input. It never creates hidden spare cars.
        yield return null;
        while (shown != requested)
        {
            yield return FadeTo(0);
            studioCamera.enabled = false;
            if (model != null) { model.SetActive(false); Destroy(model); model = null; }
            shown = -1;
            yield return null; // Destroy old renderers before allocating the replacement.
            while (Time.unscaledTime - lastChange < .12f) yield return null;
            if (unusedCleanup != null && !unusedCleanup.isDone) yield return unusedCleanup;
            int choice = requested;
            // Keep request/prefab references inside this short coroutine, not in a persistent cache.
            yield return LoadModel(choice);
            if (shown < 0 && choice == requested)
            {
                title.text = RallyGameSession.VehicleDisplayName(choice);
                if (details != null) details.text = "Vista no disponible · podés elegir este auto igualmente";
                yield return FadeTo(1);
                break;
            }
            if (choice != requested) continue;
            title.text = RallyGameSession.VehicleDisplayName(choice);
            if (details != null) details.text = "";
            yield return FadeTo(1);
        }
        transition = null;
        cleaning = true;
    }

    IEnumerator LoadModel(int choice)
    {
        var request = Resources.LoadAsync<GameObject>(Paths[choice]);
        activeRequest = request;
        yield return request;
        activeRequest = null;
        if (choice != requested) yield break;
        if (request.asset == null)
        {
            Debug.LogWarning("Vehicle preview missing: " + Paths[choice]);
            yield break;
        }
        model = Instantiate((GameObject)request.asset, turntable);
        model.name = "Preview - " + RallyGameSession.VehicleDisplayName(choice);
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        turntable.localRotation = Quaternion.Euler(0, -35, 0);
        shown = choice;
        studioCamera.enabled = true;
        yield return null; // Fill the render target while the view is still invisible.
    }

    IEnumerator FadeTo(float destination)
    {
        float start = fade.alpha, elapsed = 0;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, destination, Mathf.SmoothStep(0, 1, elapsed / fadeDuration));
            yield return null;
        }
        fade.alpha = destination;
    }

    void LateUpdate()
    {
        if (model != null) turntable.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.Self);
        if (!cleaning || transition != null || Time.unscaledTime - lastChange < 1f) return;
        foreach (var view in views) if (view != null && view.transition != null) return;
        cleaning = false;
        // Resources.LoadAsync doesn't automatically evict discarded prefab assets.
        // One debounced asynchronous cleanup, after BOTH panels finish transitioning.
        if (unusedCleanup == null || unusedCleanup.isDone) unusedCleanup = Resources.UnloadUnusedAssets();
    }

    void CreateStudio(int player)
    {
        studio = new GameObject("Vehicle Preview Studio " + (player + 1));
        studio.transform.position = new Vector3(12000 + player * 40, 12000, 12000);
        turntable = new GameObject("Rotating vehicle").transform;
        turntable.SetParent(studio.transform, false);
        var cameraObject = new GameObject("Vehicle Preview Camera " + (player + 1));
        cameraObject.transform.SetParent(studio.transform, false);
        studioCamera = cameraObject.AddComponent<Camera>();
        studioCamera.enabled = false;
        studioCamera.clearFlags = CameraClearFlags.SolidColor;
        studioCamera.backgroundColor = new Color(.045f, .052f, .062f);
        studioCamera.cullingMask = 1 << layer;
        studioCamera.orthographic = true; studioCamera.orthographicSize = 1.3f;
        studioCamera.nearClipPlane = .1f; studioCamera.farClipPlane = 24;
        studioCamera.allowHDR = false; studioCamera.allowMSAA = false;
        studioCamera.useOcclusionCulling = false;
        cameraObject.transform.localPosition = new Vector3(4, 2.6f, 5.5f);
        cameraObject.transform.LookAt(studio.transform.position + Vector3.up * .75f);
        var data = studioCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false; data.renderShadows = false; data.volumeLayerMask = 0;
        data.requiresColorTexture = false; data.requiresDepthTexture = false;
        data.antialiasing = AntialiasingMode.None;
        int width = player == 0 && RallyGameSession.LocalPlayerCount == 1 ? 768 : 640;
        int height = Mathf.RoundToInt(width * image.rectTransform.sizeDelta.y / image.rectTransform.sizeDelta.x);
        target = new RenderTexture(width, height, 16) { name = "Car studio - Player " + (player + 1), antiAliasing = 1 };
        target.Create(); studioCamera.targetTexture = target; image.texture = target;
        Light key = StudioLight("Studio key", LightType.Directional, 1.8f, Color.white);
        key.transform.localRotation = Quaternion.Euler(38, -32, 0);
        Light fill = StudioLight("Studio fill", LightType.Point, 28, new Color(.82f, .89f, 1f));
        fill.range = 14; fill.transform.localPosition = new Vector3(-3, 3, 2);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(floor.GetComponent<Collider>());
        floor.name = "Neutral turntable"; floor.layer = layer;
        floor.transform.SetParent(studio.transform, false);
        floor.transform.localPosition = new Vector3(0, -.035f, 0);
        floor.transform.localScale = new Vector3(5.3f, .025f, 5.3f);
        var renderer = floor.GetComponent<MeshRenderer>(); renderer.sharedMaterial = Resources.Load<Material>("MenuVehicles/StudioFloor");
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    Light StudioLight(string name, LightType type, float intensity, Color color)
    {
        var go = new GameObject(name); go.layer = layer; go.transform.SetParent(studio.transform, false);
        var light = go.AddComponent<Light>(); light.type = type; light.intensity = intensity;
        light.color = color; light.shadows = LightShadows.None; light.cullingMask = 1 << layer;
        return light;
    }

    void OnDisable()
    {
        if (studio != null) studio.SetActive(false);
        views.Remove(this);
    }

    void OnDestroy()
    {
        views.Remove(this);
        if (studioCamera != null) studioCamera.targetTexture = null;
        if (image != null) image.texture = null;
        if (target != null) { target.Release(); Destroy(target); }
        if (studio != null) Destroy(studio);
        if (views.Count == 0)
        {
            if (activeRequest != null && !activeRequest.isDone) activeRequest.completed += CleanupAfterExit;
            else CleanupAfterExit(null);
        }
    }

    static void CleanupAfterExit(AsyncOperation operation)
    {
        if (views.Count == 0 && !RallyRaceLoading.IsPreparing && (unusedCleanup == null || unusedCleanup.isDone))
            unusedCleanup = Resources.UnloadUnusedAssets();
    }
}
