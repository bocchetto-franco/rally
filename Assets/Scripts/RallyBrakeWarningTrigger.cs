using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyBrakeWarningTrigger : MonoBehaviour
{
    [SerializeField] RallyBrakeWarningSystem warningSystem;
    [SerializeField, Min(5f)] float targetSpeedKph = 55f;
    readonly HashSet<Collider> vehicleContacts = new HashSet<Collider>();

    public float TargetSpeedKph => targetSpeedKph;

    public void Configure(RallyBrakeWarningSystem system, float targetSpeed)
    {
        warningSystem = system;
        targetSpeedKph = Mathf.Max(5f, targetSpeed);
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!BelongsToVehicle(other)) return;
        vehicleContacts.Add(other);
        warningSystem.Enter(this);
    }

    void OnTriggerStay(Collider other)
    {
        if (!BelongsToVehicle(other)) return;
        vehicleContacts.Add(other);
        warningSystem.Enter(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!vehicleContacts.Remove(other)) return;
        if (vehicleContacts.Count == 0) warningSystem.Exit(this);
    }

    void OnDisable()
    {
        vehicleContacts.Clear();
        if (warningSystem != null) warningSystem.Exit(this);
    }

    bool BelongsToVehicle(Collider other)
    {
        if (warningSystem == null || other == null) return false;
        Rigidbody body = other.attachedRigidbody;
        return body != null && body == warningSystem.VehicleBody;
    }
}
