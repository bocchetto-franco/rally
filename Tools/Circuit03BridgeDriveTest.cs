using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class Circuit03BridgeDriveTest
{
    const string Report = "Logs/circuit03-bridge-drive-test.txt";
    static string previousScene, previousStart;
    static int players, phase;
    static bool background, loaded, prepared;
    static InputSettings.EditorInputBehaviorInPlayMode inputFocus;
    static InputSettings.BackgroundBehavior inputBackground;
    static double began, loadedAt;
    static Rigidbody body;
    static JrsVehicleController car;
    static Transform entry, exit, bridge, route;
    static Gamepad pad;
    static float maxDeviation, maximumSpeed;
    static int airborne;

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("Exit Play and save first.");
        previousScene = SceneManager.GetActiveScene().path;
        previousStart = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
        players = PlayerPrefs.GetInt("Rally.LocalPlayers",1); background = Application.runInBackground;
        inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
        inputBackground=InputSystem.settings.backgroundBehavior;
        File.WriteAllText(Report,"Bridge crossing test: actual bot plus player with virtual analog gamepad.\n");
        PlayerPrefs.SetInt("Rally.LocalPlayers",1);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Circuit_03.unity");
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += Mode;
        phase=0; loaded=false; prepared=false; began=EditorApplication.timeSinceStartup;
        EditorApplication.isPlaying=true;
    }
    static void Poll()
    {
        try
        {
            if (EditorApplication.isCompiling) throw new Exception("Compilation interrupted bridge test.");
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused=false; Application.runInBackground=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!loaded)
            {
                loaded=true; RallyGameSession.SelectLocalPlayers(1); SceneManager.LoadScene("Circuit_03");
                loadedAt=EditorApplication.timeSinceStartup; return;
            }
            if(EditorApplication.timeSinceStartup-loadedAt<2)return;
            if(!prepared)
            {
                prepared=true;
                bridge=GameObject.Find("Circuit 03 - Timber River Crossing").transform;
                entry=bridge.Find("Crossing Entry"); exit=bridge.Find("Crossing Exit"); route=GameObject.Find("AI_Waypoints").transform;
                foreach(var flow in UnityEngine.Object.FindObjectsByType<RallyRaceFlow>())flow.enabled=false;
                foreach(var m in UnityEngine.Object.FindObjectsByType<RallyCheckpointManager>())m.enabled=false;
                foreach(var c in UnityEngine.Object.FindObjectsByType<JrsVehicleController>())
                {
                    if(c.GetComponentInParent<RallyBotController>()!=null){car=c;break;}
                }
                if(car==null)throw new Exception("No bot available in single-player.");
                Place(); car.GetComponentInParent<RallyBotController>().Configure(car,body,route);
                began=EditorApplication.timeSinceStartup;
            }
            if(body==null)throw new Exception("Vehicle disappeared.");
            Vector3 local=bridge.InverseTransformPoint(body.position);
            if(Mathf.Abs(local.z)<20)maxDeviation=Mathf.Max(maxDeviation,Mathf.Abs(local.x));
            maximumSpeed=Mathf.Max(maximumSpeed,body.linearVelocity.magnitude*3.6f);
            if(Mathf.Abs(local.z)<18&& !car.frontLeftWheel.isGrounded&&!car.rearLeftWheel.isGrounded)airborne++;
            if(body.position.y < bridge.position.y-3)throw new Exception("Vehicle fell beneath bridge.");
            if(phase==1)
            {
                Vector3 target=route.Cast<Transform>().Select(t=>t.position).Where(p=>Vector3.Dot(p-body.position,body.transform.forward)>4)
                    .OrderBy(p=>(p-body.position).sqrMagnitude).First();
                Vector3 aim=body.transform.InverseTransformPoint(target);
                float steer=Mathf.Clamp(Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg/35,-1,1);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(steer,0),rightTrigger=maximumSpeed>75?.35f:1f});
            }
            if(Vector3.Dot(body.position-exit.position,exit.forward)>1 && Vector3.Distance(body.position,exit.position)<25)
            {
                if(maxDeviation>3.7f)throw new Exception("Insufficient rail margin: "+maxDeviation);
                File.AppendAllText(Report,$"PASS {(phase==0?"bot":"player")}: crossed entry/deck/exit, max lateral {maxDeviation:F2}m, speed {maximumSpeed:F1}km/h, airborne samples {airborne}.\n");
                if(phase==1){Finish(null);return;}
                car.gameObject.SetActive(false);
                car=GameObject.Find("Porsche 911 SC Rally").GetComponent<JrsVehicleController>();
                pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();
                phase=1;Place(); began=EditorApplication.timeSinceStartup;
            }
            if(EditorApplication.timeSinceStartup-began>30)throw new Exception("Vehicle failed to cross within 30 seconds. Position="+body.position);
        }
        catch(Exception e){Finish(e);}
    }
    static void Place()
    {
        body=car.GetComponent<Rigidbody>(); body.position=entry.position+Vector3.up*.65f;body.rotation=entry.rotation;
        body.linearVelocity=entry.forward*11;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
        maxDeviation=0;maximumSpeed=0;airborne=0;
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Poll;
        if(pad!=null){InputSystem.RemoveDevice(pad);pad=null;}
        File.AppendAllText(Report,error==null?"COMPLETE: PASS\n":"FAILED: "+error+"\n");
        EditorApplication.isPlaying=false;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredEditMode)return;
        EditorApplication.update-=Poll;EditorApplication.playModeStateChanged-=Mode;
        PlayerPrefs.SetInt("Rally.LocalPlayers",players);Application.runInBackground=background;
        InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;InputSystem.settings.backgroundBehavior=inputBackground;
        EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previousStart)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previousStart);
        if(!string.IsNullOrEmpty(previousScene))EditorSceneManager.OpenScene(previousScene);
    }
}
