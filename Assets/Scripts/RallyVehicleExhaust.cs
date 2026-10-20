using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Purely visual exhaust, shared by all player variants and the independent bot inputs.</summary>
[DefaultExecutionOrder(1050), DisallowMultipleComponent]
[RequireComponent(typeof(JrsVehicleController), typeof(Rigidbody))]
public sealed class RallyVehicleExhaust : MonoBehaviour
{
    [SerializeField, Min(0)] float minimumSpeedKph = 4f;
    [SerializeField, Min(0)] float backfireSpeedKph = 60f;
    [SerializeField, Min(0)] float fullThrottleRate = 12f;
    [SerializeField, Min(0)] float liftRate = 30f;
    [SerializeField] bool backfireEnabled = true;
    [SerializeField] Vector3 nozzleOffset = new Vector3(-.5f, .2f, -.035f);
    [SerializeField] float currentThrottle;
    [SerializeField] float currentEmission;
    public float CurrentEmission => currentEmission;
    public ParticleSystem Smoke => smoke;
    public ParticleSystem Backfire => flash;
    public int LiftEvents { get; private set; }
    public int ShiftEvents { get; private set; }

    JrsVehicleController car;
    Rigidbody body;
    RallyVehicleVisual visual;
    ParticleSystem smoke, flash;
    Material material;
    Camera[] cameras = new Camera[4];
    bool visible, throttleLoaded;
    float nextVisibility, puffUntil, nextPuff;
    int previousGear;
    Vector3 previousPosition;

    void Awake()
    {
        car = GetComponent<JrsVehicleController>(); body = GetComponent<Rigidbody>();
        visual = GetComponentInChildren<RallyVehicleVisual>();
        var source = car.rearLeftDustParticleSystem != null ? car.rearLeftDustParticleSystem.GetComponent<ParticleSystemRenderer>() : null;
        if (source == null || source.sharedMaterial == null) { enabled = false; return; }
        material = new Material(source.sharedMaterial) { name = "Exhaust - existing smoke shader" };
        if (material.HasProperty("_SoftParticleFadeParams")) material.SetVector("_SoftParticleFadeParams", new Vector4(0, 2.5f, 0, 0));
        smoke = Create("Exhaust Smoke", false); flash = Create("Exhaust Backfire", true);
        previousPosition = transform.position; previousGear = car.PresentationGear;
        PositionNozzle();
    }

    void PositionNozzle()
    {
        if (smoke == null) return;
        // Bounds are authored in the selected visual's metres, not the historical Porsche rig.
        Bounds bounds = visual != null ? visual.bodyBounds : new Bounds(Vector3.up * .8f, new Vector3(1.8f, 1.3f, 4f));
        Vector3 local = new Vector3(bounds.center.x + nozzleOffset.x, bounds.min.y + nozzleOffset.y, bounds.min.z + nozzleOffset.z);
        Transform basis = visual != null ? visual.transform : transform;
        Vector3 position = basis.TransformPoint(local);
        Quaternion rotation = Quaternion.LookRotation(-transform.forward + transform.up * .08f, transform.up);
        smoke.transform.SetPositionAndRotation(position, rotation); flash.transform.SetPositionAndRotation(position, rotation);
    }

    void LateUpdate()
    {
        if (smoke == null) return;
        PositionNozzle();
        currentThrottle = car.PresentationThrottle;
        float speed = body.linearVelocity.magnitude * 3.6f;
        if (Time.unscaledTime >= nextVisibility)
        {
            nextVisibility = Time.unscaledTime + .25f; visible = false;
            if (Camera.allCamerasCount > cameras.Length) cameras = new Camera[Camera.allCamerasCount];
            int count = Camera.GetAllCameras(cameras);
            for (int i = 0; i < count; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.targetTexture != null || camera.gameObject.scene != gameObject.scene || camera.cameraType != CameraType.Game) continue;
                if ((camera.transform.position - transform.position).sqrMagnitude > 6400f) continue;
                Vector3 viewport = camera.WorldToViewportPoint(transform.position);
                if (viewport.z > 0 && viewport.x > -.15f && viewport.x < 1.15f && viewport.y > -.15f && viewport.y < 1.15f) { visible = true; break; }
            }
        }
        bool teleported = (transform.position - previousPosition).sqrMagnitude > 225f;
        bool upright = Vector3.Dot(transform.up, Vector3.up) > .5f;
        bool allowed = visible && car.isActiveAndEnabled && !body.isKinematic && upright && speed > minimumSpeedKph &&
            Time.timeScale > 0 && !RallyRaceLoading.IsPreparing && !teleported;
        if (teleported || !upright || speed <= minimumSpeedKph) { smoke.Clear(false); flash.Clear(false); }
        if (allowed)
        {
            // Arm above load and release below idle: analog triggers often take several frames to lift.
            if (currentThrottle > .65f) throttleLoaded = true;
            bool lift = throttleLoaded && currentThrottle < .2f;
            if (lift) throttleLoaded = false;
            bool shift = previousGear != car.PresentationGear && currentThrottle > .5f;
            if ((lift || shift) && Time.time >= nextPuff)
            {
                nextPuff = Time.time + .35f; puffUntil = Time.time + .45f;
                smoke.Emit(lift ? 7 : 5);
                if (lift) LiftEvents++; if (shift) ShiftEvents++;
                if (lift && backfireEnabled && speed >= backfireSpeedKph) { flash.Play(false); flash.Emit(2); }
            }
            currentEmission = Time.time < puffUntil ? liftRate : fullThrottleRate * currentThrottle;
        }
        else { currentEmission = 0; puffUntil = 0; throttleLoaded = false; }
        var emission = smoke.emission; emission.rateOverTime = currentEmission;
        if (currentEmission > 0 && !smoke.isPlaying) smoke.Play(false);
        else if (currentEmission <= 0 && smoke.isPlaying) smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        previousPosition = transform.position; previousGear = car.PresentationGear;
    }

    ParticleSystem Create(string label, bool flame)
    {
        var go = new GameObject(label); go.SetActive(false); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.playOnAwake = false; main.loop = !flame;
        if (flame) main.duration = .15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = flame ? 6 : 40;
        main.startLifetime = new ParticleSystem.MinMaxCurve(flame ? .06f : .3f, flame ? .12f : .65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, flame ? 1.2f : 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(flame ? .07f : .08f, flame ? .14f : .16f);
        main.startColor = flame ? new Color(1f, .36f, .04f, .9f) : new Color(.51f, .58f, .67f, .32f);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ps.emission; emission.enabled = !flame; emission.rateOverTime = 0;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .025f; shape.angle = 12;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, flame ? .15f : 2.4f));
        var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        var color = ps.colorOverLifetime; color.enabled = true; color.color = fade;
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = flame ? 0 : .18f;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        go.SetActive(true); return ps;
    }

    void OnDisable()
    {
        currentEmission = 0;
        if (smoke != null) smoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (flash != null) flash.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
