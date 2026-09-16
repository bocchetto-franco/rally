using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyPuddleSlowZone : MonoBehaviour
{
    [SerializeField, Min(0f)] float brakingAcceleration = 3.5f;

    readonly Dictionary<Rigidbody, int> vehicleOverlaps = new Dictionary<Rigidbody, int>();
    readonly List<Rigidbody> staleBodies = new List<Rigidbody>();

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Rigidbody body = GetVehicleBody(other);
        if (body == null)
            return;

        vehicleOverlaps.TryGetValue(body, out int overlapCount);
        vehicleOverlaps[body] = overlapCount + 1;
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody body = GetVehicleBody(other);
        if (body == null || !vehicleOverlaps.TryGetValue(body, out int overlapCount))
            return;

        if (overlapCount <= 1)
            vehicleOverlaps.Remove(body);
        else
            vehicleOverlaps[body] = overlapCount - 1;
    }

    void FixedUpdate()
    {
        staleBodies.Clear();
        foreach (Rigidbody body in vehicleOverlaps.Keys)
        {
            if (body == null)
            {
                staleBodies.Add(body);
                continue;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude > 0.01f)
                body.AddForce(-horizontalVelocity.normalized * brakingAcceleration, ForceMode.Acceleration);
        }

        foreach (Rigidbody body in staleBodies)
            vehicleOverlaps.Remove(body);
    }

    static Rigidbody GetVehicleBody(Collider other)
    {
        if (other == null)
            return null;

        JrsVehicleController vehicle = other.GetComponentInParent<JrsVehicleController>();
        if (vehicle == null)
            return null;

        return other.attachedRigidbody != null
            ? other.attachedRigidbody
            : vehicle.GetComponent<Rigidbody>();
    }
}
