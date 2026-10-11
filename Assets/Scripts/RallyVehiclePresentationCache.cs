using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-entry references for presentation effects. Never queried from a frame loop.</summary>
public static class RallyVehiclePresentationCache
{
    public readonly struct Vehicle
    {
        public readonly JrsVehicleController car;
        public readonly Rigidbody body;
        public readonly RallyLocalPlayerInput input;
        public Vehicle(JrsVehicleController controller)
        {
            car = controller;
            body = controller.GetComponent<Rigidbody>();
            input = controller.GetComponent<RallyLocalPlayerInput>();
        }
    }

    static readonly Dictionary<Scene, Vehicle[]> scenes = new Dictionary<Scene, Vehicle[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        scenes.Clear();
        SceneManager.sceneUnloaded -= Unloaded;
        SceneManager.sceneUnloaded += Unloaded;
    }

    static void Unloaded(Scene scene) => scenes.Remove(scene);

    // Components added by sceneLoaded may start a frame later than existing ones,
    // even with an earlier execution order. Wait for the actual split rig, not order alone.
    public static bool IsRosterReady(Scene scene) =>
        RallyGameSession.LocalPlayerCount != 2 || System.Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0 ||
        (RallySplitScreen.Active != null && RallySplitScreen.Active.gameObject.scene == scene && RallySplitScreen.Active.Ready);

    // Call from Start or an explicit spawn/setup step, once IsRosterReady is true.
    // Current races do not change their roster mid-lap. A future dynamic spawner
    // must explicitly rebuild/rebind its effect references before enabling them.
    public static Vehicle[] ForScene(Scene scene)
    {
        if (scenes.TryGetValue(scene, out Vehicle[] vehicles)) return vehicles;
        var list = new List<Vehicle>(4);
        foreach (JrsVehicleController car in Object.FindObjectsByType<JrsVehicleController>(FindObjectsInactive.Exclude))
            // Loading temporarily disables driving components; their active rigs
            // still belong to the roster. Inactive police/bot references are excluded.
            if (car.gameObject.scene == scene && car.gameObject.activeInHierarchy) list.Add(new Vehicle(car));
        vehicles = list.ToArray();
        scenes.Add(scene, vehicles);
        return vehicles;
    }
}
