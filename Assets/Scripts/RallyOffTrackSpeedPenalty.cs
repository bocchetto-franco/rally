using System.Collections.Generic;
using UnityEngine;

/// <summary>One 25% horizontal speed loss per confirmed road exit, never every physics frame.</summary>
[DefaultExecutionOrder(950)]
[DisallowMultipleComponent]
public sealed class RallyOffTrackSpeedPenalty : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float retainedSpeed = .75f;
    [SerializeField, Min(0f)] float offTrackConfirmationSeconds = .12f;
    [SerializeField, Min(0f)] float roadRearmSeconds = .35f;
    readonly HashSet<Collider> roadSurfaces = new HashSet<Collider>();
    JrsVehicleController controller;
    RallyVehicleDynamics dynamics;
    Rigidbody body;
    WheelCollider[] wheels;
    float offTrackTime, roadTime;
    bool armed = true;

    public bool IsOffTrack { get; private set; }
    public int AppliedCount { get; private set; }
    public float RetainedSpeed => retainedSpeed;

    public void Configure(JrsVehicleController vehicle, RallyVehicleDynamics owner)
    {
        controller = vehicle; dynamics = owner;
        if (vehicle == null || vehicle.TryGetComponent<RallyBotController>(out _)) { enabled = false; return; }
        body = vehicle.GetComponent<Rigidbody>();
        wheels = new[] { vehicle.frontLeftWheel, vehicle.frontRightWheel, vehicle.rearLeftWheel, vehicle.rearRightWheel };
        roadSurfaces.Clear();
        foreach (GameObject root in vehicle.gameObject.scene.GetRootGameObjects())
            foreach (Collider surface in root.GetComponentsInChildren<Collider>(true))
                if (IsDrivingSurfaceName(surface.name) && !surface.isTrigger) roadSurfaces.Add(surface);
        // Fail open in unrelated test/asset scenes, not by treating every unknown surface as a penalty.
        enabled = body != null && roadSurfaces.Count > 0;
    }

    static bool IsDrivingSurfaceName(string name) => name == "Rally_Road_Start_to_Finish" ||
        name == "Circuit02_Road" || name == "Circuit03_Road" || name == "Continuous smooth deck collider";

    void FixedUpdate()
    {
        if (body == null || body.isKinematic || controller == null || !controller.enabled ||
            dynamics == null || !dynamics.enabled || wheels == null) return;
        int road = 0, other = 0;
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null || !wheel.enabled || !wheel.GetGroundHit(out WheelHit hit)) continue;
            if (roadSurfaces.Contains(hit.collider)) road++; else other++;
        }
        // Airborne/one-wheel contact is not an exit. Straddling the edge is not a confirmed exit either.
        if (road + other < 2 || road == other) { offTrackTime = roadTime = 0; return; }
        if (road > other)
        {
            offTrackTime = 0;
            roadTime += Time.fixedDeltaTime;
            if (roadTime >= roadRearmSeconds) { armed = true; IsOffTrack = false; }
            return;
        }
        roadTime = 0;
        offTrackTime += Time.fixedDeltaTime;
        if (offTrackTime < offTrackConfirmationSeconds) return;
        IsOffTrack = true;
        if (!armed) return;
        armed = false;
        Vector3 vertical = Vector3.Project(body.linearVelocity, Vector3.up);
        body.linearVelocity = vertical + Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up) * retainedSpeed;
        AppliedCount++;
    }
}
