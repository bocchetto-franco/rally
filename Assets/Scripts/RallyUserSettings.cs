using System;
using UnityEngine;

/// <summary>Persisted user mix and quality selection, independent of race/vehicle settings.</summary>
public static class RallyUserSettings
{
    public static readonly string[] QualityNames = { "Baja", "Media", "Alta" };
    public static readonly string[] AudioParameters = { "MusicaVolume", "EfectosVolume", "MotorVolume" };
    // Preserve the existing ambient music mix (-22 dB), effects (-4) and engine (-3).
    static readonly float[] DefaultDb = { -22f, -4f, -3f };
    const string QualityKey = "Rally.Options.Quality";

    public static float Volume(int channel)
    {
        if (channel < 0 || channel >= AudioParameters.Length) throw new ArgumentOutOfRangeException(nameof(channel));
        return Mathf.Clamp01(PlayerPrefs.GetFloat("Rally.Options." + AudioParameters[channel], Mathf.Pow(10f, DefaultDb[channel] / 20f)));
    }

    public static void SetVolume(int channel, float value)
    {
        if (channel < 0 || channel >= AudioParameters.Length) throw new ArgumentOutOfRangeException(nameof(channel));
        PlayerPrefs.SetFloat("Rally.Options." + AudioParameters[channel], Mathf.Clamp01(value));
        ApplyAudioChannel(channel);
    }

    public static bool ApplyAudioChannel(int channel)
    {
        var library = RallyAudioLibrary.Load();
        if (library == null || library.mixer == null) return false;
        float value = Volume(channel);
        return library.mixer.SetFloat(AudioParameters[channel], value <= 0f ? -80f : 20f * Mathf.Log10(value));
    }

    // AudioMixer.SetFloat must run in Start or later, not in Awake/BeforeSceneLoad.
    public static void ApplyAudio()
    {
        for (int i = 0; i < AudioParameters.Length; i++) ApplyAudioChannel(i);
    }

    public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 2), 0, 2);
    public static void SetQuality(int choice)
    {
        choice = Mathf.Clamp(choice, 0, 2);
        int index = Array.IndexOf(QualitySettings.names, QualityNames[choice]);
        if (index < 0) { Debug.LogError("Missing Rally quality preset: " + QualityNames[choice]); return; }
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(QualityKey, choice);
        Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedQuality()
    {
        if (PlayerPrefs.HasKey(QualityKey)) SetQuality(Quality);
    }

    public static void Save() => PlayerPrefs.Save();
}
