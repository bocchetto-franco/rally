using System;
using UnityEngine;

/// <summary>Temporary play-mode probe used by RallyBotSmokeTest; never saved in a scene.</summary>
public sealed class RallyBotBoundaryProbe : MonoBehaviour
{
    public int BoundaryHits { get; private set; }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.name.StartsWith("Boundary_", StringComparison.Ordinal))
            BoundaryHits++;
    }
}
