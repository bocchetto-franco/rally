using UnityEngine;

/// <summary>Bot-only friction tune. Assigning this asset never changes the player Porsche.</summary>
[CreateAssetMenu(menuName = "Rally/Bot Grip Profile")]
public sealed class RallyBotGripProfile : ScriptableObject
{
    [Header("Forward grip")]
    [Min(0.1f)] public float frontForwardExtremumValue = 1.30f;
    [Min(0.1f)] public float frontForwardAsymptoteValue = 0.95f;
    [Min(0.1f)] public float rearForwardExtremumValue = 1.35f;
    [Min(0.1f)] public float rearForwardAsymptoteValue = 1.00f;
    [Min(0.1f)] public float forwardStiffness = 1.35f;

    [Header("Front lateral grip")]
    [Min(0.1f)] public float frontSideExtremumValue = 1.70f;
    [Min(0.1f)] public float frontSideAsymptoteValue = 1.40f;
    [Min(0.1f)] public float frontSideStiffness = 1.35f;

    [Header("Rear lateral grip")]
    [Min(0.01f)] public float rearSideExtremumSlip = 0.32f;
    [Min(0.1f)] public float rearSideExtremumValue = 1.70f;
    [Min(0.01f)] public float rearSideAsymptoteSlip = 0.80f;
    [Min(0.1f)] public float rearSideAsymptoteValue = 1.40f;
    [Min(0.1f)] public float rearSideStiffness = 1.35f;
}
