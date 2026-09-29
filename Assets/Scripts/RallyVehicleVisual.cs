using UnityEngine;

/// <summary>Prepared visual variant; geometry data is measured by the editor importer.</summary>
public sealed class RallyVehicleVisual : MonoBehaviour
{
    // FL, FR, BL, BR. Right pivots include JrsVehicleController's 180-degree flip.
    public Transform[] wheels;
    public float[] radii;
    public Bounds bodyBounds;
}
