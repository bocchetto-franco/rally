using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// External Pipeline runner; survives Play with the project's no-domain-reload option.
public static class RallyFourUXPlay
{
    static bool optimized, sampling, restored, entered;
    static int phase,lastFrame,oldSync,oldCap,oldQuality,oldCircuit,oldPlayers;
    static double began,stage,deadline;
    static bool oldBackground;
    static SceneSetup[] scenes;
    static SceneAsset oldStart;
    static EditorWindow gameView;
    static object sizeGroup;
    static int oldViewIndex, addedCustomIndex = -1;
    const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
    static readonly List<float> times=new List<float>();
    static Vector3 initialCamera;
    static string report;
    public static string Baseline()=>Run(false);
    public static string After()=>Run(true);
    static string Run(bool after)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||StaticOcclusionCulling.isRunning)throw new Exception("Editor must be idle.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save open scenes first.");
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var old=assembly.GetType("RallyFourUXPlay");
            if(old==null||old==typeof(RallyFourUXPlay))continue;
            var stored=old.GetField("scenes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)?.GetValue(null);
            if(stored!=null)old.GetMethod("Restore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)?.Invoke(null,null);
        }
        optimized=after;phase=0;sampling=false;restored=false;entered=false;times.Clear();lastFrame=-1;
        FixView();
        scenes=EditorSceneManager.GetSceneManagerSetup();oldStart=EditorSceneManager.playModeStartScene;
        oldSync=QualitySettings.vSyncCount;oldCap=Application.targetFrameRate;oldQuality=QualitySettings.GetQualityLevel();oldBackground=Application.runInBackground;
        RallyGameSession.RestoreSavedState();oldCircuit=Array.IndexOf(RallyGameSession.CircuitScenes,RallyGameSession.SelectedRaceScene);oldPlayers=RallyGameSession.LocalPlayerCount;
        RallyGameSession.SelectCircuit(1);RallyGameSession.SelectLocalPlayers(1);
        report="Logs/four-ux-"+(after?"after":"baseline")+".txt";File.WriteAllText(report,"Menu C02 benchmark (Editor, not build)\n");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        EditorApplication.update+=Tick;began=stage=EditorApplication.timeSinceStartup;deadline=began+240;
        EditorApplication.isPlaying=true;return "Started: "+report;
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(phase==99){if(!EditorApplication.isPlayingOrWillChangePlaymode)Restore();return;}
            if(now>deadline)throw new Exception("Timeout phase "+phase);
            if(!EditorApplication.isPlaying)
            {
                if(entered&&!EditorApplication.isPlayingOrWillChangePlaymode)Finish(new Exception("Play stopped externally before verification completed."));
                return;
            }
            entered=true;
            if(EditorApplication.isCompiling)return;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            if(phase==0)
            {
                if(SceneManager.GetActiveScene().name!="MainMenu") {SceneManager.LoadScene("MainMenu");return;}
                var backdrop=UnityEngine.Object.FindAnyObjectByType<RallyMenuBackdropScene>();
                if(backdrop==null||now-stage<5)return;
                var menu=UnityEngine.Object.FindAnyObjectByType<RallyMenuController>();
                typeof(RallyMenuController).GetMethod("RevealMainMenu",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(menu,null);
                File.AppendAllText(report,"READY after "+(now-began).ToString("F2")+"s; objects="+UnityEngine.Object.FindObjectsByType<Transform>().Length+"; resolution="+Screen.width+"x"+Screen.height+"\n");
                initialCamera=Camera.main.transform.position;stage=now;phase=1;return;
            }
            if(phase==1)
            {
                if(now-stage<4)return;
                if(!sampling){sampling=true;times.Clear();lastFrame=Time.frameCount;began=now;}
                if(Time.frameCount!=lastFrame){times.Add(Time.unscaledDeltaTime);lastFrame=Time.frameCount;}
                if(now-began<12)return;
                if(Screen.width!=2560||Screen.height!=1440)throw new Exception("Game view resolution changed; sample is not comparable.");
                if(times.Count<30)throw new Exception("Insufficient menu frames.");
                float[] ordered=times.OrderBy(x=>x).ToArray();
                File.AppendAllText(report,"FPS="+(1f/times.Average()).ToString("F1")+"; frame p95 ms="+(ordered[(int)(ordered.Length*.95f)]*1000).ToString("F1")+"; samples="+times.Count+"; draw calls="+UnityStats.drawCalls+"; triangles="+UnityStats.triangles+"\n");
                if(Vector3.Distance(initialCamera,Camera.main.transform.position)<.1f)throw new Exception("Background camera stopped animating.");
                ScreenCapture.CaptureScreenshot("Logs/FourUX_Menu_"+(optimized?"After":"Before")+".png");
                if(!optimized){Finish(null);return;}
                stage=now;phase=8;return;
            }
            if(phase==8&&now-stage>1)
            {
                var controls=UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.name=="Controls Button");controls.onClick.Invoke();
                if(GameObject.Find("Keyboard Controls")==null)throw new Exception("Menu Controls did not open immediately.");
                UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.name=="Back Button").onClick.Invoke();
                File.AppendAllText(report,"PASS: animated background and responsive Controls/Back buttons\n");
                RallyGameSession.SelectCircuit(0);SceneManager.LoadScene("Circuit_01");stage=now;phase=2;return;
            }
            if(phase==2&&now-stage>3)
            {
                ValidateHud(false);stage=now;phase=3;return;
            }
            if(phase==3&&now-stage>1)
            {
                ScreenCapture.CaptureScreenshot("Logs/FourUX_HUD_1P.png");stage=now;phase=4;return;
            }
            if(phase==4&&now-stage>1)
            {
                RallyGameSession.SelectLocalPlayers(2);RallyGameSession.SelectCircuit(2);SceneManager.LoadScene("Circuit_03");stage=now;phase=5;return;
            }
            if(phase==5&&now-stage>3&&RallySplitScreen.Active!=null&&RallySplitScreen.Active.Ready)
            {
                ValidateHud(true);stage=now;phase=6;return;
            }
            if(phase==6&&now-stage>1)
            {
                ScreenCapture.CaptureScreenshot("Logs/FourUX_HUD_2P.png");stage=now;phase=7;return;
            }
            if(phase==7&&now-stage>1)Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    static void ValidateHud(bool split)
    {
        Canvas.ForceUpdateCanvases();
        var huds=UnityEngine.Object.FindObjectsByType<RallyRaceHud>();
        if(huds.Length!=(split?2:1))throw new Exception("Unexpected HUD count.");
        var note=UnityEngine.Object.FindObjectsByType<RallyBrakeWarningTrigger>().First();
        foreach(var warning in UnityEngine.Object.FindObjectsByType<RallyBrakeWarningSystem>())
        {
            warning.VehicleBody.isKinematic=true;warning.Enter(note);
            var panel=warning.GetComponentInChildren<Canvas>().GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="Brake Warning");
            if(panel.anchorMin!=new Vector2(.5f,1)||panel.anchoredPosition.y>0||panel.anchoredPosition.y< -110)throw new Exception("Note not in upper-center timer location.");
        }
        foreach(var hud in huds)
        {
            var serial=new SerializedObject(hud);var text=(TMP_Text)serial.FindProperty("timeText").objectReferenceValue;
            var panel=(RectTransform)text.transform.parent;
            if(panel.anchorMin!=Vector2.one||panel.pivot!=Vector2.one||text.fontSize<40)throw new Exception("Timer not legible in top-right corner.");
            if(GameObject.Find("Finish - Checkered Rally Arch")==null)throw new Exception("Finish arch missing in runtime scene.");
        }
        File.AppendAllText(report,"PASS: HUD "+(split?"2P":"1P")+" notes top-center, readable timer top-right, finish arch present\n");
    }
    static void Finish(Exception e)
    {
        File.AppendAllText(report,e==null?"COMPLETE: PASS\n":"FAIL: "+e+"\n");phase=99;Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static void Restore()
    {
        if(restored)return;restored=true;EditorApplication.update-=Tick;
        RallyGameSession.SelectCircuit(Mathf.Max(0,oldCircuit));RallyGameSession.SelectLocalPlayers(oldPlayers);
        QualitySettings.SetQualityLevel(oldQuality,true);QualitySettings.vSyncCount=oldSync;Application.targetFrameRate=oldCap;Application.runInBackground=oldBackground;
        EditorSceneManager.playModeStartScene=oldStart;EditorSceneManager.RestoreSceneManagerSetup(scenes);
        if(gameView!=null)gameView.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(gameView,oldViewIndex);
        if(sizeGroup!=null&&addedCustomIndex>=0)sizeGroup.GetType().GetMethod("RemoveCustomSize",Flags).Invoke(sizeGroup,new object[]{addedCustomIndex});
        addedCustomIndex=-1;
    }
    static void FixView()
    {
        var assembly=typeof(EditorWindow).Assembly;
        var viewType=assembly.GetType("UnityEditor.GameView");gameView=EditorWindow.GetWindow(viewType);
        oldViewIndex=(int)viewType.GetProperty("selectedSizeIndex",Flags).GetValue(gameView);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",Flags).GetValue(null);
        var groupKind=viewType.GetProperty("currentSizeGroupType",Flags).GetValue(gameView);
        sizeGroup=sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{groupKind});
        var type=sizeGroup.GetType();int builtins=(int)type.GetMethod("GetBuiltinCount",Flags).Invoke(sizeGroup,null);
        int customs=(int)type.GetMethod("GetCustomCount",Flags).Invoke(sizeGroup,null);
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");int index=-1;
        for(int i=0;i<builtins+customs;i++)
        {
            var value=type.GetMethod("GetGameViewSize",Flags).Invoke(sizeGroup,new object[]{i});
            if((int)sizeType.GetProperty("width",Flags).GetValue(value)==2560&&(int)sizeType.GetProperty("height",Flags).GetValue(value)==1440){index=i;break;}
        }
        addedCustomIndex=-1;
        if(index<0)
        {
            var kind=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            var value=Activator.CreateInstance(sizeType,Flags,null,new object[]{kind,2560,1440,"Rally UX temporary benchmark"},null);
            type.GetMethod("AddCustomSize",Flags).Invoke(sizeGroup,new[]{value});index=builtins+customs;addedCustomIndex=customs;
        }
        viewType.GetProperty("selectedSizeIndex",Flags).SetValue(gameView,index);gameView.Repaint();
    }
}
