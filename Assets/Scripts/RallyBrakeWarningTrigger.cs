using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyBrakeWarningTrigger : MonoBehaviour
{
    [SerializeField] RallyBrakeWarningSystem warningSystem;
    [SerializeField, Min(5f)] float targetSpeedKph = 55f;
    readonly HashSet<Collider> vehicleContacts = new HashSet<Collider>();
    readonly Dictionary<RallyBrakeWarningSystem, HashSet<Collider>> localContacts = new Dictionary<RallyBrakeWarningSystem, HashSet<Collider>>();

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
        if (TrackLocalPlayer(other)) return;
        if (!BelongsToVehicle(other)) return;
        vehicleContacts.Add(other);
        warningSystem.Enter(this);
    }

    void OnTriggerStay(Collider other)
    {
        if (TrackLocalPlayer(other)) return;
        if (!BelongsToVehicle(other)) return;
        vehicleContacts.Add(other);
        warningSystem.Enter(this);
    }

    void OnTriggerExit(Collider other)
    {
        foreach (var entry in localContacts)
            if (entry.Value.Remove(other) && entry.Value.Count == 0) entry.Key.Exit(this);
        if (!vehicleContacts.Remove(other)) return;
        if (vehicleContacts.Count == 0) warningSystem.Exit(this);
    }

    void OnDisable()
    {
        foreach (var entry in localContacts)
            if (entry.Key != null) entry.Key.Exit(this);
        localContacts.Clear();
        vehicleContacts.Clear();
        if (warningSystem != null) warningSystem.Exit(this);
    }

    bool TrackLocalPlayer(Collider other)
    {
        var input = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<RallyLocalPlayerInput>() : null;
        if (input == null) return false;
        var system = input.BrakeWarnings;
        if (system == null) return true;
        if (!localContacts.TryGetValue(system, out var contacts))
        {
            contacts = new HashSet<Collider>();
            localContacts.Add(system, contacts);
        }
        contacts.Add(other);
        system.Enter(this);
        return true;
    }

    bool BelongsToVehicle(Collider other)
    {
        if (warningSystem == null || other == null) return false;
        Rigidbody body = other.attachedRigidbody;
        return body != null && body == warningSystem.VehicleBody;
    }
}
