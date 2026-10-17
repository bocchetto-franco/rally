using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class RallyMenuController
{
    void BuildCredits(Transform root)
    {
        PageTitle(root, "CRÉDITOS", "Autores, fuentes y licencias. Incluye recursos históricos y permisos pendientes.");
        var panel = PanelRect(root, "Credits Panel", Panel, new Vector2(0f, -48f), new Vector2(1440f, 628f));
        var viewport = PanelRect(panel, "Credits Viewport", Color.clear, Vector2.zero, new Vector2(1380f, 588f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Credits Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = content.anchorMax = new Vector2(.5f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1300f, 588f);
        TextAsset credits = Resources.Load<TextAsset>("Credits/Credits");
        string document = credits != null ? credits.text : "No se encontró el archivo de créditos. Revisar la instalación.";
        document = Regex.Replace(document.Replace("\r\n", "\n"), @"(?m)^(#{1,3} .+)$", "$1\n");
        float offset = 12f;
        // Separate paragraphs avoid a single giant TMP mesh; the exact source is CREDITS.md.
        foreach (string raw in Regex.Split(document.Replace("\r\n", "\n"), @"\n\s*\n"))
        {
            string paragraph = raw.Trim();
            if (paragraph.Length == 0) continue;
            bool heading = paragraph.StartsWith("#", StringComparison.Ordinal);
            string value = Regex.Replace(paragraph, @"(?m)^#{1,3} ", "").Replace(((char)96).ToString(), "");
            var label = Text(content, "Credits Paragraph", value, heading ? 29f : 23f,
                heading ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.TopLeft,
                Vector2.zero, new Vector2(1300f, 100f), heading ? Accent : Color.white);
            label.richText = false;
            label.font = heading ? headingFont : font;
            label.lineSpacing = 4f;
            label.overflowMode = TextOverflowModes.Overflow;
            float height = Mathf.Ceil(label.GetPreferredValues(value, 1300f, Mathf.Infinity).y) + 8f;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(1300f, height);
            rect.anchoredPosition = new Vector2(0f, -offset);
            offset += height + (heading ? 14f : 16f);
        }
        content.sizeDelta = new Vector2(1300f, Mathf.Max(588f, offset + 12f));
        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 45f;
        var bar = PanelRect(panel, "Credits Scrollbar", PanelLight, new Vector2(690f, 0f), new Vector2(12f, 588f));
        var handle = PanelRect(bar, "Handle", Accent, Vector2.zero, new Vector2(12f, 50f));
        Stretch(handle);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        handle.GetComponent<Image>().raycastTarget = false;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        panel.gameObject.AddComponent<RallyCreditsScroll>().Configure(scroll);
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
        Text(root, "Credits Hint", "MOUSE / FLECHAS / D-PAD / STICK / L2: leer · ENTER / CRUZ / CÍRCULO / ESC: volver",
            18f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -392f), new Vector2(1490f, 34f), Muted);
        Button(root, "Back Button", "VOLVER", new Vector2(0f, -455f), new Vector2(380f, 74f),
            () => OpenFrontendPage(RallyMenuScreen.MainMenu), false);
    }
}
