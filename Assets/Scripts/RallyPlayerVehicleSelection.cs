using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Swap only the player's presentation/contact geometry before Start. Keep its existing
/// Rigidbody, controller, dynamics and references held by camera, HUD, gates and ranking.
/// No alternative physics engine and no changes to the bot prefab/profile.
/// </summary>
public static class RallyPlayerVehicleSelection
{
    public const string MiniName = "Mini Classic Rally";
    public const string MiniResource = "Vehicles/ClassicMini";
    public static readonly string[] Names = { RallyGameSession.VehicleName, MiniName };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0) return;
        RallyGameSession.RestoreSavedState();
        if (RallyGameSession.SelectedVehicle == RallyGameSession.VehicleName) return;
        var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JrsVehicleController>())
            .FirstOrDefault(c => c.name == RallyGameSession.VehicleName && c.GetComponent<RallyBotController>() == null);
        if (player == null) { Debug.LogError("Selected vehicle: player root not found."); return; }
        ApplyMini(player);
    }

    public static RallyVehicleVisual ApplyMini(JrsVehicleController player)
    {
        if (player.GetComponent<RallyBotController>() != null)
            throw new InvalidOperationException("Player selection must never modify a bot.");
        var existing = player.GetComponentInChildren<RallyVehicleVisual>();
        if (existing != null) return existing;
        var prefab = Resources.Load<RallyVehicleVisual>(MiniResource);
        if (prefab == null || prefab.wheels.Length != 4 || prefab.radii.Length != 4)
            throw new InvalidOperationException("Missing or invalid ClassicMini visual prefab.");

        WheelCollider[] colliders = { player.frontLeftWheel, player.frontRightWheel, player.rearLeftWheel, player.rearRightWheel };
        Transform root = player.transform;
        // Use the existing spawn clearance, not Terrain height (the road is a separate mesh).
        float ground = colliders.Average(w => root.InverseTransformPoint(w.transform.TransformPoint(w.center)).y - w.radius);
        var visual = UnityEngine.Object.Instantiate(prefab, root);
        visual.name = MiniName + " Visual";
        visual.transform.localPosition = Vector3.up * ground;
        visual.transform.localRotation = Quaternion.identity;

        foreach (var binder in player.GetComponentsInChildren<PorscheVehicleBinder>(true)) binder.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<MeshRenderer>(true))
            if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = false;

        for (int i = 0; i < 4; i++)
        {
            colliders[i].transform.position = visual.wheels[i].position;
            colliders[i].center = Vector3.zero;
            colliders[i].radius = visual.radii[i];
        }
        player.frontLeftWheelTransform = visual.wheels[0];
        player.frontRightWheelTransform = visual.wheels[1];
        player.rearLeftWheelTransform = visual.wheels[2];
        player.rearRightWheelTransform = visual.wheels[3];

        var body = player.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => c.name == "PorscheBodyCollider");
        if (body != null)
        {
            body.center = body.transform.InverseTransformPoint(visual.transform.TransformPoint(visual.bodyBounds.center));
            body.size = Vector3.Scale(visual.bodyBounds.size, new Vector3(.92f, .78f, .90f));
        }
        // COM, mass, damping, springs, friction, steering, brakes and downforce stay as tuned.
        Physics.SyncTransforms();
        return visual;
    }
}
