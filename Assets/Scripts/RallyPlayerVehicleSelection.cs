using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Swap only the player's presentation before Start. Keep its existing
/// Rigidbody, controller, dynamics and references held by camera, HUD, gates and ranking.
/// No alternative physics engine and no changes to the bot prefab/profile.
/// </summary>
public static class RallyPlayerVehicleSelection
{
    public const string MiniName = "Mini Classic Rally";
    public const string MiniResource = "Vehicles/ClassicMini";
    public const string LanciaName = "Lancia Delta HF Integrale";
    public const string LanciaResource = "Vehicles/LanciaDelta";
    public static readonly string[] Names = { RallyGameSession.VehicleName, MiniName, LanciaName };

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
        ApplyVisual(player, RallyGameSession.SelectedVehicle);
    }

    public static RallyVehicleVisual ApplyMini(JrsVehicleController player)
        => ApplyVisual(player, MiniName);

    public static RallyVehicleVisual ApplyVisual(JrsVehicleController player, string vehicleName)
    {
        if (player.GetComponent<RallyBotController>() != null)
            throw new InvalidOperationException("Player selection must never modify a bot.");
        var existing = player.GetComponentInChildren<RallyVehicleVisual>();
        if (existing != null) return existing;
        string resource = vehicleName == MiniName ? MiniResource : vehicleName == LanciaName ? LanciaResource : null;
        if (resource == null) throw new ArgumentOutOfRangeException(nameof(vehicleName));
        var prefab = Resources.Load<RallyVehicleVisual>(resource);
        if (prefab == null || prefab.wheels.Length != 4 || prefab.radii.Length != 4)
            throw new InvalidOperationException("Missing or invalid vehicle visual prefab: " + resource);
        if (prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            throw new InvalidOperationException("Visual variants must not contain any physics components.");

        WheelCollider[] colliders = { player.frontLeftWheel, player.frontRightWheel, player.rearLeftWheel, player.rearRightWheel };
        Transform root = player.transform;
        var visual = UnityEngine.Object.Instantiate(prefab, root);
        visual.name = vehicleName + " Visual";
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localPosition = Vector3.zero;

        // A shared set of friction numbers is NOT enough: wheelbase, track, radius,
        // body collision shape and inertia also affect handling. Never refit the
        // physics to the selected mesh. Fit the VISUAL to the proven Porsche rig.
        Vector3[] source = visual.wheels.Select(w => w.localPosition).ToArray();
        Vector3[] target = colliders.Select(w => root.InverseTransformPoint(w.transform.TransformPoint(w.center))).ToArray();
        Vector3 sourceCenter = source.Aggregate(Vector3.zero, (a, b) => a + b) * .25f;
        Vector3 targetCenter = target.Aggregate(Vector3.zero, (a, b) => a + b) * .25f;
        float widthScale = Mathf.Abs((target[1].x - target[0].x + target[3].x - target[2].x) /
            (source[1].x - source[0].x + source[3].x - source[2].x));
        float lengthScale = Mathf.Abs((target[0].z + target[1].z - target[2].z - target[3].z) /
            (source[0].z + source[1].z - source[2].z - source[3].z));
        float radiusScale = colliders.Average(w => w.radius) / visual.radii.Average();
        Vector3 bodyScale = new Vector3(widthScale, radiusScale, lengthScale);
        // Scale the body to the shared axles and wheel visuals uniformly to their
        // collider radii. The controller sets pivot world poses every frame.
        foreach (Transform part in visual.transform)
        {
            if (visual.wheels.Contains(part)) continue;
            part.localPosition = targetCenter + Vector3.Scale(part.localPosition - sourceCenter, bodyScale);
            part.localScale = Vector3.Scale(part.localScale, bodyScale);
        }
        for (int i = 0; i < 4; i++)
        {
            visual.wheels[i].localPosition = target[i];
            visual.wheels[i].localScale = Vector3.one * (colliders[i].radius / visual.radii[i]);
        }

        foreach (var binder in player.GetComponentsInChildren<PorscheVehicleBinder>(true)) binder.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<MeshRenderer>(true))
            if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = false;

        player.frontLeftWheelTransform = visual.wheels[0];
        player.frontRightWheelTransform = visual.wheels[1];
        player.rearLeftWheelTransform = visual.wheels[2];
        player.rearRightWheelTransform = visual.wheels[3];

        // Do not touch ANY physics parameter, transform, collision or inertia state.
        return visual;
    }
}
