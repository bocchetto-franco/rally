using UnityEngine;

/// <summary>Environment-only menu copy. Never includes racing scripts or vehicles.</summary>
public sealed class RallyMenuBackdropScene : MonoBehaviour
{
    public Vector3 focus;
    public Vector3 cameraPosition;
    public Material skybox;
    public Color ambientSky, ambientEquator, ambientGround;
    public float ambientIntensity = 1f;
    public bool fog;
    public Color fogColor;
    public float fogDensity;
    public FogMode fogMode;
}
