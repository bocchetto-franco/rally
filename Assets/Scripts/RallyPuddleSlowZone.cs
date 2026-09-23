using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RallyPuddleSlowZone : MonoBehaviour
{
    [Header("Water resistance")]
    [SerializeField, Min(0f)] float brakingAcceleration = 5f;
    [SerializeField, Min(0f)] float additionalHighSpeedBraking = 2.5f;

    [Header("Light aquaplaning")]
    [SerializeField, Min(0f)] float aquaplaningStartSpeedKph = 55f;
    [SerializeField, Min(0f)] float aquaplaningFullSpeedKph = 110f;
    [SerializeField, Min(0f)] float maximumLateralAcceleration = 2.2f;

    [Header("Low-cost splash")]
    [SerializeField, Min(0f)] float splashMinimumSpeedKph = 35f;
    [SerializeField, Min(0)] int entrySplashParticles = 22;
    [SerializeField, Min(0f)] float maximumContinuousParticlesPerSecond = 36f;
    [SerializeField] ParticleSystem splashParticles;

    readonly Dictionary<Rigidbody, int> vehicleOverlaps = new Dictionary<Rigidbody, int>();
    readonly List<Rigidbody> staleBodies = new List<Rigidbody>();
    Material runtimeSplashMaterial;
    float splashEmissionAccumulator;

    void Awake()
    {
        // Migrate only the exact original value serialized in Circuit_01.
        // Later manual tuning remains untouched.
        if (Mathf.Approximately(brakingAcceleration, 3.5f))
            brakingAcceleration = 5f;

        if (splashParticles == null)
            splashParticles = CreateSplashParticles();
    }

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
        if (overlapCount == 0)
            EmitEntrySplash(body);
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
            if (horizontalVelocity.sqrMagnitude <= 0.01f)
                continue;

            float speedKph = horizontalVelocity.magnitude * 3.6f;
            float aquaplaning = CalculateAquaplaning(speedKph);

            float waterResistance = CalculateWaterResistance(speedKph);
            body.AddForce(-horizontalVelocity.normalized * waterResistance, ForceMode.Acceleration);

            // Counter part of the requested steering force, making the car
            // continue slightly straighter at speed without changing any tire
            // friction curve or fighting a normal low-speed drift.
            JrsVehicleController vehicle = body.GetComponent<JrsVehicleController>();
            float steering = SteeringInput(vehicle);
            if (aquaplaning > 0f && Mathf.Abs(steering) > 0.05f)
            {
                Vector3 roadRight = Vector3.ProjectOnPlane(body.transform.right, Vector3.up).normalized;
                body.AddForce(
                    -roadRight * steering * maximumLateralAcceleration * aquaplaning,
                    ForceMode.Acceleration);
            }

            EmitContinuousSplash(body, speedKph);
        }

        foreach (Rigidbody body in staleBodies)
            vehicleOverlaps.Remove(body);
    }

    float CalculateAquaplaning(float speedKph)
    {
        float speedRange = Mathf.Max(1f, aquaplaningFullSpeedKph - aquaplaningStartSpeedKph);
        return Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((speedKph - aquaplaningStartSpeedKph) / speedRange));
    }

    float CalculateWaterResistance(float speedKph)
    {
        return brakingAcceleration + additionalHighSpeedBraking * CalculateAquaplaning(speedKph);
    }

    void OnDestroy()
    {
        if (runtimeSplashMaterial != null)
            Destroy(runtimeSplashMaterial);
    }

    static float SteeringInput(JrsVehicleController vehicle)
    {
        if (vehicle == null || vehicle.maxSteerAngle <= 0.01f)
            return 0f;

        float left = vehicle.frontLeftWheel != null ? vehicle.frontLeftWheel.steerAngle : 0f;
        float right = vehicle.frontRightWheel != null ? vehicle.frontRightWheel.steerAngle : left;
        return Mathf.Clamp((left + right) * 0.5f / vehicle.maxSteerAngle, -1f, 1f);
    }

    void EmitEntrySplash(Rigidbody body)
    {
        if (splashParticles == null)
            return;

        float speedKph = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f;
        if (speedKph < splashMinimumSpeedKph)
            return;

        PositionSplash(body);
        float intensity = Mathf.InverseLerp(splashMinimumSpeedKph, aquaplaningFullSpeedKph, speedKph);
        if (!splashParticles.isPlaying)
            splashParticles.Play(false);
        splashParticles.Emit(Mathf.Max(1, Mathf.RoundToInt(entrySplashParticles * Mathf.Lerp(0.55f, 1f, intensity))));
    }

    void EmitContinuousSplash(Rigidbody body, float speedKph)
    {
        if (splashParticles == null || speedKph < splashMinimumSpeedKph)
            return;

        PositionSplash(body);
        float intensity = Mathf.InverseLerp(splashMinimumSpeedKph, aquaplaningFullSpeedKph, speedKph);
        splashEmissionAccumulator += Mathf.Lerp(8f, maximumContinuousParticlesPerSecond, intensity) * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(splashEmissionAccumulator);
        if (count <= 0)
            return;

        splashEmissionAccumulator -= count;
        if (!splashParticles.isPlaying)
            splashParticles.Play(false);
        splashParticles.Emit(count);
    }

    void PositionSplash(Rigidbody body)
    {
        splashParticles.transform.SetPositionAndRotation(
            body.worldCenterOfMass - body.transform.up * 0.42f,
            Quaternion.LookRotation(Vector3.up, body.transform.forward));
    }

    ParticleSystem CreateSplashParticles()
    {
        GameObject splash = new GameObject("Water Splash - Runtime");
        splash.transform.SetParent(transform, false);
        var particles = splash.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 6.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.23f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.88f, 0.94f, 0.46f),
            new Color(0.9f, 0.97f, 1f, 0.72f));
        main.gravityModifier = 0.7f;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 34f;
        shape.radius = 0.9f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.material = CreateSplashMaterial();
        return particles;
    }

    Material CreateSplashMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        runtimeSplashMaterial = new Material(shader)
        {
            name = "Puddle Splash - Runtime",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };
        Texture2D texture = Resources.GetBuiltinResource<Texture2D>("Default-Particle.png");
        if (texture != null)
            runtimeSplashMaterial.SetTexture("_BaseMap", texture);
        Color tint = new Color(0.72f, 0.9f, 0.98f, 0.62f);
        runtimeSplashMaterial.SetColor("_BaseColor", tint);
        runtimeSplashMaterial.SetColor("_Color", tint);
        runtimeSplashMaterial.SetFloat("_Surface", 1f);
        runtimeSplashMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        runtimeSplashMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        runtimeSplashMaterial.SetFloat("_ZWrite", 0f);
        runtimeSplashMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return runtimeSplashMaterial;
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
