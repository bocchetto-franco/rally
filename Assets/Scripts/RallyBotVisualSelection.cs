using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gives the pre-placed bots different random car bodies each race.</summary>
public static class RallyBotVisualSelection
{
    private static readonly HashSet<ulong> configuredScenes = new HashSet<ulong>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForPlaySession()
    {
        configuredScenes.Clear();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene) => configuredScenes.Remove(scene.handle.GetRawData());

    public static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || Array.IndexOf(RallyGameSession.CircuitScenes, scene.name) < 0 ||
            configuredScenes.Contains(scene.handle.GetRawData()))
            return;

        var bots = new List<RallyBotController>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RallyBotController bot in root.GetComponentsInChildren<RallyBotController>(true))
                if (bot.isActiveAndEnabled)
                    bots.Add(bot);
        if (bots.Count == 0)
            return;
        bots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        string[] models = (string[])RallyPlayerVehicleSelection.Names.Clone();
        for (int i = 0; i < bots.Count; i++)
        {
            // Shuffle without replacement: with the current three bots, a race
            // always has each current selectable model once, in random slots.
            if (i % models.Length == 0)
                for (int j = models.Length - 1; j > 0; j--)
                {
                    int other = UnityEngine.Random.Range(0, j + 1);
                    (models[j], models[other]) = (models[other], models[j]);
                }

            JrsVehicleController vehicle = bots[i].GetComponentInChildren<JrsVehicleController>(true);
            if (vehicle == null)
                throw new InvalidOperationException("Bot has no vehicle rig: " + bots[i].name);
            string model = models[i % models.Length];
            RallyPlayerVehicleSelection.ApplyBotVisual(vehicle, model);
            Debug.Log($"BOT_VISUAL_SELECTED {scene.name}/{bots[i].name}: {model}");
        }
        configuredScenes.Add(scene.handle.GetRawData());
    }
}
