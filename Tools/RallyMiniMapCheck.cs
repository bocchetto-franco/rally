using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class RallyMiniMapCheck
{
 const string Report="Logs/mini-map-check.txt";
 static readonly string[] Keys={"Rally.SelectedCircuit","Rally.SelectedVehicle","Rally.SelectedVehicleTwo","Rally.LocalPlayers"};
 static bool[] had;static string[] values;static int players;static SceneSetup[] scenes;static SceneAsset start;
 static bool background,paused,exited;static float scale;static int phase,test,lastFrame;static double deadline,next;
 static RallyMiniMap[] maps;static RallyVehiclePresentationCache.Vehicle[] roster;static int[] builds;
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||BuildPipeline.isBuildingPlayer)throw new Exception("Editor must be idle");
  if(!EditorSettings.enterPlayModeOptionsEnabled||(EditorSettings.enterPlayModeOptions&EnterPlayModeOptions.DisableDomainReload)==0)throw new Exception("External verifier needs Disable Domain Reload");
  had=Keys.Select(PlayerPrefs.HasKey).ToArray();values=Keys.Select(k=>PlayerPrefs.GetString(k)).ToArray();players=PlayerPrefs.GetInt(Keys[3],1);
  scenes=EditorSceneManager.GetSceneManagerSetup();start=EditorSceneManager.playModeStartScene;background=Application.runInBackground;paused=EditorApplication.isPaused;scale=Time.timeScale;
  phase=test=0;lastFrame=-1;next=0;exited=false;deadline=EditorApplication.timeSinceStartup+1200;
  Directory.CreateDirectory("Logs");File.WriteAllText(Report,"Minimap real Play: three scenes, 1P/2P, waypoint geometry, moving markers, UI bounds, no repeated route rebuilds\n");
  EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Mode;EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
  Application.runInBackground=true;EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
 }
 static void Tick()
 {
  if(phase==99){if(exited&&!EditorApplication.isPlayingOrWillChangePlaymode&&!Application.isPlaying)Restore();return;}
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Timeout case="+test+" phase="+phase));return;}
  if(!EditorApplication.isPlaying)return;
  Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
  if(lastFrame==Time.frameCount||EditorApplication.timeSinceStartup<next)return;
  lastFrame=Time.frameCount;
  try {
   if(phase==0) {
    if(SceneManager.GetActiveScene().name!="MainMenu")return;
    RallyGameSession.SelectLocalPlayers(test%2+1);RallyGameSession.SelectVehicleForPlayer(0,0);RallyGameSession.SelectVehicleForPlayer(1,0);
    RallyRaceLoading.Load(RallyGameSession.CircuitScenes[test/2]);phase=1;return;
   }
   if(phase==1) {
    if(RallyRaceLoading.IsPreparing||SceneManager.GetActiveScene().name!=RallyGameSession.CircuitScenes[test/2])return;
    maps=Object.FindObjectsByType<RallyMiniMap>().OrderBy(m=>m.Player.name).ToArray();
    if(maps.Length!=test%2+1||maps.Any(m=>m.Route==null||m.Route.MeshBuildCount==0))return;
    roster=RallyVehiclePresentationCache.ForScene(SceneManager.GetActiveScene());
    Check(roster.Length==(test%2==0?4:2),"correct roster "+SceneManager.GetActiveScene().name+" "+maps.Length+"P");
    Check(roster.Count(v=>v.car.GetComponentInParent<RallyBotController>()!=null)==(test%2==0?3:0),"three real bot rigs in 1P, no bots in split");
    foreach(var car in roster) {car.car.enabled=false;var rb=car.body;rb.linearVelocity=rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
    foreach(var bot in Object.FindObjectsByType<RallyBotController>())bot.enabled=false;
    foreach(var dynamics in Object.FindObjectsByType<RallyVehicleDynamics>())dynamics.enabled=false;
    Time.timeScale=0;
    var waypoints=GameObject.Find("AI_Waypoints").transform;
    foreach(var map in maps) {
     Check(map.MarkerCount==roster.Length,"one marker per participant");
     Check(map.Route.PointCount==waypoints.childCount,"route uses every authored waypoint");
     {
      var mesh=map.Route.canvasRenderer.GetMesh();
      Check(mesh.vertexCount==waypoints.childCount*2&&mesh.triangles.Length==waypoints.childCount*6,"route closes with last-to-first segment");
     }
     var hud=map.GetComponent<RallyRaceHud>();
     var timing=(TMP_Text)typeof(RallyRaceHud).GetField("timeText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud);
     Check(!Bounds(map.Panel).Overlaps(Bounds((RectTransform)timing.transform.parent)),"map does not cover timer");
     foreach(var warning in Object.FindObjectsByType<RallyBrakeWarningSystem>())
      foreach(var rectangle in warning.GetComponentsInChildren<RectTransform>(true))
       if(rectangle.name=="Brake Warning")Check(!Bounds(map.Panel).Overlaps(Bounds(rectangle)),"map does not cover corner notes");
     if(maps.Length==2) {
      var region=map.transform.Find("Player Viewport") as RectTransform;
      Check(region!=null&&Contains(Bounds(region),Bounds(map.Panel)),"map stays in its own split viewport");
     }
     foreach(var vehicle in roster) {
      var icon=map.MarkerFor(vehicle.body);Check(icon!=null,"marker belongs to correct Rigidbody");
      Color color=icon.Find("Dot").GetComponent<Image>().color;
      bool bot=vehicle.car.GetComponentInParent<RallyBotController>()!=null;
      Check(bot?color.r>color.g&&color.b>color.g:color.g>color.b||color.b>color.r,"human and bot colors distinct");
     }
    }
    builds=maps.Select(m=>m.Route.MeshBuildCount).ToArray();
    for(int i=0;i<roster.Length;i++){var point=waypoints.GetChild((i+1)*waypoints.childCount/(roster.Length+1));roster[i].body.position=point.position+Vector3.up*.7f;}
    Physics.SyncTransforms();phase=2;next=EditorApplication.timeSinceStartup+.3;return;
   }
   if(phase==2) {
    foreach(var map in maps)foreach(var vehicle in roster)
     Check(Vector2.Distance(map.MarkerFor(vehicle.body).anchoredPosition,map.ProjectPosition(vehicle.body.position))<.1f,"marker follows actual world X/Z");
    for(int i=0;i<maps.Length;i++)Check(maps[i].Route.MeshBuildCount==builds[i],"moving cars do not rebuild static route");
    ScreenCapture.CaptureScreenshot("Logs/Minimap_"+SceneManager.GetActiveScene().name+"_"+maps.Length+"P.png");
    phase=3;next=EditorApplication.timeSinceStartup+.5;return;
   }
   if(phase==3) {
    test++;if(test>=6){Finish(null);return;}
    SceneManager.LoadScene("MainMenu");phase=0;next=0;
   }
  }catch(Exception e){Finish(e);}
 }
 static Rect Bounds(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return new Rect(c[0],c[2]-c[0]);}
 static bool Contains(Rect outer,Rect inner)=>outer.xMin<=inner.xMin+.1f&&outer.yMin<=inner.yMin+.1f&&outer.xMax>=inner.xMax-.1f&&outer.yMax>=inner.yMax-.1f;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);File.AppendAllText(Report,"PASS: "+message+"\n");}
 static void Finish(Exception error){File.AppendAllText(Report,error==null?"COMPLETE: PASS\n":"FAIL: "+error+"\n");phase=99;EditorApplication.isPlaying=false;}
 static void Mode(PlayModeStateChange mode){if(mode==PlayModeStateChange.EnteredEditMode)exited=true;}
 static void Restore()
 {
  EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;
  for(int i=0;i<Keys.Length;i++){if(!had[i])PlayerPrefs.DeleteKey(Keys[i]);else if(i==3)PlayerPrefs.SetInt(Keys[i],players);else PlayerPrefs.SetString(Keys[i],values[i]);}
  PlayerPrefs.Save();RallyGameSession.RestoreSavedState();EditorSceneManager.playModeStartScene=start;EditorSceneManager.RestoreSceneManagerSetup(scenes);
  Application.runInBackground=background;EditorApplication.isPaused=paused;Time.timeScale=scale;File.AppendAllText(Report,"RESTORED\n");
 }
}
