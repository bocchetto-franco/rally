using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Render-only race budget, including cameras cloned for local multiplayer.</summary>
[DisallowMultipleComponent]
public sealed class RallyRenderPerformance : MonoBehaviour
{
    UniversalRenderPipelineAsset pipeline;
    float previousScale, previousShadowDistance;
    float previousLodBias;
    bool captured;
    float nextUpdate;
    struct View { public Camera camera; public UniversalAdditionalCameraData data; }
    View[] views;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0) return;
        var root = new GameObject("Race Render Budget");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<RallyRenderPerformance>();
    }

    IEnumerator Start()
    {
        while (!RallyVehiclePresentationCache.IsRosterReady(gameObject.scene)) yield return null;
        previousLodBias = QualitySettings.lodBias;
        captured = true;
        pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            previousScale = pipeline.renderScale;
            previousShadowDistance = pipeline.shadowDistance;
        }
        Camera[] cameras = Camera.allCameras;
        var list = new System.Collections.Generic.List<View>(2);
        foreach (Camera camera in cameras)
            if (camera.gameObject.scene == gameObject.scene && camera.cameraType == CameraType.Game)
                list.Add(new View { camera = camera, data = camera.GetUniversalAdditionalCameraData() });
        views = list.ToArray();
        Apply();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 2f;
        Apply(); // Split-screen creates the second rig during Start.
    }

    void Apply()
    {
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool("Rally.RenderBenchmark.Running", false) &&
            UnityEditor.SessionState.GetString("Rally.RenderBenchmark.Label", "") == "baseline") return;
#endif
        int cameras = 0;
        if (views == null) return;
        foreach (View view in views)
        {
            Camera camera = view.camera;
            if (camera == null || !camera.isActiveAndEnabled) continue;
            cameras++;
            camera.useOcclusionCulling = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.farClipPlane = Mathf.Min(camera.farClipPlane, 450f);
            var data = view.data;
            data.renderPostProcessing = false;
            // The second view reuses lighting, without a second shadow render.
            data.renderShadows = !(camera.rect.height < .9f && camera.depth > 0f);
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = 0;
            // Each view needs its own depth/opaque copy for correctly refracted puddles.
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
        }
        if (pipeline != null && cameras > 0)
        {
            // Keep a user's lower detail preset instead of overwriting it with Alta's budget.
            QualitySettings.lodBias = Mathf.Min(previousLodBias, cameras > 1 ? .35f : .5f);
            pipeline.renderScale = cameras > 1 ? .55f : .7f;
            pipeline.shadowDistance = cameras > 1 ? 20f : 30f;
        }
    }

    void OnDestroy()
    {
        if (captured) QualitySettings.lodBias = previousLodBias;
        if (pipeline == null) return;
        pipeline.renderScale = previousScale;
        pipeline.shadowDistance = previousShadowDistance;
    }
}
