using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Light driving dust, independent from the rear-wheel drift smoke.</summary>
[DefaultExecutionOrder(950)]
[DisallowMultipleComponent]
[RequireComponent(typeof(JrsVehicleController), typeof(Rigidbody))]
public sealed class RallyVehicleDust : MonoBehaviour
{
    [SerializeField, Min(0f)] float minimumSpeedKph = 4f;
    [SerializeField, Min(5f)] float fullIntensitySpeedKph = 110f;
    [SerializeField, Min(0f)] float minimumEmission = .5f;
    [SerializeField, Min(0f)] float maximumEmission = 10f;

    JrsVehicleController controller;
    Rigidbody body;
    ParticleSystem leftDust;
    ParticleSystem rightDust;
    Material dustMaterial;

    void Awake()
    {
        controller = GetComponent<JrsVehicleController>();
        body = GetComponent<Rigidbody>();
        leftDust = CreateEmitter("Rear Left Driving Dust", controller.rearLeftDustParticleSystem);
        rightDust = CreateEmitter("Rear Right Driving Dust", controller.rearRightDustParticleSystem);
    }

    void LateUpdate()
    {
        float speedKph = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f;
        bool moving = Time.timeScale > 0f && controller.isActiveAndEnabled &&
            !body.isKinematic && speedKph > minimumSpeedKph;
        float blend = Mathf.InverseLerp(minimumSpeedKph, Mathf.Max(minimumSpeedKph + 1f, fullIntensitySpeedKph), speedKph);
        float rate = moving ? Mathf.Lerp(minimumEmission, maximumEmission, blend) : 0f;
        UpdateEmitter(controller.rearLeftWheel, leftDust, rate);
        UpdateEmitter(controller.rearRightWheel, rightDust, rate);
    }

    void UpdateEmitter(WheelCollider wheel, ParticleSystem dust, float rate)
    {
        if (dust == null) return;
        WheelHit contact = default;
        bool grounded = wheel != null && wheel.GetGroundHit(out contact);
        if (grounded)
        {
            Vector3 up = contact.normal.normalized;
            Vector3 heading = Vector3.ProjectOnPlane(controller.transform.forward, up);
            if (heading.sqrMagnitude < .001f) heading = Vector3.ProjectOnPlane(controller.transform.right, up);
            dust.transform.SetPositionAndRotation(contact.point + up * .06f, Quaternion.LookRotation(up, heading));
        }
        var emission = dust.emission;
        emission.rateOverTime = grounded ? rate : 0f;
        if (grounded && rate > 0f)
        {
            if (!dust.isPlaying) dust.Play(false);
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
        main.maxParticles = 28;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .4f);
        main.startSize = new ParticleSystem.MinMaxCurve(.18f, .4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.48f, .39f, .27f, .09f), new Color(.65f, .55f, .38f, .14f));
        var emission = dust.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = .08f;
        var color = dust.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);
        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .8f, 1f, 1.45f));
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
        if (leftDust != null) leftDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (rightDust != null) rightDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (dustMaterial != null) Destroy(dustMaterial);
    }
}
