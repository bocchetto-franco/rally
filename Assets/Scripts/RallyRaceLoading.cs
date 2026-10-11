using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Prepare the real scene behind an opaque loading overlay, before any physics/input.</summary>
[DefaultExecutionOrder(30000)]
public sealed class RallyRaceLoading : MonoBehaviour
{
    static RallyRaceLoading instance;
    public static bool IsPreparing => instance != null;
    public static bool IsInputBlocked => instance != null && instance.preparing;
    readonly List<UnityEngine.Object> retained = new List<UnityEngine.Object>();
    readonly List<Behaviour> held = new List<Behaviour>();
    readonly List<bool> enabledBefore = new List<bool>();
    readonly List<ParticleSystem> particles = new List<ParticleSystem>();
    readonly List<bool> particlePlaying = new List<bool>();
    Camera[] cameras;
    Vector3[] positions;
    Quaternion[] rotations;
    int[] renderedFrames;
    int[] requiredFrames;
    TMP_Text caption;
    Image fill;
    bool preparing, warmCamera;
    Vector3 warmPosition;
    Quaternion warmRotation;
    bool oldAudioPause;
    float oldTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }
    static bool Bypass => Array.IndexOf(Environment.GetCommandLineArgs(), "-rally-no-load-prep") >= 0;
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || Bypass || Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0) return;
        if (instance == null) Create().StartCoroutine(CreateWork(scene.name, false));
        // sceneLoaded runs after Awake and before Start/physics; override RaceFlow.Awake's timeScale reset now.
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }
    static RallyRaceLoading Create()
    {
        var go = new GameObject("Race Loading - preparation gate");
        instance = go.AddComponent<RallyRaceLoading>();
        DontDestroyOnLoad(go);
        instance.oldTimeScale = Time.timeScale;
        instance.oldAudioPause = AudioListener.pause;
        instance.BuildOverlay();
        return instance;
    }
    public static void Load(string scene)
    {
        if (instance != null) return;
        if (Array.IndexOf(RallyGameSession.CircuitScenes, scene) < 0) throw new ArgumentException("Not a race scene: " + scene);
        if (Bypass) { SceneManager.LoadScene(scene); return; }
        Create().StartCoroutine(CreateWork(scene, true));
    }
    static IEnumerator CreateWork(string scene, bool load) => instance.Run(instance.Prepare(scene, load));
    IEnumerator Run(IEnumerator worker)
    {
        while (true)
        {
            bool more = false;
            object current = null;
            Exception failure = null;
            try { more = worker.MoveNext(); if (more) current = worker.Current; }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                Debug.LogException(failure);
                Restore();
                preparing = false;
                Time.timeScale = 0f;
                caption.SetText("No se pudo preparar la pista.\nVolvé al menú e intentá de nuevo.");
                AddErrorReturn();
                yield break;
            }
            if (!more) break;
            yield return current;
        }
        Restore();
        // Runtime-created buffers and temporary text can be collected while the overlay still covers the race.
        retained.Clear();
        GC.Collect();
        for (int i = 0; i < 3; i++) yield return null;
        Time.timeScale = 1f;
        preparing = false;
        instance = null;
        Destroy(gameObject);
    }
    IEnumerator Prepare(string sceneName, bool load)
    {
        preparing = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Stage("CARGANDO PISTA", .05f);
        // Present at least one frame of the overlay before beginning scene integration.
        yield return null;
        if (load)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
        }
        Time.timeScale = 0f;
        AudioListener.pause = true;
        // Let scene Start create cars, split-screen rig, HUD, audio and all particle systems with physics frozen.
        yield return null; yield return null;
        if (RallyGameSession.LocalPlayerCount == 2)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (RallySplitScreen.Active == null || !RallySplitScreen.Active.Ready)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new Exception("Split-screen rig did not finish initializing.");
                yield return null;
            }
        }
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Race scene not loaded.");
        // Components already present in the scene can Start before a split rig
        // added by sceneLoaded. Let its waiting presentation Start routines finish,
        // and build the roster while vehicle controllers are still enabled.
        yield return null;
        RallyVehiclePresentationCache.ForScene(scene);
        foreach (var root in scene.GetRootGameObjects())
        {
            Hold(root.GetComponentsInChildren<JrsVehicleController>(true));
            Hold(root.GetComponentsInChildren<JrsFollowCamera>(true));
            Hold(root.GetComponentsInChildren<RallyVehicleDynamics>(true));
            Hold(root.GetComponentsInChildren<RallyLocalPlayerInput>(true));
            Hold(root.GetComponentsInChildren<RallyCheckpointManager>(true));
            Hold(root.GetComponentsInChildren<RallyBotController>(true));
            Hold(root.GetComponentsInChildren<RallyVehicleRecovery>(true));
            Hold(root.GetComponentsInChildren<RallyPauseMenu>(true));
        }
        Stage("PREPARANDO AUTOS Y AUDIO", .25f);
        // Only the selectable models, not every historical Resources asset.
        foreach (string path in new[] { "Vehicles/BmwE30", "Vehicles/AudiQuattro", "Audio/RallyAudioLibrary" })
        {
            var request = Resources.LoadAsync<UnityEngine.Object>(path);
            while (!request.isDone) yield return null;
            if (request.asset == null) throw new Exception("Missing race resource: " + path);
            retained.Add(request.asset);
        }
        var clips = new HashSet<AudioClip>();
        foreach (var source in Resources.FindObjectsOfTypeAll<AudioSource>())
            if (source.clip != null && (source.gameObject.scene == scene || source.ignoreListenerPause)) clips.Add(source.clip);
        RallyAudioLibrary library = RallyAudioLibrary.Load();
        if (library != null)
            foreach (var clip in new[] { library.engineLoop, library.tyreSkidLoop, library.impact, library.raceMusic })
                if (clip != null) clips.Add(clip);
        foreach (AudioClip clip in clips)
        {
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (clip.loadState == AudioDataLoadState.Loading)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new Exception("Audio load timed out: " + clip.name);
                yield return null;
            }
            if (clip.loadState == AudioDataLoadState.Failed) throw new Exception("Audio load failed: " + clip.name);
        }
        Stage("PREPARANDO EFECTOS", .45f);
        foreach (var root in scene.GetRootGameObjects())
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!system.gameObject.activeInHierarchy) continue;
                particles.Add(system); particlePlaying.Add(system.isPlaying);
                system.Simulate(.05f, false, true, true);
                // Allocate the small existing pools now, not on the first smoke/splash burst.
                system.Emit(system.main.maxParticles);
                system.Pause(false);
                if (particles.Count % 8 == 0) yield return null;
            }
        // Pre-populate the digits/labels actually used by race UI, not the entire font.
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                font.TryAddCharacters("0123456789:./° ·ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÁÉÍÓÚÜÑáéíóúüñ¿?¡!()-");
        Canvas.ForceUpdateCanvases();
        Stage("PREPARANDO PAISAJE Y SHADERS", .55f);
        cameras = Array.FindAll(Camera.allCameras, camera => camera.gameObject.scene == scene && camera.cameraType == CameraType.Game);
        if (cameras.Length == 0) throw new Exception("Race has no camera.");
        positions = new Vector3[cameras.Length]; rotations = new Quaternion[cameras.Length];
        renderedFrames = new int[cameras.Length]; requiredFrames = new int[cameras.Length];
        for (int i = 0; i < cameras.Length; i++) { positions[i] = cameras[i].transform.position; rotations[i] = cameras[i].transform.rotation; }
        Transform route = null;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "AI_Waypoints") { route = root.transform; break; }
        if (route == null || route.childCount < 2) throw new Exception("Missing circuit route.");
        // Actual URP frames exercise terrain, LOD, water, alpha and vertex layouts on the active graphics API.
        // A generic Shader.WarmupAllShaders cannot guarantee these PSOs on DX12 and wastes work on unused assets.
        const int views = 24;
        warmCamera = true;
        RenderPipelineManager.endCameraRendering += Rendered;
        for (int i = 0; i < views; i++)
        {
            int index = i * route.childCount / views;
            Vector3 point = route.GetChild(index).position;
            Vector3 forward = route.GetChild((index + 1) % route.childCount).position - point;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            for (int reverse = 0; reverse < 2; reverse++)
            {
                Vector3 facing = reverse == 0 ? forward : -forward;
                warmPosition = point + Vector3.up * 5f - facing * 7f;
                warmRotation = Quaternion.LookRotation(facing + Vector3.down * .12f, Vector3.up);
                Stage("PREPARANDO PAISAJE Y SHADERS", .55f + .4f * (i * 2 + reverse + 1) / (views * 2));
                // A yield alone is not a rendered frame (a minimized player can
                // tick without drawing). Keep control locked until BOTH real views render.
                for (int camera = 0; camera < cameras.Length; camera++) requiredFrames[camera] = renderedFrames[camera] + 2;
                do { yield return null; } while (!ViewsRendered());
            }
        }
        warmCamera = false;
        RenderPipelineManager.endCameraRendering -= Rendered;
        for (int i = 0; i < cameras.Length; i++) cameras[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] == null) continue;
            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (particlePlaying[i]) particles[i].Play(false);
        }
        Stage("LISTO PARA CORRER", 1f);
        yield return null;
    }
    void Rendered(ScriptableRenderContext context, Camera camera)
    {
        if (!warmCamera || cameras == null) return;
        for (int i = 0; i < cameras.Length; i++) if (cameras[i] == camera) { renderedFrames[i]++; break; }
    }
    bool ViewsRendered()
    {
        for (int i = 0; i < cameras.Length; i++) if (renderedFrames[i] < requiredFrames[i]) return false;
        return true;
    }
    void Hold<T>(T[] components) where T : Behaviour
    {
        foreach (T component in components) { held.Add(component); enabledBefore.Add(component.enabled); component.enabled = false; }
    }
    void Restore()
    {
        RenderPipelineManager.endCameraRendering -= Rendered;
        warmCamera = false;
        for (int i = 0; i < held.Count; i++) if (held[i] != null) held[i].enabled = enabledBefore[i];
        held.Clear(); enabledBefore.Clear();
        AudioListener.pause = oldAudioPause;
    }
    void LateUpdate()
    {
        if (!preparing) return;
        Time.timeScale = 0f;
        if (!warmCamera || cameras == null) return;
        foreach (var camera in cameras) if (camera != null) camera.transform.SetPositionAndRotation(warmPosition, warmRotation);
    }
    void Stage(string value, float progress) { if (caption.text != value) caption.SetText(value); fill.rectTransform.localScale = new Vector3(progress, 1, 1); }
    void BuildOverlay()
    {
        var canvas = new GameObject("Race Loading Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 5000;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var background = new GameObject("Opaque Loading Background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)background.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.045f, .035f, .025f, 1);
        caption = new GameObject("Loading Caption", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        caption.transform.SetParent(canvas.transform, false); caption.rectTransform.sizeDelta = new Vector2(1350, 180); caption.rectTransform.anchoredPosition = new Vector2(0, 40);
        caption.font = Resources.Load<TMP_FontAsset>("MenuFonts/Roboto") ?? TMP_Settings.defaultFontAsset;
        caption.fontSize = 40; caption.alignment = TextAlignmentOptions.Center; caption.color = Color.white; caption.raycastTarget = false;
        var bar = new GameObject("Loading Progress", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(canvas.transform, false);
        var barRect = (RectTransform)bar.transform; barRect.sizeDelta = new Vector2(680, 10); barRect.pivot = new Vector2(0, .5f); barRect.anchoredPosition = new Vector2(-340, -100);
        fill = bar.GetComponent<Image>(); fill.color = new Color(1, .57f, .13f); fill.rectTransform.localScale = new Vector3(0, 1, 1);
        caption.SetText("CARGANDO PISTA");
    }
    void AddErrorReturn()
    {
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var go = new GameObject("Loading Error - Volver", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(caption.transform.parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(500, 90); rect.anchoredPosition = new Vector2(0, -240);
        go.GetComponent<Image>().color = new Color(1, .57f, .13f);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(rect, false); label.rectTransform.sizeDelta = rect.sizeDelta; label.font = caption.font; label.fontSize = 30; label.alignment = TextAlignmentOptions.Center; label.text = "VOLVER AL MENÚ"; label.color = Color.black;
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => { Time.timeScale = 1f; instance = null; Destroy(gameObject); SceneManager.LoadScene(RallyGameSession.MainMenuScene); });
        go.AddComponent<RallyMenuNavigation>().Configure(button);
    }
    void OnDestroy()
    {
        Restore();
        if (instance == this) { instance = null; Time.timeScale = oldTimeScale > 0 ? oldTimeScale : 1; }
    }
}
