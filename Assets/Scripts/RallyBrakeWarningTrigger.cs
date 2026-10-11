using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyBrakeWarningTrigger : MonoBehaviour
{
    public enum TurnDirection { Left, Right }
    [SerializeField] RallyBrakeWarningSystem warningSystem;
    [SerializeField, Min(5f)] float targetSpeedKph = 55f;
    [SerializeField] TurnDirection turnDirection;
    [SerializeField, Range(1, 6), Tooltip("1 = muy cerrada; 6 = muy abierta.")] int cornerGrade = 3;
    [SerializeField] bool hairpin;
    [SerializeField] Vector3 curveEntry;
    readonly HashSet<Collider> vehicleContacts = new HashSet<Collider>(8);
    readonly Dictionary<RallyBrakeWarningSystem, HashSet<Collider>> localContacts = new Dictionary<RallyBrakeWarningSystem, HashSet<Collider>>(2);
    readonly Dictionary<Rigidbody, RallyLocalPlayerInput> localInputs = new Dictionary<Rigidbody, RallyLocalPlayerInput>(8);

    IEnumerator Start()
    {
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        // Reserve each player's contacts before their first note trigger.
        foreach (var entry in RallyVehiclePresentationCache.ForScene(gameObject.scene))
        {
            if (entry.body == null) continue;
            localInputs[entry.body] = entry.input;
            if (entry.input != null && entry.input.BrakeWarnings != null && !localContacts.ContainsKey(entry.input.BrakeWarnings))
                localContacts.Add(entry.input.BrakeWarnings, new HashSet<Collider>(8));
        }
    }

    public float TargetSpeedKph => targetSpeedKph;
    public TurnDirection Direction => turnDirection;
    public int CornerGrade => Mathf.Clamp(cornerGrade, 1, 6);
    public bool IsHairpin => hairpin;
    public Vector3 CurveEntry => curveEntry;
    static readonly string[] RightLabels = { "DERECHA 1", "DERECHA 2", "DERECHA 3", "DERECHA 4", "DERECHA 5", "DERECHA 6" };
    static readonly string[] LeftLabels = { "IZQUIERDA 1", "IZQUIERDA 2", "IZQUIERDA 3", "IZQUIERDA 4", "IZQUIERDA 5", "IZQUIERDA 6" };
    public string NoteLabel => (turnDirection == TurnDirection.Right ? RightLabels : LeftLabels)[CornerGrade - 1];

    public void Configure(RallyBrakeWarningSystem system, float targetSpeed)
    {
        warningSystem = system;
        targetSpeedKph = Mathf.Max(5f, targetSpeed);
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    public void ConfigureNote(TurnDirection direction, int grade, bool isHairpin, Vector3 entry)
    {
        turnDirection = direction;
        cornerGrade = Mathf.Clamp(grade, 1, 6);
        hairpin = isHairpin;
        curveEntry = entry;
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
        foreach (var entry in localContacts) entry.Value.Clear();
        vehicleContacts.Clear();
        if (warningSystem != null) warningSystem.Exit(this);
    }

    bool TrackLocalPlayer(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null) return false;
        if (!localInputs.TryGetValue(body, out var input))
        {
            input = body.GetComponent<RallyLocalPlayerInput>();
            localInputs.Add(body, input);
        }
        if (input == null) return false;
        var system = input.BrakeWarnings;
        if (system == null) return true;
        if (!localContacts.TryGetValue(system, out var contacts))
        {
            contacts = new HashSet<Collider>(8);
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
