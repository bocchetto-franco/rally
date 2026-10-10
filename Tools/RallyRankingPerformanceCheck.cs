using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated preview scenes; tests rank math and the new update cadence, not vehicle driving.
public static class RallyRankingPerformanceCheck
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static FieldInfo Field(string name)=>typeof(RallyRacePositions).GetField(name,Private);
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Unity must be idle.");
        string report="Logs/ranking-performance-check.txt";File.WriteAllText(report,"Isolated ranking regression\n");
        foreach(string circuit in new[]{"Circuit_01","Circuit_02","Circuit_03"}) {
            Scene scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+circuit+".unity");
            try {
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
                var ranking=all.OfType<RallyRacePositions>().Single();
                typeof(RallyRacePositions).GetMethod("Awake",Private).Invoke(ranking,null);
                var route=scene.GetRootGameObjects().Single(g=>g.name=="AI_Waypoints").transform;
                var player=all.OfType<JrsVehicleController>().Single(c=>c.name=="Porsche 911 SC Rally").GetComponent<Rigidbody>();
                var bots=all.OfType<RallyBotController>().Take(3).Select(b=>b.GetComponentInChildren<Rigidbody>(true)).ToArray();
                var bodies=new[]{player}.Concat(bots).ToArray();
                if(bodies.Length!=4||bodies.Any(b=>b==null))throw new Exception("Expected player + 3 bot bodies.");
                var racerType=typeof(RallyRacePositions).GetNestedType("Racer",BindingFlags.NonPublic);
                var racers=Array.CreateInstance(racerType,4);
                for(int i=0;i<4;i++) {
                    var entry=Activator.CreateInstance(racerType,true);
                    racerType.GetField("body").SetValue(entry,bodies[i]);racerType.GetField("order").SetValue(entry,i);
                    racers.SetValue(entry,i);bodies[i].isKinematic=true;bodies[i].position=route.GetChild(5+i*5).position;
                }
                Field("racers").SetValue(ranking,racers);Field("player").SetValue(ranking,racers.GetValue(0));
                ranking.RefreshPositions();
                if(ranking.GetPosition(player)!=4)throw new Exception("Initial progress ordering failed.");
                player.position=route.GetChild(25).position;ranking.RefreshPositions();
                if(ranking.GetPosition(player)!=1)throw new Exception("Overtake/teleport ranking failed.");
                player.position=route.GetChild(2).position;
                typeof(RallyRacePositions).GetMethod("Update",Private).Invoke(ranking,null);
                if(ranking.GetPosition(player)!=1)throw new Exception("Ranking ignored the 0.1-second cadence.");
                Field("nextProgressUpdate").SetValue(ranking,Time.time-.001f);
                typeof(RallyRacePositions).GetMethod("Update",Private).Invoke(ranking,null);
                if(ranking.GetPosition(player)!=4)throw new Exception("Ranking did not refresh after its deadline.");
                if(ranking.RacerCount!=4)throw new Exception("Racer registration changed.");
                File.AppendAllText(report,circuit+": PASS ordered progress, overtaking, teleport/reset and 10Hz cadence\n");
            } finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
        File.AppendAllText(report,"COMPLETE: PASS\n");
    }
}
