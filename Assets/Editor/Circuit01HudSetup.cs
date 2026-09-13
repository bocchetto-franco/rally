using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class Circuit01HudSetup
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string FontPath = "Assets/Settings/RallyHUD_Font.asset";
    const string SourceFontPath = "Assets/Settings/RallyHUD.ttf";

    static Circuit01HudSetup() => EditorApplication.delayCall += InstallOnce;

    static void InstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += InstallOnce; return; }
        if (SceneManager.GetActiveScene().path == ScenePath && GameObject.Find("Race HUD") == null)
            Install();
    }

    [MenuItem("Tools/Rally/Create Circuit 01 Race HUD")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;

        RallyCheckpointManager manager = UnityEngine.Object.FindAnyObjectByType<RallyCheckpointManager>();
        GameObject car = scene.GetRootGameObjects().Single(g => g.name == "Porsche 911 SC Rally");
        Rigidbody body = car.GetComponent<Rigidbody>();
        if (manager == null || body == null) throw new InvalidOperationException("Race HUD requires checkpoints and the Porsche Rigidbody.");

        GameObject oldCanvas = GameObject.Find("Checkpoint Timer Canvas");
        if (oldCanvas != null) UnityEngine.Object.DestroyImmediate(oldCanvas);
        GameObject previous = GameObject.Find("Race HUD");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

        TMP_FontAsset font = GetOrCreateFont();
        var root = new GameObject("Race HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RallyRaceHud));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        RectTransform timingPanel = Panel(root.transform, "Timing Panel", new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(560, 132), new Vector2(.5f, 1));
        TMP_Text time = Label(timingPanel, "Time", font, 44, TextAlignmentOptions.Center, new Vector2(0, -12), new Vector2(530, 58));
        TMP_Text checkpoint = Label(timingPanel, "Checkpoint", font, 30, TextAlignmentOptions.Center, new Vector2(0, -72), new Vector2(530, 42));

        RectTransform speedPanel = Panel(root.transform, "Speed Panel", new Vector2(1, 0), new Vector2(-30, 30), new Vector2(330, 122), new Vector2(1, 0));
        TMP_Text speed = Label(speedPanel, "Speed", font, 64, TextAlignmentOptions.Center, Vector2.zero, new Vector2(310, 104));

        root.GetComponent<RallyRaceHud>().Configure(manager, body, speed, time, checkpoint);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Circuit_01 HUD.");
        Selection.activeGameObject = root;
        Debug.Log("CIRCUIT_01_HUD_OK: TextMeshPro timer, Checkpoint 0/6 counter and Rigidbody km/h speedometer saved.");
    }

    static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = new Color(.025f, .035f, .05f, .72f);
        image.raycastTarget = false;
        return rect;
    }

    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    static TMP_FontAsset GetOrCreateFont()
    {
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (asset != null) return asset;
        if (!File.Exists(SourceFontPath))
        {
            string windowsFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            if (!File.Exists(windowsFont)) throw new FileNotFoundException("Arial font was not found.", windowsFont);
            File.Copy(windowsFont, SourceFontPath);
            AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceSynchronousImport);
        }
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null) throw new InvalidOperationException("Could not import the Rally HUD source font.");
        asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "RallyHUD Font";
        AssetDatabase.CreateAsset(asset, FontPath);
        if (asset.material != null && !AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (Texture2D atlas in asset.atlasTextures.Where(a => a != null && !AssetDatabase.Contains(a))) AssetDatabase.AddObjectToAsset(atlas, asset);
        AssetDatabase.ImportAsset(FontPath);
        return asset;
    }
}
