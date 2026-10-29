using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Headless final checks, fresh occlusion and the standard Windows release.</summary>
public static class RallyOctoberBatchFinalize
{
    const string Folder="Logs/Gameplay_20261028/";
    const string Report=Folder+"batch-finalization.txt";
    static bool finishing;
    static double deadline;
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Report,"October batch finalization\nStarted: "+DateTimeOffset.Now.ToString("O")+"\n");
            if(EditorUtility.scriptCompilationFailed)throw new Exception("Scripts did not compile");
            for(int circuit=1;circuit<=3;circuit++)
            {
                string path=$"Assets/Scenes/Circuit_0{circuit}.unity";
                var scene=EditorSceneManager.OpenScene(path);
                var car=Object.FindObjectsByType<JrsVehicleController>().Single(c=>c.name==RallyGameSession.VehicleName);
                if(car.GetComponent<RallyVehicleTurbo>()==null)throw new Exception("Missing turbo receiver");
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/RallyHUD_Font.asset");
                foreach(int player in new[]{-1,0,1})
                {
                    var root=new GameObject("Temporary HUD geometry check",typeof(RectTransform),typeof(Canvas));
                    root.hideFlags=HideFlags.DontSave;
                    try
                    {
                        Transform parent=root.transform;
                        if(player>=0)
                        {
                            var viewport=new GameObject("Player Viewport",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
                            viewport.SetParent(root.transform,false);
                            viewport.anchorMin=new Vector2(0,player==0?.5f:0);
                            viewport.anchorMax=new Vector2(1,player==0?1:.5f);
                            viewport.offsetMin=viewport.offsetMax=Vector2.zero;parent=viewport;
                        }
                        var hud=root.AddComponent<RallyRaceAssistHud>();hud.Configure(car.GetComponent<Rigidbody>(),font);
                        foreach(string name in new[]{"Turbo inventory","Water pace note"})
                        {
                            var panel=parent.Find(name) as RectTransform;
                            if(panel==null || root.GetComponentsInChildren<RectTransform>(true).Count(t=>t.name==name)!=1)
                                throw new Exception("Missing or duplicated HUD panel "+name);
                            if(panel.parent!=parent)throw new Exception("HUD escaped its viewport");
                            if(player>=0 && (Mathf.Abs(panel.localScale.x-.7f)>.001f || panel.anchoredPosition.y==18))
                                throw new Exception("Wrong split HUD geometry");
                        }
                        File.AppendAllText(Report,$"PASS: {scene.name} HUD {(player<0?"1P":("2P player "+(player+1)))}\n");
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                // Discard only the temporary test objects' Editor dirty state.
                EditorSceneManager.OpenScene(path);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            deadline=EditorApplication.timeSinceStartup+2700;
            RallyOctoberOcclusion.Run();EditorApplication.update+=Tick;
        }
        catch(Exception e){Fail(e);}
    }
    static void Tick()
    {
        if(finishing)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Occlusion deadline exceeded");
            string text=File.ReadAllText(Folder+"occlusion.txt");
            if(text.Contains("FAIL:"))throw new Exception(text);
            if(!text.Contains("COMPLETE: PASS") || StaticOcclusionCulling.isRunning || EditorApplication.isUpdating || EditorApplication.isCompiling)return;
            finishing=true;EditorApplication.update-=Tick;
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            File.AppendAllText(Report,"PASS: three fresh occlusion bakes saved\n");
            string buildRoot=Path.GetFullPath("Build")+Path.DirectorySeparatorChar;
            string current=Path.GetFullPath("Build/Windows"),previous=Path.GetFullPath("Build/Windows_Previous_20261028_Gameplay");
            if(!current.StartsWith(buildRoot,StringComparison.OrdinalIgnoreCase)||!previous.StartsWith(buildRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe build paths");
            if(Directory.Exists(previous))throw new Exception("Previous build backup already exists; inspect before retrying");
            if(Directory.Exists(current))Directory.Move(current,previous);
            File.AppendAllText(Report,"BACKUP: "+previous+"\n");
            BuildWindows.BuildRelease();
            string buildLog=File.ReadAllText(BuildWindows.BuildLog);
            if(!buildLog.Contains("Result: Succeeded") || !buildLog.Contains("Errors: 0"))throw new Exception("Build did not succeed");
            File.Copy(BuildWindows.BuildLog,Folder+"BuildLog.txt",true);
            File.AppendAllText(Report,"COMPLETE: PASS\nFinished: "+DateTimeOffset.Now.ToString("O")+"\n");
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    static void Fail(Exception error)
    {
        finishing=true;EditorApplication.update-=Tick;
        File.AppendAllText(Report,"FAIL: "+error+"\n");Debug.LogException(error);
        if(Application.isBatchMode)EditorApplication.Exit(1);
    }
}
