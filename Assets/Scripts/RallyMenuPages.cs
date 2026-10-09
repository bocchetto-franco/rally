using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class RallyMenuController
{
    Selectable[] optionControls;

    void OpenFrontendPage(RallyMenuScreen page)
    {
        RallyUserSettings.Save();
        screen = page;
        mainMenuRevealed = true;
        BuildInterface();
        // Consume the opening/returning press, not the first action on the new page.
        menuNavigation.enabled = false;
        StartCoroutine(EnableMenuNavigationNextFrame());
    }

    void PageTitle(Transform root, string title, string subtitle)
    {
        Text(root, "Title", title, 64f, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(0f, 365f), new Vector2(1440f, 90f), Color.white);
        Text(root, "Subtitle", subtitle, 23f, FontStyles.Normal, TextAlignmentOptions.Left,
            new Vector2(0f, 292f), new Vector2(1440f, 42f), Muted);
    }

    void BuildControls(Transform root)
    {
        PageTitle(root, "CONTROLES", "Tu auto, tus manos. Los mismos controles en los tres circuitos.");
        ControlColumn(root, "Keyboard Controls", "TECLADO", -375f,
            new[] { "Acelerar", "Frenar / marcha atrás", "Dirección", "Freno de mano", "Enderezar auto volcado", "Volver al checkpoint", "Pausa", "Navegar / confirmar", "Volver atrás" },
            new[] { "W / flecha arriba", "S / flecha abajo", "A / D · flechas", "Espacio", "T (también M)", "R", "Escape", "Flechas / Enter", "Escape" });
        ControlColumn(root, "Gamepad Controls", "DUALSENSE / GAMEPAD", 375f,
            new[] { "Acelerar", "Frenar", "Dirección", "Freno de mano", "Enderezar auto volcado", "Volver al checkpoint", "Pausa", "Navegar / confirmar", "Volver atrás" },
            new[] { "R2 / RT (analógico)", "L2 / LT (analógico)", "Stick izquierdo", "Cruz / A", "Cuadrado / X", "Triángulo / Y", "Options / Start", "D-Pad / Cruz (A)", "Círculo / B" });
        Text(root, "Controls Note", "Enderezar conserva tu progreso. Volver al checkpoint reposiciona el auto.\nOpciones: arriba/abajo elige un control; izquierda/derecha ajusta el volumen. Mouse también disponible.",
            20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -329f), new Vector2(1440f, 68f), Muted);
        Button(root, "Back Button", "VOLVER", new Vector2(0f, -441f), new Vector2(380f, 74f), () => OpenFrontendPage(RallyMenuScreen.MainMenu), false);
    }

    void ControlColumn(Transform root, string name, string title, float x, string[] actions, string[] inputs)
    {
        var card = PanelRect(root, name, Panel, new Vector2(x, -8f), new Vector2(700f, 535f));
        Text(card, "Heading", title, 30f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 219f), new Vector2(624f, 44f), Accent);
        for (int i = 0; i < actions.Length; i++)
        {
            float y = 150f - i * 43f;
            Text(card, "Action " + i, actions[i], 21f, FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(-157f, y), new Vector2(310f, 39f), Muted);
            Text(card, "Binding " + i, inputs[i], 21f, FontStyles.Bold, TextAlignmentOptions.Right,
                new Vector2(161f, y), new Vector2(310f, 39f), Color.white);
        }
    }

    void BuildOptions(Transform root)
    {
        PageTitle(root, "OPCIONES", "Ajustes guardados automáticamente para tu próxima carrera.");
        var audio = PanelRect(root, "Audio Options", Panel, new Vector2(0f, 69f), new Vector2(1440f, 310f));
        Text(audio, "Heading", "AUDIO", 30f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 113f), new Vector2(1340f, 45f), Accent);
        optionControls = new Selectable[7];
        string[] names = { "MÚSICA", "EFECTOS", "MOTOR" };
        for (int i = 0; i < 3; i++)
        {
            int channel = i;
            float y = 45f - i * 76f;
            Text(audio, names[i] + " Label", names[i], 25f, FontStyles.Bold, TextAlignmentOptions.Left,
                new Vector2(-506f, y), new Vector2(290f, 44f), Color.white);
            var percent = Text(audio, names[i] + " Value", "", 25f, FontStyles.Bold, TextAlignmentOptions.Right,
                new Vector2(584f, y), new Vector2(130f, 44f), Accent);
            Slider slider = VolumeSlider(audio, names[i] + " Slider", new Vector2(65f, y));
            slider.SetValueWithoutNotify(RallyUserSettings.Volume(i));
            percent.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                RallyUserSettings.SetVolume(channel, value);
                percent.text = Mathf.RoundToInt(value * 100f) + "%";
            });
            optionControls[i] = slider;
        }
        var graphics = PanelRect(root, "Graphics Options", Panel, new Vector2(0f, -207f), new Vector2(1440f, 172f));
        Text(graphics, "Heading", "CALIDAD GRÁFICA", 30f, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(-440f, 26f), new Vector2(460f, 60f), Accent);
        var qualityButtons = new Button[3];
        System.Action refresh = () =>
        {
            for (int i = 0; i < 3; i++)
            {
                var colors = qualityButtons[i].colors;
                colors.normalColor = RallyUserSettings.Quality == i ? Accent : PanelLight;
                qualityButtons[i].colors = colors;
                qualityButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = RallyUserSettings.Quality == i ? Background : Color.white;
            }
        };
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            qualityButtons[i] = Button(graphics, "Quality " + RallyUserSettings.QualityNames[i], RallyUserSettings.QualityNames[i].ToUpperInvariant(),
                new Vector2(-15f + i * 252f, 26f), new Vector2(224f, 65f), () => { RallyUserSettings.SetQuality(choice); refresh(); }, false);
            optionControls[3 + i] = qualityButtons[i];
        }
        refresh();
        Text(graphics, "Quality Note", "Alta conserva el aspecto actual; Media y Baja reducen detalle y carga gráfica.", 20f,
            FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -49f), new Vector2(1300f, 36f), Muted);
        Text(root, "Options Hint", "ARRIBA/ABAJO: elegir · IZQUIERDA/DERECHA: volumen · CRUZ/ENTER: confirmar · CÍRCULO/ESC: volver", 19f,
            FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -345f), new Vector2(1490f, 40f), Muted);
        optionControls[6] = Button(root, "Back Button", "VOLVER", new Vector2(0f, -441f), new Vector2(380f, 74f), () => OpenFrontendPage(RallyMenuScreen.MainMenu), false);
    }

    Slider VolumeSlider(Transform parent, string name, Vector2 position)
    {
        var rect = PanelRect(parent, name, PanelLight, position, new Vector2(830f, 46f));
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        var fillArea = PanelRect(rect, "Fill Area", Color.clear, Vector2.zero, new Vector2(800f, 10f));
        var fill = PanelRect(fillArea, "Fill", Accent, Vector2.zero, new Vector2(800f, 10f));
        Stretch(fill); // Slider drives its anchors; fixed width offsets would overflow the track.
        var handleArea = PanelRect(rect, "Handle Area", Color.clear, Vector2.zero, new Vector2(800f, 30f));
        var handle = PanelRect(handleArea, "Handle", Color.white, Vector2.zero, new Vector2(18f, 30f));
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        // Only the slider background handles pointer events; visual children must not block it.
        fillArea.GetComponent<Image>().raycastTarget = false;
        fill.GetComponent<Image>().raycastTarget = false;
        handleArea.GetComponent<Image>().raycastTarget = false;
        handle.GetComponent<Image>().raycastTarget = false;
        return slider;
    }

    void OnApplicationPause(bool paused) { if (paused) RallyUserSettings.Save(); }
    void OnApplicationQuit() => RallyUserSettings.Save();
}
