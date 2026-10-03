using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Creates the serialized mixer/library with Unity's editor API, never at runtime.</summary>
[InitializeOnLoad]
public static class RallyAudioSetup
{
    public const string Folder = "Assets/Resources/Audio/";
    public const string MixerPath = Folder + "RallyAudio.mixer";
    public const string LibraryPath = Folder + "RallyAudioLibrary.asset";
    const string Request = "Temp/rally-audio-setup.request";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static RallyAudioSetup() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Install(); }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/audio-setup.txt", "FAILED\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Rally/Install and Verify Audio")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play before installing audio assets.");
        foreach (string name in new[] { "EngineLoop.wav", "TyreSkid.wav", "Impact.wav", "MenuMusic.ogg", "RaceMusic.ogg" })
        {
            string path = Folder + name;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Audio missing: " + path);
            bool music = name.EndsWith(".ogg", StringComparison.Ordinal);
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .75f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null) mixer = CreateMixer();
        EnsureMixerView(mixer);
        var library = AssetDatabase.LoadAssetAtPath<RallyAudioLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<RallyAudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.mixer = mixer;
        library.motorGroup = Group(mixer, "Motor");
        library.effectsGroup = Group(mixer, "Efectos");
        library.musicGroup = Group(mixer, "Musica");
        library.engineLoop = Clip("EngineLoop.wav");
        library.tyreSkidLoop = Clip("TyreSkid.wav");
        library.impact = Clip("Impact.wav");
        library.menuMusic = Clip("MenuMusic.ogg");
        library.raceMusic = Clip("RaceMusic.ogg");
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/audio-setup.txt", "PASS\nMixer: " + MixerPath
            + "\nGroups: Motor, Efectos, Musica\nExposed volumes: MotorVolume, EfectosVolume, MusicaVolume\n"
            + string.Join("\n", new[] { library.engineLoop, library.tyreSkidLoop, library.impact, library.menuMusic, library.raceMusic }
                .Select(c => c.name + ": " + c.length.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "s, " + c.channels + " channel(s)")));
        Debug.Log("Rally audio installed and verified. See Logs/audio-setup.txt");
    }

    static AudioMixerGroup Group(AudioMixer mixer, string name)
        => mixer.FindMatchingGroups(name).Single(g => g.name == name);

    static AudioClip Clip(string name)
        => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name)
            ?? throw new InvalidOperationException("Clip did not import: " + name);

    static void EnsureMixerView(AudioMixer mixer)
    {
        Type type = mixer.GetType();
        PropertyInfo views = type.GetProperty("views", Flags);
        if (((Array)views.GetValue(mixer)).Length > 0) return;
        Type viewType = views.PropertyType.GetElementType();
        object view = Activator.CreateInstance(viewType);
        viewType.GetField("name", Flags).SetValue(view, "Todos los canales");
        var groups = mixer.FindMatchingGroups("");
        FieldInfo guidsField = viewType.GetField("guids", Flags);
        Array guids = Array.CreateInstance(guidsField.FieldType.GetElementType(), groups.Length);
        for (int i = 0; i < groups.Length; i++)
            guids.SetValue(groups[i].GetType().GetProperty("groupID", Flags).GetValue(groups[i]), i);
        guidsField.SetValue(view, guids);
        Array array = Array.CreateInstance(viewType, 1);
        array.SetValue(view, 0);
        views.SetValue(mixer, array);
        EditorUtility.SetDirty(mixer);
    }

    static AudioMixer CreateMixer()
    {
        // Unity has no public creation API. Restrict reflection to this editor-only
        // installer; the shipped game uses a normal serialized AudioMixer asset.
        Type type = TypeCache.GetTypesDerivedFrom<AudioMixer>()
            .First(t => t.FullName == "UnityEditor.Audio.AudioMixerController");
        var mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { MixerPath });
        object master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
        object snapshot = type.GetProperty("startSnapshot", Flags).GetValue(mixer);
        PropertyInfo exposed = type.GetProperty("exposedParameters", Flags);
        Type parameterType = exposed.PropertyType.GetElementType();
        Array parameters = Array.CreateInstance(parameterType, 3);
        string[] names = { "Motor", "Efectos", "Musica" };
        float[] levels = { -3f, -4f, -22f };
        for (int i = 0; i < names.Length; i++)
        {
            object group = type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { names[i], false });
            type.GetMethod("AddChildToParent", Flags).Invoke(mixer, new[] { group, master });
            object guid = group.GetType().GetMethod("GetGUIDForVolume", Flags).Invoke(group, null);
            snapshot.GetType().GetMethod("SetValue", Flags).Invoke(snapshot, new[] { guid, (object)levels[i] });
            object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("guid", Flags).SetValue(parameter, guid);
            parameterType.GetField("name", Flags).SetValue(parameter, names[i] + "Volume");
            parameters.SetValue(parameter, i);
            EditorUtility.SetDirty((UnityEngine.Object)group);
        }
        exposed.SetValue(mixer, parameters);
        EditorUtility.SetDirty((UnityEngine.Object)snapshot);
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return mixer;
    }
}
