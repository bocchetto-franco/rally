using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A slow real-time tour of a stripped visual copy, not an active race.</summary>
public sealed class RallyMenuBackdrop : MonoBehaviour
{
    RallyMenuBackdropScene environment;
    Camera cameraView;
    string requestedTrack;
    int revision;
    float angle;
    Terrain[] terrains;

    public void Show(string track)
    {
        if (requestedTrack == track) return;
        requestedTrack = track;
        StartCoroutine(Load(track, ++revision));
    }

    IEnumerator Load(string track, int request)
    {
        ResourceRequest load = Resources.LoadAsync<GameObject>("MenuBackdrops/" + track);
        yield return load;
        if (request != revision || load.asset == null) yield break;
        if (environment != null)
        {
            environment.gameObject.SetActive(false);
            Destroy(environment.gameObject);
        }
        environment = Instantiate((GameObject)load.asset, transform).GetComponent<RallyMenuBackdropScene>();
        terrains = environment.GetComponentsInChildren<Terrain>();
        cameraView = Camera.main;
        if (cameraView == null || environment == null) yield break;
        cameraView.cullingMask = 1 << 31;
        cameraView.clearFlags = CameraClearFlags.Skybox;
        cameraView.nearClipPlane = .3f;
        cameraView.farClipPlane = 1000f;
        cameraView.fieldOfView = 55f;
        cameraView.allowHDR = false;
        cameraView.allowMSAA = false;
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
}
