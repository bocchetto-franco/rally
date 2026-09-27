using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RallyBotSmokeTest
{
    private const string RequestPath = "Logs/bot-smoke-request.txt";
    private const string ResultPath = "Logs/bot-smoke-result.txt";
    private const string Prefix = "RallyBotSmoke.";
    private static readonly string[] Scenes = { "Circuit_01", "Circuit_02", "Circuit_03" };

    static RallyBotSmokeTest() => EditorApplication.update += Update;

    [MenuItem("Tools/Rally/Verify Bots In All Circuits")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene and leave Play before the bot smoke test.");
        SessionState.SetString(Prefix + "PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        File.WriteAllText(ResultPath, "Bot runtime smoke test\n");
        SessionState.SetInt(Prefix + "Index", 0);
        StartScene(0);
    }

    private static void StartScene(int index)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/" + Scenes[index] + ".unity", OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + Scenes[index] + ".unity");
        SessionState.SetString(Prefix + "PlayStart", "");
        SessionState.SetInt(Prefix + "StartWaypoint", -1);
        SessionState.SetFloat(Prefix + "MaxCrossTrack", 0f);
        SessionState.SetInt(Prefix + "MaxCrossTrackWaypoint", -1);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (File.Exists(RequestPath))
            {
                File.Move(RequestPath, RequestPath + ".consumed-" + DateTime.Now.Ticks);
                try { Start(); }
                catch (Exception e) { File.AppendAllText(ResultPath, "START ERROR: " + e + "\n"); }
                return;
            }
            int next = SessionState.GetInt(Prefix + "Index", -1);
            if (next >= 0 && SessionState.GetBool(Prefix + "BetweenScenes", false))
            {
                SessionState.SetBool(Prefix + "BetweenScenes", false);
                if (next < Scenes.Length)
                    StartScene(next);
                else
                {
                    string previous = SessionState.GetString(Prefix + "PreviousStartScene", "");
                    EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                        ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                    SessionState.SetInt(Prefix + "Index", -1);
                    File.AppendAllText(ResultPath, "COMPLETE\n");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                }
            }
            return;
        }

        if (!EditorApplication.isPlaying)
            return;
        int index = SessionState.GetInt(Prefix + "Index", -1);
        if (index < 0 || index >= Scenes.Length || SceneManager.GetActiveScene().name != Scenes[index])
            return;
        GameObject botObject = GameObject.Find("Bot_Car");
        RallyBotController bot = botObject != null ? botObject.GetComponent<RallyBotController>() : null;
        Rigidbody body = botObject != null ? botObject.GetComponentInChildren<Rigidbody>() : null;
        RallyCheckpointManager manager = UnityEngine.Object.FindAnyObjectByType<RallyCheckpointManager>();
        if (bot == null || body == null || manager == null)
        {
            File.AppendAllText(ResultPath, Scenes[index] + ": missing bot/body/manager\n");
            FinishScene(index);
            return;
        }
        RallyBotBoundaryProbe probe = body.GetComponent<RallyBotBoundaryProbe>();
        if (probe == null)
            probe = body.gameObject.AddComponent<RallyBotBoundaryProbe>();
        string started = SessionState.GetString(Prefix + "PlayStart", "");
        if (string.IsNullOrEmpty(started))
        {
            SessionState.SetString(Prefix + "PlayStart", DateTime.UtcNow.Ticks.ToString());
            SessionState.SetInt(Prefix + "StartWaypoint", bot.CurrentWaypointIndex);
            return;
        }
        double elapsed = (DateTime.UtcNow - new DateTime(long.Parse(started), DateTimeKind.Utc)).TotalSeconds;
        if (bot.CrossTrackDistance > SessionState.GetFloat(Prefix + "MaxCrossTrack", 0f))
        {
            SessionState.SetFloat(Prefix + "MaxCrossTrack", bot.CrossTrackDistance);
            SessionState.SetInt(Prefix + "MaxCrossTrackWaypoint", bot.CurrentWaypointIndex + 1);
        }
        if (elapsed < 100.0)
            return;
        int startWaypoint = SessionState.GetInt(Prefix + "StartWaypoint", -1);
        string status = bot.CurrentWaypointIndex >= startWaypoint + 12 && probe.BoundaryHits == 0
            ? "PASS" : "CHECK";
        File.AppendAllText(ResultPath,
            $"{Scenes[index]}: {status}; waypoint {startWaypoint + 1} -> {bot.CurrentWaypointIndex + 1}; " +
            $"bot speed {body.linearVelocity.magnitude * 3.6f:F1} km/h; player timer {manager.FormattedTime}; " +
            $"checkpoints {manager.CheckpointCount}; max cross-track " +
            $"{SessionState.GetFloat(Prefix + "MaxCrossTrack", 0f):F1} m near waypoint " +
            $"{SessionState.GetInt(Prefix + "MaxCrossTrackWaypoint", -1)}; " +
            $"boundary hits {probe.BoundaryHits}\n");
        FinishScene(index);
    }

    private static void FinishScene(int index)
    {
        SessionState.SetInt(Prefix + "Index", index + 1);
        SessionState.SetBool(Prefix + "BetweenScenes", true);
        EditorApplication.isPlaying = false;
    }
}
