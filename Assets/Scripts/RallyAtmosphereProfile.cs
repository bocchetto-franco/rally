using UnityEngine;

[CreateAssetMenu(menuName = "Rally/Atmosphere Profile")]
public sealed class RallyAtmosphereProfile : ScriptableObject
{
    public Vector3 sunEuler = new Vector3(18, -35, 0);
    public Color sunColor = new Color(1, .69f, .4f);
    [Min(0)] public float sunIntensity = 1.25f;
    [Range(0, 1)] public float shadowStrength = .85f;
    public Material skybox;
    public Color ambientSky = new Color(.6f, .48f, .5f);
    public Color ambientEquator = new Color(.51f, .39f, .28f);
    public Color ambientGround = new Color(.26f, .22f, .2f);
    public Color fogColor = new Color(.7f, .51f, .4f);
    [Min(0)] public float fogStart = 120;
    [Min(1)] public float fogEnd = 430;
    public Material originalRoadMaterial;
    public Material wetRoadMaterial;
    public bool rain;
    public Material rainMaterial;
    [Min(1)] public int rainParticlesSingle = 480;
    [Min(1)] public int rainParticlesPerSplitView = 300;
}
