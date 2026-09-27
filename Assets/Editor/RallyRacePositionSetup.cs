using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RallyRacePositionSetup
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Circuit_01.unity",
        "Assets/Scenes/Circuit_02.unity",
        "Assets/Scenes/Circuit_03.unity"
    };

    [MenuItem("Tools/Rally/Add Race Positions To All Circuits")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene and leave Play before updating race HUDs.");

        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject hudRoot = FindRoot(scene, "Race HUD");
            GameObject route = FindRoot(scene, "AI_Waypoints");
            GameObject player = FindRoot(scene, "Porsche 911 SC Rally");
            GameObject checkpointRoot = FindRoot(scene, "Checkpoint System");
            RallyRaceHud hud = hudRoot != null ? hudRoot.GetComponent<RallyRaceHud>() : null;
            Rigidbody body = player != null ? player.GetComponent<Rigidbody>() : null;
            RallyCheckpointManager manager = checkpointRoot != null ? checkpointRoot.GetComponent<RallyCheckpointManager>() : null;
            if (hud == null || route == null || body == null || manager == null)
                throw new InvalidOperationException(scene.name + " needs HUD, route, player and checkpoints.");

            RallyRacePositions positions = hudRoot.GetComponent<RallyRacePositions>();
            if (positions == null)
                positions = hudRoot.AddComponent<RallyRacePositions>();
            positions.Configure(route.transform, body, manager);

            Transform panel = hudRoot.transform.Find("Position Panel");
            if (panel == null)
                panel = CreatePanel(hudRoot.transform);
            Transform label = panel.Find("Position");
            TMP_Text positionText = label != null ? label.GetComponent<TMP_Text>() : null;
            if (positionText == null)
                throw new InvalidOperationException(scene.name + " has an incomplete position label.");

            SerializedObject hudFields = new SerializedObject(hud);
            TMP_Text checkpointText = hudFields.FindProperty("checkpointText").objectReferenceValue as TMP_Text;
            if (checkpointText == null || checkpointText.font == null)
                throw new InvalidOperationException(scene.name + " has no HUD font to reuse.");
            positionText.font = checkpointText.font;
            hud.ConfigurePositions(positions, positionText);
            positionText.text = "4°/4";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + path);
        }
        Debug.Log("RACE_POSITIONS_SETUP_COMPLETE: Circuit_01, Circuit_02, Circuit_03");
    }

    private static Transform CreatePanel(Transform parent)
    {
        var panel = new GameObject("Position Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(28, -28);
        panelRect.sizeDelta = new Vector2(240, 86);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.05f, 0.72f);
        background.raycastTarget = false;

        var label = new GameObject("Position", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(220, 70);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.fontSize = 40;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return panel.transform;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }
}
