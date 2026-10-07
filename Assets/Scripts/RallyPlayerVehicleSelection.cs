using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Swap a vehicle's presentation before its controller Start. Keep its existing
/// Rigidbody, WheelColliders, dynamics and race references; bots retain their grip profile.
/// </summary>
public static class RallyPlayerVehicleSelection
{
    public const string BmwName = "BMW M3 E30";
    public const string BmwResource = "Vehicles/BmwE30";
    public const string AudiName = "Audi Quattro S1";
    public const string AudiResource = "Vehicles/AudiQuattro";
    public static readonly string[] Names = { RallyGameSession.VehicleName, BmwName, AudiName };

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
        // Clone the pristine split-screen rig before applying independent visual selections.
        if (RallyGameSession.LocalPlayerCount == 2 || RallyGameSession.SelectedVehicle == RallyGameSession.VehicleName) return;
        var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JrsVehicleController>())
            .FirstOrDefault(c => c.name == RallyGameSession.VehicleName && c.GetComponentInParent<RallyBotController>() == null);
        if (player == null) { Debug.LogError("Selected vehicle: player root not found."); return; }
        ApplyVisual(player, RallyGameSession.SelectedVehicle);
    }

    public static RallyVehicleVisual ApplyMini(JrsVehicleController player)
        => ApplyVisual(player, BmwName); // Compatibility with the original editor validation entry.

    public static RallyVehicleVisual ApplyVisual(JrsVehicleController player, string vehicleName)
    {
        if (player.GetComponentInParent<RallyBotController>(true) != null)
            throw new InvalidOperationException("Player selection must never modify a bot.");
        return ApplyVisualOnly(player, vehicleName);
    }

    /// <summary>Fits the selected model and wheel geometry, never the bot's grip profile.</summary>
    public static RallyVehicleVisual ApplyBotVisual(JrsVehicleController bot, string vehicleName)
    {
        if (bot.GetComponentInParent<RallyBotController>(true) == null)
            throw new InvalidOperationException("Bot visuals require a bot vehicle.");
        if (vehicleName == RallyGameSession.VehicleName)
            return null; // The shared Bot_Car prefab already has the Porsche model.
        return ApplyVisualOnly(bot, vehicleName);
    }

    private static RallyVehicleVisual ApplyVisualOnly(JrsVehicleController player, string vehicleName)
    {
        var existing = player.GetComponentInChildren<RallyVehicleVisual>();
        if (existing != null) return existing;
        string resource = vehicleName == BmwName ? BmwResource : vehicleName == AudiName ? AudiResource : null;
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

        if (visual.useAuthoredScale)
        {
            FitAuthoredWheels(root, visual, colliders);
        }
        else
        {
        // Legacy assets remain supported until replaced. New models must preserve
        // authored uniform scale instead of being stretched to the Porsche axles.
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
        }

        foreach (var binder in player.GetComponentsInChildren<PorscheVehicleBinder>(true)) binder.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<MeshRenderer>(true))
            if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = false;

        player.frontLeftWheelTransform = visual.wheels[0];
        player.frontRightWheelTransform = visual.wheels[1];
        player.rearLeftWheelTransform = visual.wheels[2];
        player.rearRightWheelTransform = visual.wheels[3];

        // Tuning, body collision, mass and inertia stay on the original rig.
        return visual;
    }

    static void FitAuthoredWheels(Transform root, RallyVehicleVisual visual, WheelCollider[] wheels)
    {
        Vector3 oldCenter = Vector3.zero, newCenter = Vector3.zero;
        float oldGround = 0f, newGround = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 old = root.InverseTransformPoint(wheels[i].transform.TransformPoint(wheels[i].center));
            Vector3 next = root.InverseTransformPoint(visual.wheels[i].position);
            oldCenter += old * .25f;
            newCenter += next * .25f;
            oldGround += (old.y - wheels[i].radius) * .25f;
            newGround += (next.y - visual.radii[i]) * .25f;
        }
        // Match the original rig's ground clearance and axle midpoint, without
        // changing the car's scale or moving the race root / checkpoint state.
        Vector3 offset = oldCenter - newCenter;
        offset.y = oldGround - newGround;
        visual.transform.localPosition += offset;
        for (int i = 0; i < 4; i++)
        {
            WheelCollider wheel = wheels[i];
            wheel.transform.position = visual.wheels[i].position - wheel.transform.TransformVector(wheel.center);
            wheel.radius = visual.radii[i];
        }
    }

    public static string MigrateSavedName(string name)
        => name == "Mini Classic Rally" ? BmwName : name == "Lancia Delta HF Integrale" ? AudiName : name;
}
