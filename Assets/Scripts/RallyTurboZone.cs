using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public sealed class RallyTurboZone : MonoBehaviour
{
    [Header("Precise entry - vehicle centre must cross this lane")]
    [SerializeField, Min(.1f)] float activationHalfWidth = .7f;
    [SerializeField, Min(0f)] float minimumSpeedKph = 12f;
    [SerializeField, Range(0f, 1f)] float minimumForwardAlignment = .5f;
    [Header("Temporary boost - same for players and bots")]
    [SerializeField, Min(.1f)] float durationSeconds = 2.25f;
    [SerializeField, Min(0f)] float accelerationMps2 = 4.5f;
    [SerializeField, Min(1f)] float maximumSpeedKph = 180f;
    [SerializeField, Min(0f)] float botSpeedBonusKph = 35f;
    [SerializeField, Min(.1f)] float rearmSeconds = 8f;
    readonly Dictionary<Rigidbody, float> lastPass = new Dictionary<Rigidbody, float>(4);
    BoxCollider trigger;
    public float ActivationHalfWidth => activationHalfWidth;

    void Awake() => trigger = GetComponent<BoxCollider>();

    IEnumerator Start()
    {
        // Build receivers while the loading gate is up, not on first contact.
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        foreach (var vehicle in RallyVehiclePresentationCache.ForScene(gameObject.scene))
            if (vehicle.car.GetComponent<RallyVehicleTurbo>() == null) vehicle.car.gameObject.AddComponent<RallyVehicleTurbo>();
    }

    void OnTriggerEnter(Collider other) => TryActivate(other.attachedRigidbody);
    void OnTriggerStay(Collider other) => TryActivate(other.attachedRigidbody);

    public bool TryActivate(Rigidbody body)
    {
        if (body == null || trigger == null || !trigger.enabled || !isActiveAndEnabled ||
            RallyRaceLoading.IsPreparing || Time.timeScale <= 0f) return false;
        Vector3 local = transform.InverseTransformPoint(body.position) - trigger.center;
        Vector3 half = trigger.size * .5f;
        // Collider overlap alone is deliberately insufficient: wheels/body corners
        // brushing the lane must not activate it. Stay also catches the root later.
        if (Mathf.Abs(local.x) > activationHalfWidth || Mathf.Abs(local.z) > half.z || Mathf.Abs(local.y) > half.y) return false;
        Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, transform.up);
        if (velocity.magnitude * 3.6f < minimumSpeedKph || Vector3.Dot(velocity.normalized, transform.forward) < minimumForwardAlignment) return false;
        if (lastPass.TryGetValue(body, out float last) && Time.time < last + rearmSeconds) return false;
        if (!body.TryGetComponent<RallyVehicleTurbo>(out var turbo) ||
            !turbo.Activate(durationSeconds, accelerationMps2, maximumSpeedKph, botSpeedBonusKph)) return false;
        lastPass[body] = Time.time;
        return true;
    }

    void OnValidate()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        activationHalfWidth = Mathf.Clamp(activationHalfWidth, .1f, Mathf.Max(.1f, box.size.x * .5f));
    }
}
