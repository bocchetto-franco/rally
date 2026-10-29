using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated regression; temporary preview objects only, no source scene writes.
public static class RallyFinalRaceStateCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Before() => Run(false);
    public static void After() => Run(true);
    static void Run(bool fixedVersion)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        string report = "Logs/FinalAudit/race-state-" + (fixedVersion ? "after" : "before") + ".txt";
        Directory.CreateDirectory("Logs/FinalAudit");
        File.WriteAllText(report, "Isolated checkpoint recovery / finish ranking regression\n");
        try
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(road, scene); road.name = "Circuit03_Road";
            road.transform.position = new Vector3(0,-.5f,0); road.transform.localScale = new Vector3(40,1,40);
            var car = Create("Temporary player", scene);
            var body = car.AddComponent<Rigidbody>(); body.useGravity = false; body.position = new Vector3(9,-.8f,0);
            car.AddComponent<BoxCollider>(); car.AddComponent<JrsVehicleController>();
            var gateObject = Create("Temporary gate", scene); gateObject.transform.position = new Vector3(0,3.5f,0);
            var box = gateObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(20,8,6);
            var gate = gateObject.AddComponent<RallyCheckpointTrigger>();
            var manager = Create("Temporary timer",scene).AddComponent<RallyCheckpointManager>();
            manager.Configure(new[]{gate}); manager.BindVehicle(body);
            Physics.SyncTransforms();
            typeof(RallyCheckpointManager).GetMethod("StoreCheckpointPose",Private).Invoke(manager,new object[]{0});
            manager.ResetVehicleToLastCheckpoint();
            bool safe = body.position.y > 0;
            File.AppendAllText(report, "Reset from low shoulder: y="+body.position.y+" safe="+safe+"\n");
            if(fixedVersion && !safe) throw new Exception("Recovery placed root below driving surface");

            var other = Create("Temporary J2",scene).AddComponent<Rigidbody>(); other.isKinematic=true;
            var otherManager = Create("Temporary timer J2",scene).AddComponent<RallyCheckpointManager>();
            var ranking = Create("Temporary ranking",scene).AddComponent<RallyRacePositions>();
            var type = typeof(RallyRacePositions).GetNestedType("Racer",BindingFlags.NonPublic);
            var racers = Array.CreateInstance(type,2);
            for(int i=0;i<2;i++) {
                var racer=Activator.CreateInstance(type,true);
                type.GetField("body").SetValue(racer,i==0?body:other);
                type.GetField("checkpoints").SetValue(racer,i==0?manager:otherManager);
                type.GetField("order").SetValue(racer,i);
                type.GetField("progress").SetValue(racer,100f+i);
                racers.SetValue(racer,i);
            }
            typeof(RallyRacePositions).GetField("racers",Private).SetValue(ranking,racers);
            typeof(RallyCheckpointManager).GetField("finished",Private).SetValue(manager,true);
            typeof(RallyCheckpointManager).GetField("elapsedTime",Private).SetValue(manager,60f);
            int finishPosition=ranking.GetPosition(body);
            File.AppendAllText(report,"Finished J1 vs geometrically-ahead unfinished J2: J1="+finishPosition+"\n");
            if(fixedVersion && finishPosition!=1)throw new Exception("Unfinished racer overtook finished racer");
            typeof(RallyCheckpointManager).GetField("finished",Private).SetValue(otherManager,true);
            typeof(RallyCheckpointManager).GetField("elapsedTime",Private).SetValue(otherManager,62f);
            int timedPosition=ranking.GetPosition(body);
            File.AppendAllText(report,"Finished J1 60s vs J2 62s: J1="+timedPosition+"\n");
            if(fixedVersion && timedPosition!=1)throw new Exception("Finish time ordering incorrect");
            typeof(RallyCheckpointManager).GetField("elapsedTime",Private).SetValue(otherManager,59f);
            if(fixedVersion && ranking.GetPosition(body)!=2)throw new Exception("Faster finisher ordering incorrect");
            type.GetField("checkpoints").SetValue(racers.GetValue(1),null);
            if(fixedVersion && ranking.GetPosition(body)!=2)throw new Exception("Untimed bot progress ordering changed");
            File.AppendAllText(report,"Untimed bot progress ordering preserved\n");
            File.AppendAllText(report,fixedVersion?"COMPLETE: PASS\n":"BASELINE recorded; unsafe reset and finish ordering reproduced if safe=False/J1=2\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static GameObject Create(string name,Scene scene) {var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene);return go;}
}
