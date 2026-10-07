using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class RallyCheckpointLapCheck
{
 const string Report="Logs/checkpoint-lap-check.txt";
 static bool active,restoring,baseline;
 static int test,phase;
 static double deadline;
 static SceneSetup[] setup;
 static SceneAsset start;
 static bool background,optionsEnabled;
 static EnterPlayModeOptions options;
 static Dictionary<string,string> strings;
 static Dictionary<string,int> ints;
 static Dictionary<string,float> floats;
 static HashSet<string> existed;
 static CheckpointLapDriver[] drivers;
 public static void RunBaseline()=>Begin(true);
 public static void Run()=>Begin(false);
 static void Begin(bool original)
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save current scene before testing.");
  baseline=original;test=0;phase=0;active=true;restoring=false;
  setup=EditorSceneManager.GetSceneManagerSetup();start=EditorSceneManager.playModeStartScene;
  background=Application.runInBackground;optionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;options=EditorSettings.enterPlayModeOptions;
  strings=new Dictionary<string,string>();ints=new Dictionary<string,int>();floats=new Dictionary<string,float>();existed=new HashSet<string>();
  foreach(var k in new[]{"Rally.SelectedVehicle","Rally.SelectedVehicleTwo","Rally.SelectedCircuit"}){Remember(k);strings[k]=PlayerPrefs.GetString(k);}
  foreach(var k in new[]{"Rally.LocalPlayers","Rally.BotDifficulty"}){Remember(k);ints[k]=PlayerPrefs.GetInt(k);}
  Remember("Rally.LastRaceTime");floats["Rally.LastRaceTime"]=PlayerPrefs.GetFloat("Rally.LastRaceTime");
  foreach(var circuit in RallyGameSession.CircuitScenes)
  {
   string prefix="Rally.BestTimes."+circuit;Remember(prefix+".Count");ints[prefix+".Count"]=PlayerPrefs.GetInt(prefix+".Count");
   foreach(var suffix in new[]{".Best",".0",".1",".2",".3",".4"}){Remember(prefix+suffix);floats[prefix+suffix]=PlayerPrefs.GetFloat(prefix+suffix);}
  }
  Application.runInBackground=true;EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)3;
  EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Circuit_01.unity");
  Directory.CreateDirectory("Logs");File.WriteAllText(original?"Logs/checkpoint-lap-baseline.txt":Report,"Real trigger crossing in Play (automated kinematic route, not SendMessage).\n");
  EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
  deadline=EditorApplication.timeSinceStartup+120;EditorApplication.isPlaying=true;
 }
 static void Remember(string key){if(PlayerPrefs.HasKey(key))existed.Add(key);}
 static void Tick()
 {
  if(!active||!EditorApplication.isPlaying)return;
  try {
   EditorApplication.QueuePlayerLoopUpdate();
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Test timed out, phase "+phase+", time="+Time.time+", frame="+Time.frameCount+", progress="+string.Join(",",drivers?.Select(d=>d.Distance.ToString("F1")+"m gate "+d.Timer.NextCheckpoint)??Array.Empty<string>()));
   int circuit=baseline?1:test%3,mode=baseline?1:test/3;
   if(phase==0)
   {
    Time.timeScale=1;RallyGameSession.SelectLocalPlayers(mode==2?2:1);RallyGameSession.SelectVehicleForPlayer(0,mode);RallyGameSession.SelectVehicleForPlayer(1,1);
    RallyGameSession.SelectCircuit(circuit);SceneManager.LoadScene(RallyGameSession.CircuitScenes[circuit]);phase=1;return;
   }
   if(phase==1)
   {
    var split=RallySplitScreen.Active;if(mode==2&&(split==null||!split.Ready))return;
    var manager=split!=null?split.TimerOne:UnityEngine.Object.FindAnyObjectByType<RallyCheckpointManager>();
    if(manager==null)return;
    var vehicle=GameObject.Find(RallyGameSession.VehicleName)?.GetComponent<JrsVehicleController>();if(vehicle==null)return;
    var route=GameObject.Find("AI_Waypoints").transform;
    foreach(var bot in UnityEngine.Object.FindObjectsByType<RallyBotController>())bot.gameObject.SetActive(false);
    foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>())camera.enabled=false;
    foreach(var zone in UnityEngine.Object.FindObjectsByType<RallyPuddleSlowZone>())zone.enabled=false;
    var list=new List<CheckpointLapDriver>{Prepare(vehicle,manager,route,mode==1?9:0)};
    if(mode==2)list.Add(Prepare(split.PlayerTwo,split.TimerTwo,route,-9));
    drivers=list.ToArray();Application.runInBackground=true;Time.timeScale=4;phase=2;deadline=EditorApplication.timeSinceStartup+90;return;
   }
   if(phase==2)
   {
    foreach(var driver in drivers)driver.Advance();
    bool finished=drivers.All(d=>d.Timer.IsFinished);
    if(!finished&&!drivers.All(d=>d.AtEnd))return;
    if(baseline)
    {
     File.AppendAllText("Logs/checkpoint-lap-baseline.txt","Circuit_02 at +9m lateral: next="+drivers[0].Timer.NextCheckpoint+"/"+drivers[0].Timer.CheckpointCount+", finished="+finished+"\n"+(finished?"Baseline did not reproduce.":"REPRODUCED: missed narrow checkpoint keeps lap unfinished.")+"\n");
     Stop(null);return;
    }
    if(!finished)throw new Exception(RallyGameSession.CircuitScenes[circuit]+": incomplete lap, counters "+string.Join(",",drivers.Select(d=>d.Timer.NextCheckpoint+"/"+d.Timer.CheckpointCount)));
    phase=3;return;
   }
   if(phase==3)
   {
    if(GameObject.Find("Race Finish Canvas")==null||Time.timeScale!=0)return;
    foreach(var d in drivers)
    {
     // The kinematic test driver can retain a reported MovePosition velocity;
     // unlike a normally driven car it was already kinematic before FinishRace.
     if(d.Timer.IsRunning||d.Timer.ElapsedTime<=0||d.Vehicle.enabled||!d.Body.isKinematic)
      throw new Exception("Invalid final timer/control state: running="+d.Timer.IsRunning+", elapsed="+d.Timer.ElapsedTime+", control="+d.Vehicle.enabled+", kinematic="+d.Body.isKinematic);
    }
    File.AppendAllText(Report,"PASS "+RallyGameSession.CircuitScenes[circuit]+" / "+(mode==0?"Porsche center":mode==1?"BMW outside road, +9m":"2P Audi center / BMW -9m")+": all "+drivers[0].Timer.CheckpointCount+" ordered gates, stopped timer, disabled control, paused results menu.\n");
    test++;if(test==9){File.AppendAllText(Report,"COMPLETE: PASS\n");Stop(null);}else{phase=0;deadline=EditorApplication.timeSinceStartup+120;}
   }
  }catch(Exception e){Stop(e);}
 }
 static CheckpointLapDriver Prepare(JrsVehicleController vehicle,RallyCheckpointManager timer,Transform route,float lateral)
 {
  vehicle.enabled=false;
  foreach(var dynamics in UnityEngine.Object.FindObjectsByType<RallyVehicleDynamics>())if(dynamics.VehicleController==vehicle)dynamics.enabled=false;
  var body=vehicle.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;
  var driver=new CheckpointLapDriver();driver.Setup(vehicle,timer,route,lateral);return driver;
 }
 static void Stop(Exception error)
 {
  if(error!=null)File.AppendAllText(baseline?"Logs/checkpoint-lap-baseline.txt":Report,"FAILED: "+error+"\n");
  active=false;restoring=true;EditorApplication.update-=Tick;Time.timeScale=1;EditorApplication.isPlaying=false;
 }
 static void Changed(PlayModeStateChange state)
 {
  if(state!=PlayModeStateChange.EnteredEditMode||(!restoring&&!active))return;
  foreach(var p in strings){if(existed.Contains(p.Key))PlayerPrefs.SetString(p.Key,p.Value);else PlayerPrefs.DeleteKey(p.Key);}
  foreach(var p in ints){if(existed.Contains(p.Key))PlayerPrefs.SetInt(p.Key,p.Value);else PlayerPrefs.DeleteKey(p.Key);}
  foreach(var p in floats){if(existed.Contains(p.Key))PlayerPrefs.SetFloat(p.Key,p.Value);else PlayerPrefs.DeleteKey(p.Key);}
  PlayerPrefs.Save();RallyGameSession.RestoreSavedState();
  Application.runInBackground=background;EditorSceneManager.playModeStartScene=start;EditorSettings.enterPlayModeOptionsEnabled=optionsEnabled;EditorSettings.enterPlayModeOptions=options;
  EditorSceneManager.RestoreSceneManagerSetup(setup);active=restoring=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Changed;
 }
}
public sealed class CheckpointLapDriver
{
 public RallyCheckpointManager Timer{get;private set;}
 public JrsVehicleController Vehicle{get;private set;}
 public Rigidbody Body{get;private set;}
 public bool AtEnd{get;private set;}
 public float Distance=>distance;
 float lastTime;
 Vector3[] points;float[] lengths;float total,distance,side;int observed;
 public void Setup(JrsVehicleController car,RallyCheckpointManager timer,Transform route,float lateral)
 {
  Vehicle=car;Timer=timer;Body=car.GetComponent<Rigidbody>();side=lateral;lastTime=Time.fixedTime;
  points=route.Cast<Transform>().Select(t=>t.position).ToArray();lengths=new float[points.Length];
  for(int i=0;i<points.Length;i++){lengths[i]=Vector3.Distance(points[i],points[(i+1)%points.Length]);total+=lengths[i];}
  var pose=Sample(0);Body.transform.SetPositionAndRotation(pose.position,pose.rotation);Body.position=pose.position;Body.rotation=pose.rotation;Physics.SyncTransforms();
 }
 public void Advance()
 {
  if(Timer==null||Timer.IsFinished||AtEnd)return;
  if(Time.fixedTime<=lastTime)return;
  float step=Mathf.Min(.04f,Time.fixedTime-lastTime);lastTime=Time.fixedTime;
  if(Timer.NextCheckpoint<observed)throw new Exception("Checkpoint order moved backwards.");observed=Timer.NextCheckpoint;
  distance=Mathf.Min(total-.05f,distance+45*step);
  var pose=Sample(distance);Body.MovePosition(pose.position);Body.MoveRotation(pose.rotation);
  AtEnd=distance>=total-.06f;
 }
 (Vector3 position,Quaternion rotation) Sample(float d)
 {
  int i=0;while(i<lengths.Length-1&&d>lengths[i]){d-=lengths[i];i++;}
  Vector3 delta=points[(i+1)%points.Length]-points[i];Vector3 forward=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
  return(points[i]+delta*(d/Mathf.Max(.001f,lengths[i]))+Vector3.Cross(Vector3.up,forward)*side+Vector3.up*.7f,Quaternion.LookRotation(forward));
 }
}
