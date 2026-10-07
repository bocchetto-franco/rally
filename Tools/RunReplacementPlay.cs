public static class RunReplacementPlay
{
    public static void Run() => RallyReplacementVehiclePlayTest.Run();
    public static string Status() => "play="+UnityEditor.EditorApplication.isPlaying+
        " phase="+UnityEditor.SessionState.GetInt("ReplacementCarsTest.Phase",-1)+
        " case="+UnityEditor.SessionState.GetInt("ReplacementCarsTest.Case",-1)+
        " active="+UnityEditor.SessionState.GetBool("ReplacementCarsTest.Active",false)+
        " scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+
        " background="+UnityEngine.Application.runInBackground+
        " time="+UnityEngine.Time.time;
}
