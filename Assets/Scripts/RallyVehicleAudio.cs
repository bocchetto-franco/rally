using UnityEngine;

/// <summary>Presentation only: the controller still owns engine pitch and all physics.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class RallyVehicleAudio : MonoBehaviour
{
    [SerializeField] RallyAudioLibrary library;
    [SerializeField] AudioSource skidSource;
    [SerializeField] AudioSource impactSource;
    JrsVehicleController vehicle;
    Rigidbody body;
    RallyVehicleDynamics dynamics;
    bool paused;
    float nextImpactTime;

    public void Configure(JrsVehicleController controller)
    {
        if (vehicle != null) return;
        vehicle = controller;
        body = controller.GetComponent<Rigidbody>();
        if (library == null) library = RallyAudioLibrary.Load();
        if (library == null || body == null)
        {
            Debug.LogError("Rally audio library or player Rigidbody is missing.", this);
            enabled = false;
            return;
        }
        foreach (var candidate in FindObjectsByType<RallyVehicleDynamics>())
            if (candidate.VehicleController == controller) { dynamics = candidate; break; }

        if (controller.engineAudioSource == null)
            controller.engineAudioSource = gameObject.AddComponent<AudioSource>();
        AudioSource engine = controller.engineAudioSource;
        engine.Stop();
        engine.clip = library.engineLoop;
        engine.outputAudioMixerGroup = library.motorGroup;
        engine.loop = true;
        engine.playOnAwake = false;
        engine.spatialBlend = 0f;
        engine.dopplerLevel = 0f;
        engine.volume = library.engineVolume;
        engine.pitch = 0.65f;
        if (engine.clip != null) engine.Play();

        skidSource = CreateEffectSource(library.tyreSkidLoop, true);
        impactSource = CreateEffectSource(library.impact, false);
        skidSource.volume = 0f;
    }

    AudioSource CreateEffectSource(AudioClip clip, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.outputAudioMixerGroup = library.effectsGroup;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    void LateUpdate()
    {
        if (vehicle == null || library == null || skidSource == null) return;
        bool shouldPause = Time.timeScale <= 0f || !vehicle.isActiveAndEnabled;
        if (shouldPause != paused)
        {
            paused = shouldPause;
            if (paused)
            {
                vehicle.engineAudioSource.Pause();
                skidSource.Stop();
                skidSource.volume = 0f;
                impactSource.Stop();
            }
            else vehicle.engineAudioSource.UnPause();
        }
        if (paused) return;

        // Read the exact same hysteresis/ground-contact decision as the rear smoke.
        bool drifting = dynamics != null && dynamics.isActiveAndEnabled && dynamics.IsRearDrifting;
        float slip = Mathf.Max(LateralSlip(vehicle.rearLeftWheel), LateralSlip(vehicle.rearRightWheel));
        float target = drifting ? library.skidVolume * Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0.18f, 0.75f, slip)) : 0f;
        skidSource.volume = Mathf.MoveTowards(skidSource.volume, target, Time.deltaTime * 3f);
        if (target > 0f && !skidSource.isPlaying && skidSource.clip != null) skidSource.Play();
        else if (skidSource.volume <= 0f && skidSource.isPlaying) skidSource.Stop();
    }

    static float LateralSlip(WheelCollider wheel)
        => wheel != null && wheel.GetGroundHit(out WheelHit hit) ? Mathf.Abs(hit.sidewaysSlip) : 0f;

    void OnCollisionEnter(Collision collision)
    {
        if (library == null || impactSource == null || library.impact == null
            || Time.timeScale <= 0f || vehicle == null || !vehicle.isActiveAndEnabled
            || Time.time < nextImpactTime) return;

        float impactSpeed = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            // Ignore supporting road/terrain contact and measure only approach speed,
            // not the tangential speed of a car sliding along a barrier.
            if (Vector3.Dot(normal, Vector3.up) > 0.65f) continue;
            impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)));
        }
        if (impactSpeed < library.minimumImpactSpeed) return;
        float strength = Mathf.InverseLerp(library.minimumImpactSpeed, library.fullVolumeImpactSpeed, impactSpeed);
        impactSource.PlayOneShot(library.impact, library.impactVolume * Mathf.Lerp(0.2f, 1f, strength));
        nextImpactTime = Time.time + library.impactCooldown;
    }

    void OnDisable()
    {
        if (vehicle != null && vehicle.engineAudioSource != null) vehicle.engineAudioSource.Stop();
        if (skidSource != null) skidSource.Stop();
        if (impactSource != null) impactSource.Stop();
    }
}
