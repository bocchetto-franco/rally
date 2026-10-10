using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>A slow real-time tour of a stripped visual copy, not an active race.</summary>
public sealed class RallyMenuBackdrop : MonoBehaviour
{
    RallyMenuBackdropScene environment;
    Camera cameraView;
    string requestedTrack;
    int revision;
    float angle;
    Terrain[] terrains;
    readonly HashSet<AsyncInstantiateOperation<GameObject>> instantiations = new HashSet<AsyncInstantiateOperation<GameObject>>();
    RenderTexture backgroundTarget;
    RawImage backgroundImage;
    int screenWidth, screenHeight;

    public void Show(string track)
    {
        if (requestedTrack == track) return;
        requestedTrack = track;
        foreach (var pending in instantiations)
            if (!pending.isDone) pending.Cancel();
        StartCoroutine(Load(track, ++revision));
    }

    IEnumerator Load(string track, int request)
    {
        // Coalesce rapid selection changes instead of loading every D-Pad tap.
        yield return new WaitForSecondsRealtime(.12f);
        if (request != revision) yield break;
        ResourceRequest load = Resources.LoadAsync<GameObject>("MenuBackdrops/" + track);
        yield return load;
        if (request != revision || load.asset == null) yield break;
        var operation = Object.InstantiateAsync((GameObject)load.asset, transform);
        instantiations.Add(operation);
        yield return operation;
        instantiations.Remove(operation);
        if (operation.Result == null || operation.Result.Length == 0) yield break;
        GameObject instance = operation.Result[0];
        if (request != revision) { instance.SetActive(false); Destroy(instance); yield break; }
        if (environment != null)
        {
            environment.gameObject.SetActive(false);
            Destroy(environment.gameObject);
        }
        environment = instance.GetComponent<RallyMenuBackdropScene>();
        if (environment == null) { Destroy(instance); yield break; }
        terrains = environment.GetComponentsInChildren<Terrain>();
        foreach (Terrain terrain in terrains)
        {
            // Instance-only menu budget: never edit the source TerrainData/scene.
            terrain.heightmapPixelError = Mathf.Max(terrain.heightmapPixelError, 24f);
            terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, 35f);
            terrain.detailObjectDensity *= .6f;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
        }
        cameraView = Camera.main;
        if (cameraView == null || environment == null) yield break;
        cameraView.cullingMask = 1 << 31;
        cameraView.clearFlags = CameraClearFlags.Skybox;
        cameraView.nearClipPlane = .3f;
        cameraView.farClipPlane = 1000f;
        cameraView.fieldOfView = 55f;
        cameraView.allowHDR = false;
        cameraView.allowMSAA = false;
        var cameraData = cameraView.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.volumeLayerMask = 0;
        cameraData.antialiasing = AntialiasingMode.None;
        // Keep the samples required by the existing water; only the backdrop is lower-res.
        cameraData.requiresDepthTexture = true;
        cameraData.requiresColorTexture = true;
        PrepareBackgroundTarget();
        RenderSettings.skybox = environment.skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = environment.ambientSky;
        RenderSettings.ambientEquatorColor = environment.ambientEquator;
        RenderSettings.ambientGroundColor = environment.ambientGround;
        RenderSettings.ambientIntensity = environment.ambientIntensity;
        RenderSettings.fog = environment.fog;
        RenderSettings.fogColor = environment.fogColor;
        RenderSettings.fogDensity = environment.fogDensity;
        RenderSettings.fogMode = environment.fogMode;
        angle = 0f;
        MoveCamera();
    }

    void LateUpdate()
    {
        if (environment == null || cameraView == null) return;
        if (screenWidth != Screen.width || screenHeight != Screen.height) PrepareBackgroundTarget();
        angle = Mathf.Repeat(angle + Time.unscaledDeltaTime * .03f, Mathf.PI * 2f);
        MoveCamera();
    }

    void MoveCamera()
    {
        Vector3 offset = environment.cameraPosition - environment.focus;
        // Stay near the art-directed view rather than orbiting behind a mountain.
        Vector3 position = environment.focus + Quaternion.AngleAxis(Mathf.Sin(angle) * 12f, Vector3.up) * offset;
        foreach (Terrain terrain in terrains)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                position.y = Mathf.Max(position.y, terrain.SampleHeight(position) + terrain.transform.position.y + 12f);
        }
        cameraView.transform.SetPositionAndRotation(position, Quaternion.LookRotation(environment.focus - position));
    }

    void PrepareBackgroundTarget()
    {
        screenWidth = Screen.width; screenHeight = Screen.height;
        float scale = Mathf.Min(1f, Mathf.Min(1280f / Mathf.Max(1, screenWidth), 720f / Mathf.Max(1, screenHeight)));
        int width = Mathf.Max(32, Mathf.RoundToInt(screenWidth * scale));
        int height = Mathf.Max(32, Mathf.RoundToInt(screenHeight * scale));
        if (backgroundImage == null)
        {
            var canvasObject = new GameObject("Menu Background Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
            var imageObject = new GameObject("Animated Background", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            backgroundImage = imageObject.GetComponent<RawImage>(); backgroundImage.raycastTarget = false;
            var rect = backgroundImage.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (backgroundTarget != null)
        {
            cameraView.targetTexture = null; backgroundTarget.Release(); Destroy(backgroundTarget);
        }
        backgroundTarget = new RenderTexture(width, height, 24) { name = "Menu background - bounded 720p", antiAliasing = 1 };
        backgroundTarget.Create(); cameraView.targetTexture = backgroundTarget;
        backgroundImage.texture = backgroundTarget;
    }

    void OnDestroy()
    {
        revision++;
        foreach (var pending in instantiations)
            if (!pending.isDone) pending.Cancel();
        instantiations.Clear();
        if (backgroundTarget != null)
        {
            if (cameraView != null && cameraView.targetTexture == backgroundTarget) cameraView.targetTexture = null;
            backgroundTarget.Release(); Destroy(backgroundTarget);
        }
    }
}
