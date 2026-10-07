using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyRecoveryPromptCheck
{
    public static string Refresh()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();
        return Status();
    }

    public static string Status() => "playing=" + EditorApplication.isPlaying +
        "; compiling=" + EditorApplication.isCompiling + "; scene=" + SceneManager.GetActiveScene().path +
        "; dirty=" + SceneManager.GetActiveScene().isDirty;

    public static string Verify()
    {
        if (EditorApplication.isCompiling) throw new System.Exception("Wait for compilation");
        if (EditorApplication.isPlaying)
        {
            typeof(RallyVehicleRecoverySmokeTest).GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            return "PASS: recovery input and HUD tested in current Play; scenes were not saved";
        }
        RallyVehicleRecoverySmokeTest.Run();
        return "Started recovery/HUD Play test; see Logs/vehicle-recovery-test.txt";
    }
}
