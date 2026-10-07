using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Interactive-safe test: restores editor scenes/options, never exits Unity.</summary>
[InitializeOnLoad]
public static class RallyReplacementVehiclePlayTest
{
 const string Key="ReplacementCarsTest.";
 const string Report="Logs/replacement-cars-play.txt";
 static Gamepad pad;
 static JrsVehicleController car;
 static float began;
 static Vector3 start;
 static double deadline;
 [Serializable] class Setup { public SceneSetup[] scenes; }
 static readonly string[] Prefs={"Rally.SelectedVehicle","Rally.SelectedVehicleTwo","Rally.SelectedCircuit"};
 static RallyReplacementVehiclePlayTest(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 [MenuItem("Tools/Rally/Verify Replacement Cars In Play")]
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save current scene before testing");
  SessionState.SetString(Key+"Setup",JsonUtility.ToJson(new Setup{scenes=EditorSceneManager.GetSceneManagerSetup()}));
  foreach(var p in Prefs){SessionState.SetBool(Key+p+"Exists",PlayerPrefs.HasKey(p));SessionState.SetString(Key+p,PlayerPrefs.GetString(p));}
  SessionState.SetBool(Key+"PlayersExists",PlayerPrefs.HasKey("Rally.LocalPlayers"));SessionState.SetInt(Key+"Players",PlayerPrefs.GetInt("Rally.LocalPlayers",1));
  SessionState.SetString(Key+"Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
  SessionState.SetBool(Key+"Background",Application.runInBackground);
  Application.runInBackground=true;
  SessionState.SetBool(Key+"Active",true);SessionState.SetInt(Key+"Phase",0);SessionState.SetInt(Key+"Case",0);
  File.WriteAllText(Report,"RUNNING: 6 single-player selections plus split-screen setup\n");
  deadline=EditorApplication.timeSinceStartup+600;
  EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/VehicleCircuitSelection.unity");
  EditorApplication.isPlaying=true;
 }
 static void Tick()
 {
  if(!SessionState.GetBool(Key+"Active",false)||!EditorApplication.isPlaying)return;
  if(deadline==0)deadline=EditorApplication.timeSinceStartup+600;
  try {
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout");
   int test=SessionState.GetInt(Key+"Case",0),phase=SessionState.GetInt(Key+"Phase",0);
   if(phase==0){RallyGameSession.SelectLocalPlayers(1);SceneManager.LoadScene(RallyGameSession.SelectionScene);SessionState.SetInt(Key+"Phase",1);return;}
   if(phase==1){
    int index=1+test%2,circuit=test/2;
    if(test==6){RallyGameSession.SelectLocalPlayers(2);RallyGameSession.SelectVehicleForPlayer(0,1);RallyGameSession.SelectVehicleForPlayer(1,2);SceneManager.LoadScene("Circuit_03");SessionState.SetInt(Key+"Phase",4);return;}
    var select=GameObject.Find("Vehicle "+index+" Button")?.GetComponent<Button>();if(select==null)return;
    select.onClick.Invoke();GameObject.Find("Circuit "+(circuit+1)+" Button").GetComponent<Button>().onClick.Invoke();
    if(RallyGameSession.SelectedVehicle!=RallyPlayerVehicleSelection.Names[index])throw new Exception("Wrong menu selection");
    GameObject.Find("Race Button").GetComponent<Button>().onClick.Invoke();SessionState.SetInt(Key+"Phase",2);return;
   }
   if(phase==2){
    if(SceneManager.GetActiveScene().name!=RallyGameSession.CircuitScenes[test/2])return;
    car=GameObject.Find(RallyGameSession.VehicleName)?.GetComponent<JrsVehicleController>();if(car==null)return;
    Verify(car,RallyPlayerVehicleSelection.Names[1+test%2]);
    pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();start=car.transform.position;began=Time.time;
    SessionState.SetInt(Key+"Phase",3);return;
   }
   if(phase==3){
    InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=.65f});pad.MakeCurrent();
    if(Time.time-began<3.5f)return;
    float distance=Vector3.Distance(start,car.transform.position);
    if(distance<2f)throw new Exception("Car failed to accelerate: "+distance);
    var wheels=new[]{car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel};
    if(wheels.Count(w=>w.isGrounded)<2)throw new Exception("Lost wheel contact");
    var visual=car.GetComponentInChildren<RallyVehicleVisual>();
    for(int i=0;i<4;i++){wheels[i].GetWorldPose(out Vector3 p,out _);if(Vector3.Distance(p,visual.wheels[i].position)>.15f)throw new Exception("Wheel visual detached");}
    File.AppendAllText(Report,"PASS "+SceneManager.GetActiveScene().name+" / "+visual.name+" menu, acceleration="+distance.ToString("F2")+"m, contact and suspension visuals\n");
    InputSystem.RemoveDevice(pad);pad=null;SessionState.SetInt(Key+"Case",test+1);SessionState.SetInt(Key+"Phase",0);return;
   }
   if(phase==4){
    var split=RallySplitScreen.Active;if(split==null||!split.Ready)return;
    Verify(split.PlayerOne,RallyPlayerVehicleSelection.BmwName);Verify(split.PlayerTwo,RallyPlayerVehicleSelection.AudiName);
    if(Vector3.Distance(split.PlayerOne.transform.position,split.PlayerTwo.transform.position)<2.5f)throw new Exception("Split-screen grid overlap");
    File.AppendAllText(Report,"PASS Circuit_03 split-screen: independent BMW/Audi models, matching player tuning, separate grid positions\nCOMPLETE: PASS\n");Finish(null);
   }
  } catch(Exception e){Finish(e);}
 }
 static void Verify(JrsVehicleController c,string name)
 {
  var v=c.GetComponentInChildren<RallyVehicleVisual>();var rb=c.GetComponent<Rigidbody>();
  if(v==null||v.name!=name+" Visual"||!v.useAuthoredScale)throw new Exception("Wrong visual: "+name);
  if(!c.enabled||Mathf.Abs(rb.mass-1450)>.01f||Mathf.Abs(rb.angularDamping-3)>.01f)throw new Exception("Player tuning changed");
 }
 static void Finish(Exception error)
 {
  if(error!=null)File.AppendAllText(Report,"FAIL: "+error+"\n");
  if(pad!=null){InputSystem.RemoveDevice(pad);pad=null;}
  SessionState.SetBool(Key+"Active",false);SessionState.SetBool(Key+"Restore",true);Time.timeScale=1;EditorApplication.isPlaying=false;
 }
 static void Changed(PlayModeStateChange state)
 {
  if(state!=PlayModeStateChange.EnteredEditMode)return;
  if(!SessionState.GetBool(Key+"Restore",false)&&!SessionState.GetBool(Key+"Active",false))return;
  Application.runInBackground=SessionState.GetBool(Key+"Background",false);
  foreach(var p in Prefs){if(SessionState.GetBool(Key+p+"Exists",false))PlayerPrefs.SetString(p,SessionState.GetString(Key+p,""));else PlayerPrefs.DeleteKey(p);}
  if(SessionState.GetBool(Key+"PlayersExists",false))PlayerPrefs.SetInt("Rally.LocalPlayers",SessionState.GetInt(Key+"Players",1));else PlayerPrefs.DeleteKey("Rally.LocalPlayers");PlayerPrefs.Save();RallyGameSession.RestoreSavedState();
  EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"Start",""));
  var setup=JsonUtility.FromJson<Setup>(SessionState.GetString(Key+"Setup",""));if(setup?.scenes!=null)EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
  SessionState.SetBool(Key+"Active",false);SessionState.SetBool(Key+"Restore",false);deadline=0;
 }
}
