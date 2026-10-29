using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RallyOctoberGameplayCheck
{
    const string Report="Logs/Gameplay_20261028/play.txt";
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static IEnumerator steps;
    static bool stopping,entered,background,paused;
    static SceneSetup[] setup;
    static SceneAsset start;
    static double deadline,next;
    static int frame;
    static Gamepad[] pads;
    static InputDevice[] disabled;
    static InputSettings originalInput,testInput;
    static string inputJson;
    static readonly string[] Keys={"Rally.LocalPlayers","Rally.SelectedCircuit","Rally.SelectedVehicle","Rally.SelectedVehicleTwo"};
    static bool[] had;
    static string[] strings;
    static int players;
    [MenuItem("Tools/Rally/Verify October Gameplay")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new Exception("Editor must be idle");
        setup=EditorSceneManager.GetSceneManagerSetup();start=EditorSceneManager.playModeStartScene;background=Application.runInBackground;paused=EditorApplication.isPaused;
        had=Keys.Select(PlayerPrefs.HasKey).ToArray();strings=Keys.Select(k=>PlayerPrefs.GetString(k)).ToArray();players=PlayerPrefs.GetInt(Keys[0],1);
        originalInput=InputSystem.settings;inputJson=EditorJsonUtility.ToJson(originalInput);testInput=Object.Instantiate(originalInput);testInput.hideFlags=HideFlags.HideAndDontSave;
        testInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=testInput;
        stopping=entered=false;frame=-1;next=0;deadline=EditorApplication.timeSinceStartup+1200;
        Directory.CreateDirectory("Logs/Gameplay_20261028");File.WriteAllText(Report,"October gameplay: six real Play setups, virtual DualSense and generic Xbox layout. No scene writes.\n");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        // Scene reload can be disabled by the project. Open explicitly rather than
        // relying on playModeStartScene to replace an Editor recovery scene.
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Application.runInBackground=true;EditorApplication.isPaused=false;steps=Steps();EditorApplication.update+=Tick;
        EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(stopping){if(!EditorApplication.isPlayingOrWillChangePlaymode)Restore();return;}
        if(EditorApplication.isPlaying){entered=true;EditorApplication.QueuePlayerLoopUpdate();}
        else if(entered&&!EditorApplication.isPlayingOrWillChangePlaymode){End(new Exception("Play stopped before completion"));return;}
        if(EditorApplication.timeSinceStartup>deadline){End(new Exception("Deadline exceeded"));return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Time.frameCount<frame+3 || EditorApplication.timeSinceStartup<next)return;
        frame=Time.frameCount;
        try{if(!steps.MoveNext()){End(null);return;}next=EditorApplication.timeSinceStartup+(steps.Current is float wait?wait:0);}
        catch(Exception e){End(e);}
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);File.AppendAllText(Report,"PASS: "+label+"\n");}
    static void State(Gamepad pad,params GamepadButton[] buttons)
    {
        if(pad is DualSenseGamepadHID)
        {
            byte mask=8;if(buttons.Contains(GamepadButton.West))mask|=16;if(buttons.Contains(GamepadButton.South))mask|=32;
            if(buttons.Contains(GamepadButton.East))mask|=64;if(buttons.Contains(GamepadButton.North))mask|=128;
            InputSystem.QueueStateEvent(pad,new DualSenseHIDInputReport{leftStickX=128,leftStickY=128,rightStickX=128,rightStickY=128,buttons0=mask,buttons1=buttons.Contains(GamepadButton.RightStick)?(byte)128:(byte)0});
        }
        else {var state=new GamepadState();foreach(var button in buttons)state=state.WithButton(button);InputSystem.QueueStateEvent(pad,state);}
    }
    static void Place(Rigidbody body,Vector3 position,Quaternion rotation,Vector3 velocity)
    {
        body.transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;
        if(!body.isKinematic){body.linearVelocity=velocity;body.angularVelocity=Vector3.zero;}Physics.SyncTransforms();
    }
    static IEnumerator Steps()
    {
        while(SceneManager.GetActiveScene().name!="MainMenu")yield return null;
        disabled=InputSystem.devices.Where(d=>(d is Keyboard || d is Gamepad)&&d.enabled).ToArray();foreach(var d in disabled)InputSystem.DisableDevice(d);
        pads=new Gamepad[]{InputSystem.AddDevice<DualSenseGamepadHID>(),InputSystem.AddDevice<Gamepad>()};
        foreach(var pad in pads)State(pad);yield return null;
        for(int circuit=0;circuit<3;circuit++)for(int count=1;count<=2;count++)
        {
            RallyGameSession.SelectLocalPlayers(count);RallyGameSession.SelectVehicleForPlayer(0,circuit);RallyGameSession.SelectVehicleForPlayer(1,(circuit+1)%3);
            RallyRaceLoading.Load(RallyGameSession.CircuitScenes[circuit]);
            while(RallyRaceLoading.IsPreparing || SceneManager.GetActiveScene().name!=RallyGameSession.CircuitScenes[circuit])yield return null;
            yield return .25f;
            var cars=count==2?new[]{RallySplitScreen.Active.PlayerOne,RallySplitScreen.Active.PlayerTwo}:new[]{Object.FindObjectsByType<JrsVehicleController>().Single(c=>c.name==RallyGameSession.VehicleName)};
            var bots=Object.FindObjectsByType<RallyBotController>();Check(bots.Length==(count==2?0:3),"correct roster "+(circuit+1)+"/"+count+"P");
            foreach(var bot in bots)
            {
                var serialized=new SerializedObject(bot);
                Check(Mathf.Abs(serialized.FindProperty("easySpeedMultiplier").floatValue-.65025f)<.00001f && Mathf.Abs(serialized.FindProperty("easyBrakingMultiplier").floatValue-.765f)<.00001f,"Easy both multipliers reduced 10%");
                bot.gameObject.SetActive(false);
            }
            var zones=Object.FindObjectsByType<RallyTurboZone>().OrderBy(z=>z.name).ToArray();Check(zones.Length==3,"three lightning pickups retained");
            for(int p=0;p<count;p++)
            {
                var car=cars[p];var body=car.GetComponent<Rigidbody>();
                var input=car.GetComponent<RallyLocalPlayerInput>()??car.gameObject.AddComponent<RallyLocalPlayerInput>();
                input.Configure(RallyLocalPlayerInput.InputDevice.Gamepad,96);input.PairGamepad(pads[p]);car.SetLocalInput(input);
                var dynamics=Object.FindObjectsByType<RallyVehicleDynamics>().Single(d=>d.VehicleController==car);dynamics.SetLocalInput(input);
                Check(car.maxSteerAngle==55 && dynamics.SteeringResponse==96,"same agile steering in selected model");
                var penalty=car.GetComponent<RallyOffTrackSpeedPenalty>();Check(penalty!=null && penalty.RetainedSpeed==.75f,"off-track retains 75%, never 50%");
                var timer=count==2?(p==0?RallySplitScreen.Active.TimerOne:RallySplitScreen.Active.TimerTwo):Object.FindAnyObjectByType<RallyCheckpointManager>();
                input.Checkpoints=timer;timer.BindVehicle(body);
                var turbo=car.GetComponent<RallyVehicleTurbo>();State(pads[p]);yield return .1f;
                State(pads[p],GamepadButton.North);yield return .1f;Check(turbo.ActivationCount==0 && !turbo.IsBoosting,"Triangle/Y without charge does nothing");
                Check(timer.NextCheckpoint==0,"Triangle is no longer checkpoint reset");State(pads[p]);yield return .1f;
                float grip=car.rearLeftWheel.sidewaysFriction.stiffness;
                State(pads[p],GamepadButton.West);yield return .12f;
                Check(input.Handbrake && car.rearLeftWheel.sidewaysFriction.stiffness<grip*.6f,"Square/X holds temporary rear drift grip: held="+input.Handbrake+", "+grip+" -> "+car.rearLeftWheel.sidewaysFriction.stiffness);
                State(pads[p]);yield return .12f;Check(!input.Handbrake && Mathf.Abs(car.rearLeftWheel.sidewaysFriction.stiffness-grip)<.01f,"release restores baseline rear grip");
                var zone=zones[p];Place(body,zone.transform.TransformPoint(new Vector3(0,.7f,-8)),zone.transform.rotation,zone.transform.forward*22);
                yield return .45f;Check(turbo.HasStoredTurbo && !turbo.IsBoosting,"real pickup crossing stores one, without automatic boost");
                Check(!turbo.Collect(2.25f,4.5f,180,35),"inventory capacity exactly one");
                int progress=timer.NextCheckpoint;State(pads[p],GamepadButton.North);yield return .1f;
                Check(turbo.IsBoosting && !turbo.HasStoredTurbo && turbo.ActivationCount==1,"Triangle/Y consumes own charge and starts existing boost");
                Check(timer.NextCheckpoint==progress,"turbo never resets race progress");
                if(count==2)Check(!cars[1-p].GetComponent<RallyVehicleTurbo>().IsBoosting || p==1,"J1 activation does not boost J2");
                State(pads[p]);turbo.CancelBoost();
                var constraints=body.constraints;body.constraints=RigidbodyConstraints.FreezeAll;
                Vector3 position=body.position;Place(body,position,Quaternion.Euler(0,body.rotation.eulerAngles.y,110),Vector3.zero);
                State(pads[p],GamepadButton.East);yield return .12f;
                Check(Vector3.Dot(body.rotation*Vector3.up,Vector3.up)>.95f && Vector2.Distance(new Vector2(body.position.x,body.position.z),new Vector2(position.x,position.z))<.05f,"Circle/B rights locally without checkpoint teleport");
                Check(timer.NextCheckpoint==progress,"recovery retains checkpoint progress");State(pads[p]);body.constraints=constraints;
                var hud=Object.FindObjectsByType<RallyRaceAssistHud>().Single(h=>h.GetComponent<RallyRaceHud>()!=null && Field<Rigidbody>(h,"body")==body);
                var region=hud.transform.Find("Player Viewport");
                Check(Field<UnityEngine.UI.Image>(hud,"turboPanel").transform.parent==(region!=null?region:hud.transform),"turbo HUD belongs to its own viewport");
                Check(Field<GameObject>(hud,"waterPanel").transform.parent==(region!=null?region:hud.transform),"water HUD belongs to its own viewport");
                Check(turbo.Collect(2.25f,4.5f,180,35),"inventory available again after use");
                Check(Field<RallyWaterSurface[]>(hud,"puddles").Length>0,"water warning includes visual-only pools");
                var station=Field<float[]>(hud,"puddleStations")[0];var route=Field<Vector3[]>(hud,"points");var distances=Field<float[]>(hud,"distances");var puddle=Field<RallyWaterSurface[]>(hud,"puddles")[0];
                body.isKinematic=true;Vector3 ahead=At(route,distances,station-25);var heading=Quaternion.LookRotation(At(route,distances,station-23)-ahead,Vector3.up);Place(body,ahead+Vector3.up*.7f,heading,Vector3.zero);yield return .25f;
                Check(hud.WaterWarningVisible && hud.WarnedPuddle==puddle,"cyan water note before the puddle");
                if(p==0 && circuit==0)
                {
                    var follow=Object.FindObjectsByType<JrsFollowCamera>().Single(f=>f.target==body.transform);
                    bool enabled=follow.enabled;follow.enabled=false;
                    follow.transform.position=body.transform.TransformPoint(follow.offset);follow.transform.LookAt(body.position+Vector3.up);
                    ScreenCapture.CaptureScreenshot("Logs/Gameplay_20261028/HUD_"+count+"P.png");yield return .2f;
                    follow.enabled=enabled;
                }
                Place(body,At(route,distances,station+35)+Vector3.up*.7f,Quaternion.identity,Vector3.zero);yield return .25f;
                Check(hud.WarnedPuddle!=puddle,"water note disappears after passing its puddle");body.isKinematic=false;
            }
            var shakes=Object.FindObjectsByType<RallyCameraRoadShake>();Check(shakes.Length==count,"one independently bound shake per camera");
            foreach(var shake in shakes)
            {
                var camera=shake.GetComponent<Camera>();Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
                typeof(RallyCameraRoadShake).GetField("strength",Private).SetValue(shake,.5f);
                typeof(RallyCameraRoadShake).GetMethod("BeforeRender",Private).Invoke(shake,new object[]{default(UnityEngine.Rendering.ScriptableRenderContext),camera});
                typeof(RallyCameraRoadShake).GetMethod("AfterRender",Private).Invoke(shake,new object[]{default(UnityEngine.Rendering.ScriptableRenderContext),camera});
                Check(Vector3.Distance(camera.transform.position,position)<.00001f && Quaternion.Angle(camera.transform.rotation,rotation)<.001f,"camera restores exact follow pose, no feedback");
            }
            File.AppendAllText(Report,"CASE COMPLETE "+RallyGameSession.CircuitScenes[circuit]+" "+count+"P\n");
            SceneManager.LoadScene("MainMenu");yield return .3f;
        }
    }
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static Vector3 At(Vector3[] points,float[] distances,float station)
    {
        station=Mathf.Repeat(station,distances[points.Length]);for(int i=0;i<points.Length;i++)if(station<=distances[i+1])return Vector3.Lerp(points[i],points[(i+1)%points.Length],Mathf.InverseLerp(distances[i],distances[i+1],station));return points[0];
    }
    static void End(Exception error)
    {
        File.AppendAllText(Report,error==null?"COMPLETE: PASS\n":"FAIL: "+error+"\n");stopping=true;
        if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
        if(disabled!=null)foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);
        EditorApplication.isPlaying=false;
    }
    static void Restore()
    {
        EditorApplication.update-=Tick;
        for(int i=0;i<Keys.Length;i++){if(!had[i])PlayerPrefs.DeleteKey(Keys[i]);else if(i==0)PlayerPrefs.SetInt(Keys[i],players);else PlayerPrefs.SetString(Keys[i],strings[i]);}
        PlayerPrefs.Save();RallyGameSession.RestoreSavedState();if(originalInput!=null)InputSystem.settings=originalInput;
        else{var restored=ScriptableObject.CreateInstance<InputSettings>();EditorJsonUtility.FromJsonOverwrite(inputJson,restored);restored.hideFlags=HideFlags.HideAndDontSave;InputSystem.settings=restored;}
        if(testInput!=null && testInput!=InputSystem.settings)Object.DestroyImmediate(testInput);
        EditorSceneManager.playModeStartScene=start;EditorSceneManager.RestoreSceneManagerSetup(setup);Application.runInBackground=background;EditorApplication.isPaused=paused;
        File.AppendAllText(Report,"RESTORED: devices, preferences, scenes, settings; no source scenes saved\n");
    }
}
