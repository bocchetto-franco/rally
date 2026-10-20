using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A bounded world-space rain box per viewport; never renders another player's rain.</summary>
public sealed class RallyCameraRain : MonoBehaviour
{
    public Camera View { get; private set; }
    public ParticleSystem Particles { get; private set; }
    ParticleSystemRenderer rainRenderer;
    Vector3 previous;

    public void Configure(Camera camera, Material material, int budget)
    {
        View = camera;
        Particles = gameObject.AddComponent<ParticleSystem>(); Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = Particles.main; main.playOnAwake = false; main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = budget;
        main.startLifetime = .85f; main.startSpeed = new ParticleSystem.MinMaxCurve(16, 20);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .045f);
        main.startColor = new Color(.72f, .81f, .88f, .42f);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = Particles.emission; emission.rateOverTime = budget;
        var shape = Particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(26, 28, .2f);
        var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
        var color = Particles.colorOverLifetime; color.enabled = true; color.color = fade;
        rainRenderer = Particles.GetComponent<ParticleSystemRenderer>(); rainRenderer.sharedMaterial = material;
        rainRenderer.renderMode = ParticleSystemRenderMode.Stretch; rainRenderer.velocityScale = .035f; rainRenderer.lengthScale = 4;
        rainRenderer.shadowCastingMode = ShadowCastingMode.Off; rainRenderer.receiveShadows = false;
        rainRenderer.lightProbeUsage = LightProbeUsage.Off; rainRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Follow(); previous = transform.position;
        Particles.Play(false);
    }
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        if (Particles != null) Particles.Play(false);
    }
    void BeginCamera(ScriptableRenderContext context, Camera camera) { if (rainRenderer != null) rainRenderer.enabled = camera == View; }
    void EndCamera(ScriptableRenderContext context, Camera camera) { if (rainRenderer != null) rainRenderer.enabled = true; }
    void Follow()
    {
        Vector3 forward = Vector3.ProjectOnPlane(View.transform.forward, Vector3.up).normalized;
        transform.SetPositionAndRotation(View.transform.position + Vector3.up * 9f + forward * 9f,
            Quaternion.LookRotation(Vector3.down, forward.sqrMagnitude > .01f ? forward : Vector3.forward));
    }
    void LateUpdate()
    {
        if (View == null || Particles == null) return;
        Follow();
        if ((transform.position - previous).sqrMagnitude > 900f) Particles.Clear(false);
        previous = transform.position;
        var emission = Particles.emission; emission.enabled = View.isActiveAndEnabled;
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera; RenderPipelineManager.endCameraRendering -= EndCamera;
        if (Particles != null) Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
