using UnityEngine;
using UnityEngine.Rendering;

/// <summary>World-space driving trail, sharing the existing drift texture and road contacts.</summary>
[DefaultExecutionOrder(950)]
[DisallowMultipleComponent]
[RequireComponent(typeof(JrsVehicleController), typeof(Rigidbody))]
public sealed class RallyVehicleDust : MonoBehaviour
{
    [SerializeField, Min(0f)] float minimumSpeedKph = 4f;
    [SerializeField, Min(5f)] float fullIntensitySpeedKph = 110f;
    [SerializeField, Min(0f)] float minimumEmission = .25f;
    [SerializeField, Min(0f)] float maximumEmission = 24f;
    [SerializeField, Min(0f)] float fullTurnEmissionBonus = 48f;
    [SerializeField, Min(1f)] float maximumEmissionPerWheel = 70f;
    [SerializeField, Range(1f, 2f)] float fullTurnSizeMultiplier = 1.65f;
    [SerializeField, Min(10f)] float maximumEffectDistance = 90f;

    [Header("Runtime readings (updated after the vehicle controller)")]
    [SerializeField] float currentSpeedKph;
    [SerializeField] float currentSteerAngle;
    [SerializeField, Range(0f, 1f)] float currentSteering;
    [SerializeField] float currentRearEmission;

    JrsVehicleController controller;
    Rigidbody body;
    ParticleSystem leftDust;
    ParticleSystem rightDust;
    ParticleSystem leftCornerDust;
    ParticleSystem rightCornerDust;
    Material dustMaterial;
    RallyOffTrackSpeedPenalty roadContacts;
    Camera[] cameras = new Camera[4];
    float nextVisibilityCheck;
    bool visible;
    Vector3 lastPosition;

    void Awake()
    {
        controller = GetComponent<JrsVehicleController>();
        body = GetComponent<Rigidbody>();
        leftDust = CreateEmitter("Rear Left Driving Dust", controller.rearLeftDustParticleSystem);
        rightDust = CreateEmitter("Rear Right Driving Dust", controller.rearRightDustParticleSystem);
        leftCornerDust = CreateEmitter("Front Left Corner Dust", controller.rearLeftDustParticleSystem);
        rightCornerDust = CreateEmitter("Front Right Corner Dust", controller.rearRightDustParticleSystem);
        lastPosition = transform.position;
    }

    void Start()
    {
        // Dynamics has already configured the shared road cache in its Start.
        // Reuse it rather than searching the scene from each wheel/frame.
        roadContacts = GetComponent<RallyOffTrackSpeedPenalty>();
    }

    // Also used by the existing rear slip smoke; its thresholds remain independent.
    public bool CanEmitAt(Collider surface) => roadContacts != null &&
        roadContacts.IsDrivingSurface(surface) && EmissionAllowed();

