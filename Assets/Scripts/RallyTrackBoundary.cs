using UnityEngine;

/// <summary>Shared containment classification; touching a wall is never a speed-limit event.</summary>
public static class RallyTrackBoundary
{
    public static bool IsBoundary(Collider collider)
    {
        if (collider == null || collider.isTrigger) return false;
        string label = collider.name;
        return label.StartsWith("Boundary_") || label.StartsWith("Bridge boundary ") ||
            (collider.sharedMaterial != null && collider.sharedMaterial.name == "Circuit_01_SoftBoundary");
    }
}
