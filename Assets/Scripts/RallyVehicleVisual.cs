using UnityEngine;

/// <summary>Prepared visual variant; geometry data is measured by the editor importer.</summary>
public sealed class RallyVehicleVisual : MonoBehaviour
{
    // FL, FR, BL, BR. Right pivots include JrsVehicleController's 180-degree flip.
    public Transform[] wheels;
    public float[] radii;
    public Bounds bodyBounds;
    [Tooltip("Model authored at real-world scale: keep its proportions and fit only wheel collider geometry.")]
    public bool useAuthoredScale;
    [Tooltip("Visual-only tailpipe anchor in this model's local metres.")]
    public bool hasExhaustAnchor;
    public Vector3 exhaustLocalPosition;
}
