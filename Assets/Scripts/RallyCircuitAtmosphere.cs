using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Scene-scoped presentation. Disable this component to restore the original lighting/materials.</summary>
[DisallowMultipleComponent]
public sealed class RallyCircuitAtmosphere : MonoBehaviour
{
    public RallyAtmosphereProfile profile;
    Light sun;
    Quaternion oldRotation;
    Color oldSun, oldSky, oldEquator, oldGround, oldFog;
    float oldIntensity, oldShadow, oldFogStart, oldFogEnd, oldDensity, oldAmbientIntensity;
    bool oldFogEnabled, captured, ready, applied;
    AmbientMode oldAmbientMode;
    FogMode oldFogMode;
    Material oldSkybox;
    readonly List<Renderer> roads = new List<Renderer>();
    readonly List<Material[]> originalMaterials = new List<Material[]>();
    readonly List<RallyCameraRain> rain = new List<RallyCameraRain>();
    public bool IsApplied => applied;
    public IReadOnlyList<RallyCameraRain> RainViews => rain;

    IEnumerator Start()
    {
        if (profile == null) yield break;
        Capture(); Apply();
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        if (profile.rain && profile.rainMaterial != null)
        {
            var cameras = new List<Camera>();
            foreach (var c in Camera.allCameras)
                if (c.gameObject.scene == gameObject.scene && c.cameraType == CameraType.Game && c.targetTexture == null) cameras.Add(c);
            foreach (var camera in cameras)
            {
                var go = new GameObject("Local Rain - " + camera.name); go.transform.SetParent(transform, false);
                var effect = go.AddComponent<RallyCameraRain>();
                effect.Configure(camera, profile.rainMaterial, cameras.Count > 1 ? profile.rainParticlesPerSplitView : profile.rainParticlesSingle);
                rain.Add(effect);
                effect.gameObject.SetActive(isActiveAndEnabled);
            }
        }
        ready = true;
        if (!isActiveAndEnabled) Restore();
    }

    void Capture()
    {
        sun = RenderSettings.sun;
        if (sun != null) { oldRotation = sun.transform.rotation; oldSun = sun.color; oldIntensity = sun.intensity; oldShadow = sun.shadowStrength; }
        oldSkybox = RenderSettings.skybox; oldAmbientMode = RenderSettings.ambientMode; oldAmbientIntensity = RenderSettings.ambientIntensity;
        oldSky = RenderSettings.ambientSkyColor; oldEquator = RenderSettings.ambientEquatorColor; oldGround = RenderSettings.ambientGroundColor;
        oldFogEnabled = RenderSettings.fog; oldFog = RenderSettings.fogColor; oldFogMode = RenderSettings.fogMode;
        oldFogStart = RenderSettings.fogStartDistance; oldFogEnd = RenderSettings.fogEndDistance; oldDensity = RenderSettings.fogDensity;
        if (profile.originalRoadMaterial != null && profile.wetRoadMaterial != null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    if (System.Array.IndexOf(materials, profile.originalRoadMaterial) < 0) continue;
                    roads.Add(renderer); originalMaterials.Add(materials);
                }
        captured = true;
    }

    void Apply()
    {
        if (!captured || applied || profile == null) return;
        if (sun != null) { sun.transform.rotation = Quaternion.Euler(profile.sunEuler); sun.color = profile.sunColor; sun.intensity = profile.sunIntensity; sun.shadowStrength = profile.shadowStrength; }
        if (profile.skybox != null) RenderSettings.skybox = profile.skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = 1;
        RenderSettings.ambientSkyColor = profile.ambientSky; RenderSettings.ambientEquatorColor = profile.ambientEquator; RenderSettings.ambientGroundColor = profile.ambientGround;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogStartDistance = profile.fogStart; RenderSettings.fogEndDistance = Mathf.Max(profile.fogStart + 1, profile.fogEnd);
        for (int i = 0; i < roads.Count; i++)
        {
            if (roads[i] == null) continue;
            var materials = (Material[])originalMaterials[i].Clone();
            for (int j = 0; j < materials.Length; j++) if (materials[j] == profile.originalRoadMaterial) materials[j] = profile.wetRoadMaterial;
            roads[i].sharedMaterials = materials;
        }
        foreach (var effect in rain) if (effect != null) effect.gameObject.SetActive(true);
        applied = true;
    }

    void Restore()
    {
        if (!captured || !applied) return;
        if (sun != null) { sun.transform.rotation = oldRotation; sun.color = oldSun; sun.intensity = oldIntensity; sun.shadowStrength = oldShadow; }
        // A scene unload must not overwrite the next scene's RenderSettings.
        if (SceneManager.GetActiveScene() == gameObject.scene)
        {
            RenderSettings.skybox = oldSkybox; RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientIntensity = oldAmbientIntensity;
            RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.fog = oldFogEnabled; RenderSettings.fogColor = oldFog; RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogStartDistance = oldFogStart; RenderSettings.fogEndDistance = oldFogEnd; RenderSettings.fogDensity = oldDensity;
        }
        for (int i = 0; i < roads.Count; i++) if (roads[i] != null) roads[i].sharedMaterials = originalMaterials[i];
        foreach (var effect in rain) if (effect != null) effect.gameObject.SetActive(false);
        applied = false;
    }
    void OnEnable() { if (ready) Apply(); }
    void OnDisable() => Restore();
}
