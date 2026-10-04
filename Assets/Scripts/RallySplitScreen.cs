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
        inputOne.Configure(RallyLocalPlayerInput.InputDevice.Keyboard, dynamicsOne.SteeringResponse);
        var inputTwo = secondCar.AddComponent<RallyLocalPlayerInput>();
        inputTwo.Configure(RallyLocalPlayerInput.InputDevice.Gamepad, dynamicsOne.SteeringResponse);
        PlayerOne.SetLocalInput(inputOne);
        PlayerTwo.SetLocalInput(inputTwo);
        dynamicsOne.SetLocalInput(inputOne);
        var secondDynamics = Instantiate(dynamicsOne.gameObject, staging.transform);
        secondDynamics.name = "Player 2 Rally Dynamics";
        dynamicsTwo = secondDynamics.GetComponent<RallyVehicleDynamics>();
        dynamicsTwo.BindVehicle(PlayerTwo);
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
        FitCanvas(hudOne.GetComponent<Canvas>(), CameraOne.rect, "J1 · TECLADO");
        FitCanvas(hudTwo.GetComponent<Canvas>(), CameraTwo.rect, "J2 · GAMEPAD");

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
        if (slots.Length < 4) throw new InvalidOperationException("Split screen needs the existing four-slot starting grid.");
        string roadName = gameObject.scene.name == "Circuit_01" ? "Rally_Road_Start_to_Finish" : gameObject.scene.name.Replace("_", "") + "_Road";
        var road = GameObject.Find(roadName).GetComponent<MeshCollider>();
        var bots = FindObjectsByType<RallyBotController>().Where(b => b.gameObject.scene == gameObject.scene).OrderBy(b => b.name).ToArray();
        if (bots.Length > 3) throw new InvalidOperationException("This grid supports two players and up to three bots.");
        if (!road.Raycast(new Ray(slots[0].position + Vector3.up * 80f, Vector3.down), out var originalGround, 200f))
            throw new InvalidOperationException("First grid position must be on the road.");
        // The road join behind the grid is narrower than its centre ray suggests.
        // Find a row with all four tyres supported, never closer than six metres
        // to the row ahead (the tuned body is under five metres long).
        Vector3 fifth = default;
        bool supported = false;
        JrsVehicleController lastCar = bots.Length == 3 ? bots[2].GetComponentInChildren<JrsVehicleController>() : PlayerOne;
        for (float spacing = 13f; spacing >= 6f; spacing -= 1f)
        {
            Vector3 candidate = slots[2].position - slots[0].forward * spacing;
            if (!road.Raycast(new Ray(candidate + Vector3.up * 80f, Vector3.down), out var ground, 200f)) continue;
            candidate.y = ground.point.y + slots[0].position.y - originalGround.point.y;
            if (!RoadSupportsWheels(road, lastCar, candidate, slots[0].rotation)) continue;
            fifth = candidate;
            supported = true;
            break;
        }
        if (!supported) throw new InvalidOperationException("No safe fifth grid position on " + gameObject.scene.name);
        var marker = new GameObject("Grid_05_Local_Bot").transform;
        marker.SetParent(grid, false);
        marker.SetPositionAndRotation(fifth, slots[0].rotation);
        SetPose(PlayerOne.GetComponent<Rigidbody>(), slots[0]);
        SetPose(PlayerTwo.GetComponent<Rigidbody>(), slots[1]);
        for (int i = 0; i < bots.Length; i++)
            SetPose(bots[i].GetComponentInChildren<Rigidbody>(), i < 2 ? slots[i + 2] : marker);
        Physics.SyncTransforms();
    }

    static bool RoadSupportsWheels(MeshCollider road, JrsVehicleController car, Vector3 position, Quaternion rotation)
    {
        Rigidbody body = car.GetComponent<Rigidbody>();
        Quaternion delta = rotation * Quaternion.Inverse(body.rotation);
        foreach (WheelCollider wheel in new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel })
        {
            Vector3 centre = position + delta * (wheel.transform.TransformPoint(wheel.center) - body.position);
            if (!road.Raycast(new Ray(centre + Vector3.up * 80f, Vector3.down), out _, 200f)) return false;
        }
        return true;
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
        panel.anchoredPosition = new Vector2(0f, -95f);
        panel.sizeDelta = new Vector2(390f, 115f);
        panel.localScale = Vector3.one;
        panel.GetComponentInChildren<TMP_Text>().fontSize = 42f;
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
