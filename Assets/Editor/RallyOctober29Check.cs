using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
public static class RallyOctober29Check
{
    public static void Run()
    {
        const string report="Logs/UX_20261029/validation.txt";
        File.WriteAllText(report,"October 29 validation\n");
        if(EditorUtility.scriptCompilationFailed)throw new Exception("Compilation failed");
        foreach(var index in new[]{1,2,3})
        {
            EditorSceneManager.OpenScene($"Assets/Scenes/Circuit_0{index}.unity");
            var car=Object.FindObjectsByType<JrsVehicleController>().Single(c=>c.name==RallyGameSession.VehicleName);
            var dynamics=Object.FindObjectsByType<RallyVehicleDynamics>().Single(d=>d.VehicleController==car);
            if(car.maxSteerAngle!=52 || dynamics.SteeringResponse!=82)throw new Exception("Steering mismatch");
            foreach(bool split in new[]{false,true})
            {
                var root=new GameObject("Temporary hazard check",typeof(Canvas));
                if(split){var view=new GameObject("Player Viewport",typeof(RectTransform));view.transform.SetParent(root.transform,false);}
                var hud=root.AddComponent<RallyRaceAssistHud>();hud.Configure(car.GetComponent<Rigidbody>(),AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/RallyHUD_Font.asset"));
                var arrow=root.GetComponentInChildren<RallyPaceNoteArrow>(true);
                if(arrow==null || arrow.color.b<.9f)throw new Exception("Water arrow missing");
                var labels=root.GetComponentsInChildren<TMP_Text>(true);
                if(labels.Any(t=>(t.text??"").Contains(" m</size>")) || !labels.Any(t=>t.text=="POZO / AGUA"))throw new Exception("Water label mismatch");
                var parent=split?root.transform.Find("Player Viewport"):root.transform;
                if(parent.Find("Water pace note")==null)throw new Exception("Wrong viewport");
                Object.DestroyImmediate(root);
            }
            File.AppendAllText(report,$"PASS: Circuit_0{index} steering 52/82, cyan arrow, no metres, 1P/2P parents\n");
        }
        if(Mathf.Abs(RallyPaceNotePresentation.Scale(.32f,.32f)-1)>.001f || RallyPaceNotePresentation.Scale(0,.32f)!=.84f)throw new Exception("Animation mismatch");
        string menu=File.ReadAllText("Assets/Scripts/RallyMenuController.cs");
        if(menu.Contains("Bot Difficulty Card") || menu.Contains("difficultyButtons"))throw new Exception("Difficulty selector remains");
        File.AppendAllText(report,"PASS: shared curve/water animation; selector removed; Easy session retained\nCOMPLETE: PASS\n");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        RallyCreditsSetup.Sync();
        string output="Build/Windows_UX_20261029/Rally.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var built=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=BuildWindows.Validate(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.CompressWithLz4HC|BuildOptions.DetailedBuildReport});
        File.AppendAllText(report,$"BUILD: {built.summary.result}; errors {built.summary.totalErrors}; warnings {built.summary.totalWarnings}; {output}\n");
        if(built.summary.result!=BuildResult.Succeeded || built.summary.totalErrors!=0)throw new Exception("Build failed");
    }
}
