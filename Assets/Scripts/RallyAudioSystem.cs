using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One persistent music player, with a short crossfade between menu and race.</summary>
[DisallowMultipleComponent]
public sealed class RallyAudioSystem : MonoBehaviour
{
    static RallyAudioSystem instance;
    RallyAudioLibrary library;
    readonly AudioSource[] music = new AudioSource[2];
    int activeSource;
    bool racing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance == null && RallyAudioLibrary.Load() != null)
            new GameObject("Rally Audio - Music").AddComponent<RallyAudioSystem>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        library = RallyAudioLibrary.Load();
        if (library == null) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
        for (int i = 0; i < music.Length; i++)
        {
            music[i] = gameObject.AddComponent<AudioSource>();
            music[i].outputAudioMixerGroup = library.musicGroup;
            music[i].playOnAwake = false;
            music[i].loop = true;
            music[i].spatialBlend = 0f;
            music[i].volume = 0f;
            music[i].priority = 128;
            music[i].ignoreListenerPause = true;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        RallyUserSettings.ApplyAudio();
        SelectMusic(SceneManager.GetActiveScene());
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SelectMusic(scene);

    void SelectMusic(Scene scene)
    {
        racing = Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) >= 0;
        bool menu = scene.name == RallyGameSession.MainMenuScene
            || scene.name == RallyGameSession.SelectionScene || scene.name == RallyGameSession.ResultsScene;
        AudioClip wanted = racing ? (library.raceMusic != null ? library.raceMusic : library.menuMusic)
            : menu ? library.menuMusic : null;
        if (music[activeSource].clip == wanted) return;
        activeSource = 1 - activeSource;
        AudioSource source = music[activeSource];
        source.Stop();
        source.clip = wanted;
        source.volume = 0f;
        if (wanted != null) source.Play();
    }

    void Update()
    {
        if (library == null) return;
        float volume = racing ? library.raceMusicVolume : library.menuMusicVolume;
        if (racing && Time.timeScale <= 0f) volume *= 0.65f;
        for (int i = 0; i < music.Length; i++)
        {
            AudioSource source = music[i];
            float target = i == activeSource && source.clip != null ? volume : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target,
                Time.unscaledDeltaTime / Mathf.Max(0.1f, library.musicFadeSeconds));
            if (i != activeSource && source.volume == 0f && source.isPlaying) source.Stop();
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    // Call from a future settings UI (Start or later). Zero means mute.
    public bool SetChannelVolume(string exposedParameter, float linearVolume)
    {
        if (library == null || library.mixer == null) return false;
        float db = linearVolume <= 0f ? -80f : 20f * Mathf.Log10(Mathf.Clamp01(linearVolume));
        return library.mixer.SetFloat(exposedParameter, db);
    }
}
