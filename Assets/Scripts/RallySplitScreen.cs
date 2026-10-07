using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>Runtime two-player setup, retaining the original scene and tuned physics.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class RallySplitScreen : MonoBehaviour
{
    public JrsVehicleController PlayerOne { get; private set; }
    public JrsVehicleController PlayerTwo { get; private set; }
    public RallyCheckpointManager TimerOne { get; private set; }
    public RallyCheckpointManager TimerTwo { get; private set; }
    public Camera CameraOne { get; private set; }
    public Camera CameraTwo { get; private set; }
    public bool Ready { get; private set; }
    public bool AllFinished => Ready && TimerOne.IsFinished && TimerTwo.IsFinished;
    RallyVehicleDynamics dynamicsOne, dynamicsTwo;
    bool frozenOne, frozenTwo;
    public static RallySplitScreen Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        Active = null;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0) return;
        RallyGameSession.RestoreSavedState();
        if (RallyGameSession.LocalPlayerCount != 2) return;
        // Bots are scene instances, not runtime spawns. Disable their complete
        // rigs before Start/physics/rendering, then remove this Play-only copy.
        // The saved scenes and prefabs remain available for single-player races.
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (RallyBotController bot in sceneRoot.GetComponentsInChildren<RallyBotController>(true))
            {
                bot.gameObject.SetActive(false);
                Destroy(bot.gameObject);
            }
        var root = new GameObject("Local Split Screen - 2 Players");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<RallySplitScreen>();
    }

    void Start()
    {
        Active = this;
        PlayerOne = FindObjectsByType<JrsVehicleController>().First(c => c.gameObject.scene == gameObject.scene &&
            c.GetComponentInParent<RallyBotController>() == null && c.name == RallyGameSession.VehicleName);
        dynamicsOne = FindObjectsByType<RallyVehicleDynamics>().First(d => d.VehicleController == PlayerOne);
        TimerOne = FindObjectsByType<RallyCheckpointManager>().First(c => c.gameObject.scene == gameObject.scene);
        CameraOne = FindObjectsByType<JrsFollowCamera>().First(c => c.gameObject.scene == gameObject.scene &&
            c.GetComponent<Camera>() != null && c.GetComponent<Camera>().enabled).GetComponent<Camera>();
        RallyRaceHud hudOne = FindObjectsByType<RallyRaceHud>().First(h => h.gameObject.scene == gameObject.scene);
        RallyRacePositions positions = FindObjectsByType<RallyRacePositions>().First(p => p.gameObject.scene == gameObject.scene);

        // Inactive staging prevents cloned Awake/OnEnable from writing to original
        // external references before we rebind them to the second car.
        GameObject staging = new GameObject("Split Screen Staging");
        staging.SetActive(false);
        var secondCar = Instantiate(PlayerOne.gameObject, staging.transform);
        secondCar.name = "Player 2 Rally Car";
        PlayerTwo = secondCar.GetComponent<JrsVehicleController>();
        var inputOne = PlayerOne.gameObject.AddComponent<RallyLocalPlayerInput>();
        inputOne.ConfigurePlayer(0, dynamicsOne.SteeringResponse);
        var inputTwo = secondCar.AddComponent<RallyLocalPlayerInput>();
        inputTwo.ConfigurePlayer(1, dynamicsOne.SteeringResponse);
        PlayerOne.SetLocalInput(inputOne);
        PlayerTwo.SetLocalInput(inputTwo);
        dynamicsOne.SetLocalInput(inputOne);
        var secondDynamics = Instantiate(dynamicsOne.gameObject, staging.transform);
        secondDynamics.name = "Player 2 Rally Dynamics";
        dynamicsTwo = secondDynamics.GetComponent<RallyVehicleDynamics>();
        dynamicsTwo.BindVehicle(PlayerTwo);
        // Presentation only: retain the same rigidbody, wheels and tuning for both cars.
        if (RallyGameSession.VehicleForPlayer(0) != RallyGameSession.VehicleName)
            RallyPlayerVehicleSelection.ApplyVisual(PlayerOne, RallyGameSession.VehicleForPlayer(0));
        if (RallyGameSession.VehicleForPlayer(1) != RallyGameSession.VehicleName)
            RallyPlayerVehicleSelection.ApplyVisual(PlayerTwo, RallyGameSession.VehicleForPlayer(1));
        secondCar.transform.SetParent(null, true);
        secondDynamics.transform.SetParent(null, true);

        PlaceGrid();
        TimerOne.BindVehicle(PlayerOne.GetComponent<Rigidbody>());
        TimerTwo = new GameObject("Player 2 Checkpoints and Timer").AddComponent<RallyCheckpointManager>();
        TimerTwo.ConfigureLocalPlayer(TimerOne, PlayerTwo.GetComponent<Rigidbody>());
        inputOne.Checkpoints = TimerOne;
        inputTwo.Checkpoints = TimerTwo;
        positions.RebuildRacers();

        var secondCamera = Instantiate(CameraOne.gameObject, staging.transform);
        secondCamera.name = "Player 2 Follow Camera";
        secondCamera.tag = "Untagged";
        foreach (AudioListener listener in secondCamera.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
        secondCamera.transform.SetParent(null, true);
        Destroy(staging);
        CameraTwo = secondCamera.GetComponent<Camera>();
        CameraOne.rect = new Rect(0f, .5f, 1f, .5f);
        CameraTwo.rect = new Rect(0f, 0f, 1f, .5f);
        CameraOne.depth = 0f;
        CameraTwo.depth = 1f;
        var extra = CameraTwo.GetComponent<UniversalAdditionalCameraData>();
        if (extra != null && extra.renderType == CameraRenderType.Base) extra.cameraStack.Clear();
        PositionCamera(CameraOne, PlayerOne);
        PositionCamera(CameraTwo, PlayerTwo);
        // Only one listener in the shared world, even if the source rig contained one.
        AudioListener primaryListener = CameraOne.GetComponent<AudioListener>();
        foreach (AudioListener listener in FindObjectsByType<AudioListener>())
            if (listener != primaryListener) listener.enabled = false;

        var hudTwo = Instantiate(hudOne.gameObject).GetComponent<RallyRaceHud>();
        var duplicatePositions = hudTwo.GetComponent<RallyRacePositions>();
        if (duplicatePositions != null) { duplicatePositions.enabled = false; Destroy(duplicatePositions); }
        hudTwo.name = "Player 2 Race HUD";
        hudOne.BindPlayer(TimerOne, PlayerOne.GetComponent<Rigidbody>(), positions);
        hudTwo.BindPlayer(TimerTwo, PlayerTwo.GetComponent<Rigidbody>(), positions);
        FitCanvas(hudOne.GetComponent<Canvas>(), CameraOne.rect, "JUGADOR 1");
        FitCanvas(hudTwo.GetComponent<Canvas>(), CameraTwo.rect, "JUGADOR 2");

        var warningOne = FindObjectsByType<RallyBrakeWarningSystem>().FirstOrDefault(w => w.VehicleBody == PlayerOne.GetComponent<Rigidbody>());
        if (warningOne != null)
        {
            warningOne.Configure(PlayerOne.GetComponent<Rigidbody>());
            var warningTwo = new GameObject("Player 2 Brake Warnings").AddComponent<RallyBrakeWarningSystem>();
            warningTwo.Configure(PlayerTwo.GetComponent<Rigidbody>());
            inputOne.BrakeWarnings = warningOne;
            inputTwo.BrakeWarnings = warningTwo;
            FitWarning(warningOne, CameraOne.rect);
            FitWarning(warningTwo, CameraTwo.rect);
        }
        Ready = true;
    }

    void Update()
    {
        if (!Ready) return;
        if (TimerOne.IsFinished && !frozenOne) { Freeze(PlayerOne, dynamicsOne); frozenOne = true; }
        if (TimerTwo.IsFinished && !frozenTwo) { Freeze(PlayerTwo, dynamicsTwo); frozenTwo = true; }
    }

    public static void Freeze(JrsVehicleController vehicle, RallyVehicleDynamics dynamics)
    {
        vehicle.enabled = false;
        dynamics.enabled = false;
        var body = vehicle.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
    }

    void OnDestroy() { if (Active == this) Active = null; }

    static void PositionCamera(Camera camera, JrsVehicleController vehicle)
    {
        var follow = camera.GetComponent<JrsFollowCamera>();
        follow.target = vehicle.transform;
        camera.transform.position = vehicle.transform.TransformPoint(follow.offset);
        camera.transform.LookAt(vehicle.transform.position);
    }

    void PlaceGrid()
    {
        Transform grid = GameObject.Find("Starting Grid").transform;
        var slots = Enumerable.Range(0, grid.childCount).Select(grid.GetChild).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
        if (slots.Length < 2) throw new InvalidOperationException("Split screen needs two starting grid positions.");
        SetPose(PlayerOne.GetComponent<Rigidbody>(), slots[0]);
        SetPose(PlayerTwo.GetComponent<Rigidbody>(), slots[1]);
        Physics.SyncTransforms();
    }

    static void SetPose(Rigidbody body, Transform slot)
    {
        // A freshly activated interpolated clone still has the original Transform
        // pose. Update both representations before SyncTransforms/first physics step.
        body.transform.SetPositionAndRotation(slot.position, slot.rotation);
        body.position = slot.position;
        body.rotation = slot.rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    static void FitWarning(RallyBrakeWarningSystem warnings, Rect viewport)
    {
        var canvas = warnings.GetComponentInChildren<Canvas>();
        FitCanvas(canvas, viewport, null);
        var panel = canvas.transform.Find("Player Viewport/Brake Warning") as RectTransform;
        if (panel == null) return;
        panel.anchoredPosition = new Vector2(0f, -196f);
        panel.sizeDelta = new Vector2(300f, 122f);
        panel.localScale = Vector3.one;
    }

    static void FitCanvas(Canvas canvas, Rect viewport, string label)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.transform.localScale = Vector3.one;
        var children = Enumerable.Range(0, canvas.transform.childCount).Select(canvas.transform.GetChild).ToArray();
        var region = new GameObject("Player Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        region.SetParent(canvas.transform, false);
        region.anchorMin = viewport.min;
        region.anchorMax = viewport.max;
        region.offsetMin = region.offsetMax = Vector2.zero;
        foreach (var child in children)
        {
            child.SetParent(region, false);
            child.localScale *= .7f;
            if (child is RectTransform rect) rect.anchoredPosition *= .7f;
        }
        if (label == null) return;
        var title = new GameObject("Player Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(region, false);
        var titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, 0f);
        titleRect.pivot = new Vector2(.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 12f);
        titleRect.sizeDelta = new Vector2(380f, 36f);
        var text = title.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, .85f, .25f);
        text.raycastTarget = false;
    }
}
