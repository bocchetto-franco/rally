using UnityEngine;
using UnityEngine.Audio;

/// <summary>Shared audio assets and mix levels; no vehicle physics parameters.</summary>
[CreateAssetMenu(menuName = "Rally/Audio Library")]
public sealed class RallyAudioLibrary : ScriptableObject
{
    public const string ResourcePath = "Audio/RallyAudioLibrary";
    public AudioMixer mixer;
    public AudioMixerGroup motorGroup;
    public AudioMixerGroup effectsGroup;
    public AudioMixerGroup musicGroup;

    [Header("CC0 recordings")]
    public AudioClip engineLoop;
    public AudioClip tyreSkidLoop;
    public AudioClip impact;
    public AudioClip menuMusic;
    public AudioClip raceMusic;

    [Header("Source levels (before mixer faders)")]
    [Range(0f, 1f)] public float engineVolume = 0.75f;
    [Range(0f, 1f)] public float skidVolume = 0.65f;
    [Range(0f, 1f)] public float impactVolume = 0.85f;
    [Range(0f, 1f)] public float menuMusicVolume = 0.75f;
    [Range(0f, 1f)] public float raceMusicVolume = 0.5f;
    [Min(0.1f)] public float musicFadeSeconds = 0.8f;

    [Header("Collision sound")]
    [Min(0f)] public float minimumImpactSpeed = 2f;
    [Min(0.1f)] public float fullVolumeImpactSpeed = 14f;
    [Min(0.05f)] public float impactCooldown = 0.2f;

    public static RallyAudioLibrary Load() => Resources.Load<RallyAudioLibrary>(ResourcePath);
}