    bool EmissionAllowed()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || RallyRaceLoading.IsPreparing ||
            controller == null || !controller.isActiveAndEnabled || body == null || body.isKinematic ||
            Vector3.Dot(body.rotation * Vector3.up, Vector3.up) <= .5f) return false;
        if (Time.unscaledTime >= nextVisibilityCheck)
        {
            nextVisibilityCheck = Time.unscaledTime + .2f;
            int count = Camera.allCamerasCount;
            if (count > cameras.Length) cameras = new Camera[count];
            count = Camera.GetAllCameras(cameras);
            visible = false;
            for (int i = 0; i < count; i++)
            {
                if (CameraCanSee(cameras[i])) { visible = true; break; }
            }
        }
        return visible;
    }

    bool CameraCanSee(Camera camera)
    {
        if (camera == null || !camera.isActiveAndEnabled || camera.cameraType != CameraType.Game ||
            camera.targetTexture != null || camera.gameObject.scene != gameObject.scene ||
            (camera.cullingMask & (1 << gameObject.layer)) == 0) return false;
        float range = Mathf.Min(maximumEffectDistance, camera.farClipPlane);
        if ((camera.transform.position - transform.position).sqrMagnitude > range * range) return false;
        Vector3 viewport = camera.WorldToViewportPoint(transform.position);
        return viewport.z > 0f && viewport.x > -.2f && viewport.x < 1.2f && viewport.y > -.2f && viewport.y < 1.2f;
    }

    void Update()
    {
        float speedKph = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f;
        currentSpeedKph = speedKph;
        // A checkpoint reset must not leave a cloud at the previous location.
        if ((transform.position - lastPosition).sqrMagnitude > 225f) ClearTrail();
        lastPosition = transform.position;
        bool moving = speedKph > minimumSpeedKph && EmissionAllowed();
        float blend = Mathf.InverseLerp(minimumSpeedKph, Mathf.Max(minimumSpeedKph + 1f, fullIntensitySpeedKph), speedKph);
        blend = Mathf.SmoothStep(0f, 1f, blend);
        // Read this car's live WheelColliders after Update/FixedUpdate. A shared
        // keyboard/gamepad value would mix the two local players' effects.
        currentSteerAngle = (controller.frontLeftWheel.steerAngle + controller.frontRightWheel.steerAngle) * .5f;
        float steering = Mathf.Clamp01(Mathf.Abs(currentSteerAngle) / Mathf.Max(1f, controller.maxSteerAngle));
        currentSteering = steering;
        // Automatic braking reduces speed in a tight turn. A purely multiplied
        // speed rate became weaker than a fast straight despite stronger steering.
        float turnSpeed = Mathf.InverseLerp(8f, 35f, speedKph);
        float rate = moving ? Mathf.Min(maximumEmissionPerWheel,
            Mathf.Lerp(minimumEmission, maximumEmission, blend) + fullTurnEmissionBonus * steering * turnSpeed) : 0f;
        currentRearEmission = rate;
        float growth = Mathf.Lerp(1f, fullTurnSizeMultiplier, steering);
        // Steering changes the size of new particles too, not only their later expansion.
        float particleSize = Mathf.Lerp(.22f, .85f, blend) * Mathf.Lerp(1f, 1.2f, steering);
        UpdateEmitter(controller.rearLeftWheel, leftDust, rate, particleSize, blend, growth);
        UpdateEmitter(controller.rearRightWheel, rightDust, rate, particleSize, blend, growth);

        // Reuse the two existing corner emitters, keeping the main trail behind.
        float cornerRate = moving && speedKph > 8f ? Mathf.Min(10f, rate * .25f * steering) : 0f;
        UpdateEmitter(controller.frontLeftWheel, leftCornerDust, cornerRate, particleSize * .7f, blend, growth);
        UpdateEmitter(controller.frontRightWheel, rightCornerDust, cornerRate, particleSize * .7f, blend, growth);
        GateLegacyFrontSmoke(controller.frontLeftWheel, controller.frontLeftDustParticleSystem, moving);
        GateLegacyFrontSmoke(controller.frontRightWheel, controller.frontRightDustParticleSystem, moving);
    }

    void GateLegacyFrontSmoke(WheelCollider wheel, ParticleSystem smoke, bool moving)
    {
        if (smoke == null || !smoke.isPlaying) return;
        if (!moving || wheel == null || !wheel.GetGroundHit(out WheelHit hit) || !CanEmitAt(hit.collider))
            smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void UpdateEmitter(WheelCollider wheel, ParticleSystem dust, float rate, float particleSize, float speedBlend, float growth)
    {
        if (dust == null) return;
        // Keep existing particles responsive even if this wheel briefly loses
        // contact at a road edge; contact still gates all new emission below.
        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.sizeMultiplier = growth;
        if (rate <= 0f)
        {
            var stoppedEmission = dust.emission;
            stoppedEmission.rateOverTime = 0f;
            if (dust.isPlaying) dust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            return;
        }
        WheelHit contact = default;
        bool grounded = wheel != null && wheel.enabled && wheel.GetGroundHit(out contact) && CanEmitAt(contact.collider);
        if (grounded)
        {
            Vector3 up = contact.normal.normalized;
            Vector3 heading = Vector3.ProjectOnPlane(controller.transform.forward, up);
            if (heading.sqrMagnitude < .001f) heading = Vector3.ProjectOnPlane(controller.transform.right, up);
            dust.transform.SetPositionAndRotation(contact.point + up * .06f, Quaternion.LookRotation(up, heading));
            var main = dust.main;
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * .65f, particleSize);
            // World-space particles linger where the tire passed, with a small
            // backward/outward drift rather than travelling with the chassis.
            Vector3 drift = -heading.normalized * Mathf.Lerp(.35f, .8f, speedBlend);
            var velocity = dust.velocityOverLifetime;
            velocity.x = drift.x; velocity.z = drift.z;
        }
        var emission = dust.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(grounded ? rate : 0f);
        if (grounded && rate > 0f)
        {
            if (!dust.isEmitting) dust.Play(false);
        }
        else if (dust.isPlaying)
            dust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    ParticleSystem CreateEmitter(string emitterName, ParticleSystem driftSmoke)
    {
        var originalRenderer = driftSmoke != null ? driftSmoke.GetComponent<ParticleSystemRenderer>() : null;
        if (originalRenderer == null || originalRenderer.sharedMaterial == null) return null;
        if (dustMaterial == null)
        {
            // Share the imported texture/shader, but keep the small dust close to
            // the ground visible without editing the drift smoke's material.
            dustMaterial = new Material(originalRenderer.sharedMaterial) { name = "Driving Dust - runtime" };
            if (dustMaterial.HasProperty("_SoftParticleFadeParams"))
                dustMaterial.SetVector("_SoftParticleFadeParams", new Vector4(0f, 2.5f, 0f, 0f));
        }
        var emitterObject = new GameObject(emitterName);
        emitterObject.SetActive(false);
        emitterObject.transform.SetParent(transform, false);
        var dust = emitterObject.AddComponent<ParticleSystem>();
        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = false;
        main.maxParticles = emitterName.StartsWith("Rear") ? 96 : 24;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.85f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .55f);
        main.startSize = new ParticleSystem.MinMaxCurve(.18f, .4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.48f, .36f, .19f, .24f), new Color(.69f, .55f, .32f, .38f));
        var emission = dust.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = .10f;
        var velocity = dust.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = .18f;
        var color = dust.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);
        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .65f, 1f, 1.9f));
        var inherit = dust.inheritVelocity;
        inherit.enabled = true;
        inherit.mode = ParticleSystemInheritVelocityMode.Initial;
        inherit.curve = .05f;
        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        emitterObject.SetActive(true);
        return dust;
    }

    void OnDisable()
    {
        ClearTrail();
        visible = false;
        nextVisibilityCheck = 0f;
    }

    void ClearTrail()
    {
        if (leftDust != null) leftDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (rightDust != null) rightDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (leftCornerDust != null) leftCornerDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (rightCornerDust != null) rightCornerDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (dustMaterial != null) Destroy(dustMaterial);
    }
}
