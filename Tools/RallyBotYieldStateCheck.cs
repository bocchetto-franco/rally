using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RallyBotYieldStateCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Editor must be idle");
        var report = new List<string>(); float oldScale = Time.timeScale;
        Action<bool,string> check = (ok,text) => { if(!ok) throw new Exception(text); report.Add("PASS: "+text); };
        try
        {
            Time.timeScale = 1;
            foreach(string circuit in RallyGameSession.CircuitScenes)
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+circuit+".unity");
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var route = roots.Single(r=>r.name=="AI_Waypoints").transform;
                    var bots = roots.SelectMany(r=>r.GetComponentsInChildren<RallyBotController>(true)).ToArray();
                    var player = roots.SelectMany(r=>r.GetComponentsInChildren<JrsVehicleController>(true)).Single(c=>c.name==RallyGameSession.VehicleName && c.GetComponentInParent<RallyBotController>()==null).GetComponent<Rigidbody>();
                    check(bots.Length==3,circuit+" three authored bots");
                    foreach(var bot in bots)
                    {
                        var car = bot.GetComponentInChildren<JrsVehicleController>(); var body=car.GetComponent<Rigidbody>();
                        bot.Configure(car,body,route);
                        typeof(RallyBotController).GetMethod("Awake",Private).Invoke(bot,null);
                        typeof(RallyBotController).GetMethod("EnsureRoute",Private).Invoke(bot,null);
                        var serialized = new SerializedObject(bot);
                        float speed=serialized.FindProperty("maxSpeedKph").floatValue;
                        check(Mathf.Abs(bot.RaceMaxSpeedKph-speed*.7225f)<.001f,circuit+" "+bot.name+" uses original Easy tuning");
                        check(body.GetComponent<RallyBotCollisionYield>()!=null,"receiver bound on actual bot Rigidbody");
                        player.linearVelocity=Vector3.zero;
                        Time.timeScale=0; bot.ReportBlockedPlayer(player); check(!bot.IsYieldingToPlayer,"paused race cannot start contact yield"); Time.timeScale=1;
                        player.linearVelocity=Vector3.forward*20; bot.ReportBlockedPlayer(player); check(!bot.IsYieldingToPlayer,"moving player does not trigger blocked-player response");
                        player.linearVelocity=Vector3.zero; bot.ReportBlockedPlayer(player);
                        check(bot.IsYieldingToPlayer && bot.BrakeInput==1 && bot.VerticalInput==0 && bot.HorizontalInput==0,"blocked-player response brakes without reverse or steering input");
                        var end=typeof(RallyBotController).GetField("yieldUntil",Private); float until=(float)end.GetValue(bot);
                        check(Mathf.Abs(until-Time.time-.75f)<.001f,"serialized yield duration is 0.75 seconds");
                        bot.ReportBlockedPlayer(player); check((float)end.GetValue(bot)==until && bot.PlayerYieldCount==1,"continuous contact does not extend braking every physics tick");
                        end.SetValue(bot,Time.time-.01f);
                        typeof(RallyBotController).GetMethod("FixedUpdate",Private).Invoke(bot,null);
                        check(!bot.IsYieldingToPlayer && bot.BrakeInput==0 && bot.VerticalInput>0,"expired yield returns to forward waypoint control");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            report.Add("COMPLETE: PASS (state machine and serialized routes only; NOT real collision callbacks or a Play driving lap)");
        }
        catch(Exception e) { report.Add("FAIL: "+e); throw; }
        finally { Time.timeScale=oldScale; File.WriteAllLines("Logs/five-blocks-bot-state.txt",report); }
    }
}
