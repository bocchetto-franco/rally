using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Wheel-contact mud spray; no vehicle forces, grip or trigger changes.</summary>
public sealed class RallyPuddleMudVisual : MonoBehaviour
{
    public Vector3 localCenter;
    public Vector2 halfSize;
    public Material sprayMaterial;
    [Min(0f)] public float minimumSpeedKph = 15f;
    [Range(0f, 60f)] public float particlesPerWheelPerSecond = 12f;

    ParticleSystem spray;
    struct VehicleSample { public JrsVehicleController car; public Rigidbody body; }
    static VehicleSample[] vehicles;
    static float nextVehicleSearch;
    static UnityEngine.SceneManagement.Scene cachedScene;
    float emissionRemainder;
    readonly System.Random random = new System.Random();
    float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetVehicleCache() { vehicles = null; nextVehicleSearch = 0f; }

    void Start()
    {
        var go = new GameObject("Mud droplets - visual only");
        go.transform.SetParent(transform, false);
        spray = go.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = spray.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .65f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .14f);
        main.gravityModifier = 1.4f;
        main.maxParticles = 96;
        var emission = spray.emission; emission.enabled = false;
        var shape = spray.shape; shape.enabled = false;
        var color = spray.colorOverLifetime;
        color.enabled = true;
        color.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.85f, .65f), new GradientAlphaKey(0, 1) }
        };
        var renderer = spray.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sprayMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = .035f;
        renderer.lengthScale = 1.3f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void FixedUpdate()
    {
        if (spray == null || Time.timeScale <= 0f) return;
        if (vehicles == null || cachedScene != gameObject.scene || Time.time >= nextVehicleSearch)
        {
            // All puddles reuse one lookup; cache bodies instead of GetComponent in each wheel loop.
            var cars = FindObjectsByType<JrsVehicleController>();
            vehicles = new VehicleSample[cars.Length];
            for (int i = 0; i < cars.Length; i++)
                vehicles[i] = new VehicleSample { car = cars[i], body = cars[i].GetComponent<Rigidbody>() };
            cachedScene = gameObject.scene;
            nextVehicleSearch = Time.time + 5f;
        }
        foreach (var sample in vehicles)
        {
            var car = sample.car;
            if (car == null || !car.isActiveAndEnabled) continue;
            Vector3 p = transform.InverseTransformPoint(car.transform.position) - localCenter;
            if (Mathf.Abs(p.x) > halfSize.x + 3 || Mathf.Abs(p.z) > halfSize.y + 4) continue;
            var body = sample.body;
            if (body == null) continue;
            float kph = body.linearVelocity.magnitude * 3.6f;
            if (kph < minimumSpeedKph) continue;
            float amount = Mathf.Lerp(.25f, 1f, Mathf.InverseLerp(minimumSpeedKph, 100f, kph));
            Emit(car.frontLeftWheel, body, amount, -1);
            Emit(car.frontRightWheel, body, amount, 1);
            Emit(car.rearLeftWheel, body, amount, -1);
            Emit(car.rearRightWheel, body, amount, 1);
        }
    }

    void Emit(WheelCollider wheel, Rigidbody body, float amount, float side)
    {
        if (wheel == null || !wheel.GetGroundHit(out WheelHit hit)) return;
        Vector3 p = transform.InverseTransformPoint(hit.point) - localCenter;
        float x = p.x / Mathf.Max(.1f, halfSize.x), z = p.z / Mathf.Max(.1f, halfSize.y);
        // Keep spray over the water/muddy margin and require real tire contact.
        if (x*x + z*z > 1.05f || Mathf.Abs(hit.point.y - transform.TransformPoint(localCenter).y) > .8f) return;
        emissionRemainder += particlesPerWheelPerSecond * amount * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(emissionRemainder);
        emissionRemainder -= count;
        if (count == 0) return;
        if (!spray.isPlaying) spray.Play(false);
        for (int i = 0; i < count; i++)
        {
            float wetness = Range(0, 1);
            var particle = new ParticleSystem.EmitParams
            {
                position = hit.point + hit.normal * .06f,
                velocity = body.linearVelocity * .08f + body.transform.right * side * Range(1.2f, 2.8f)
                    - body.transform.forward * Range(.4f, 1.6f) + Vector3.up * Range(1.1f, 3.2f),
                startColor = Color.Lerp(new Color(.16f,.10f,.055f,.95f), new Color(.46f,.32f,.17f,.72f), wetness),
                startSize = Range(.04f,.13f), startLifetime = Range(.3f,.65f)
            };
            spray.Emit(particle, 1);
        }
    }
}
