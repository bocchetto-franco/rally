using UnityEngine;

/// <summary>Authored width settings; baked by the Editor tool, no runtime mesh work.</summary>
public sealed class RallyRoadWidthProfile : MonoBehaviour
{
    [Range(1f,1.4f)] public float widthMultiplier=1.2f;
    [Min(5f)] public float minimumBoundaryMargin=5f;
    [Min(1f)] public float bridgeBlendDistance=20f;
    [HideInInspector] public bool applied;
}
