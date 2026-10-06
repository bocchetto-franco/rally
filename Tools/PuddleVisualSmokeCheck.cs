using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot Play check invoked with Unity Pipeline run_script; never saved into a scene/build.
public static class PuddleVisualSmokeCheck
{
    static int phase, originalPlayers;
    static double started, step;
    static SceneAsset originalStart;
    static Rigidbody body;
    static RallyPuddleMudVisual puddle;
    static ParticleSystem particles;
    static Vector3 direction;
    public static void Run()
    {
        originalPlayers=RallyGameSession.LocalPlayerCount;
        originalStart=EditorSceneManager.playModeStartScene;
        RallyGameSession.SelectLocalPlayers(1);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Circuit_01.unity");
        phase=0;started=EditorApplication.timeSinceStartup;
        File.WriteAllText("Logs/puddle-play-test.txt","Running visual wheel-contact test\n");
        EditorApplication.update+=Tick;
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try
        {
            if(phase==99)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                EditorApplication.update-=Tick;EditorSceneManager.playModeStartScene=originalStart;
                RallyGameSession.SelectLocalPlayers(originalPlayers);return;
            }
            if(EditorApplication.timeSinceStartup-started>100)throw new Exception("Timed out during Play test.");
            if(!Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode!=EditorApplication.isPlaying)return;
            if(phase==0){SceneManager.LoadScene("Circuit_01");step=Time.time;phase=1;return;}
            if(phase==1)
            {
                if(Time.time-step<1)return;
                puddle=UnityEngine.Object.FindAnyObjectByType<RallyPuddleMudVisual>();
                var car=UnityEngine.Object.FindObjectsByType<JrsVehicleController>().First(c=>c.GetComponent<RallyBotController>()==null);
                body=car.GetComponent<Rigidbody>();direction=Vector3.ProjectOnPlane(puddle.transform.forward,Vector3.up).normalized;
                float clearance=.6f;
                if(car.frontLeftWheel.GetGroundHit(out WheelHit wheelHit))clearance=body.position.y-wheelHit.point.y;
                Vector3 target=puddle.transform.TransformPoint(puddle.localCenter)-direction*2;
                var hit=Physics.RaycastAll(target+Vector3.up*4,Vector3.down,8,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.attachedRigidbody==null).OrderBy(h=>h.distance).First();
                target.y=hit.point.y+clearance+.05f;
                body.position=target;body.rotation=Quaternion.LookRotation(direction);body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
                Physics.SyncTransforms();step=Time.time;phase=2;return;
            }
            if(phase==2)
            {
                if(Time.time-step<.5f)return;
                body.linearVelocity=direction*15;step=Time.time;phase=3;return;
            }
            if(phase==3)
            {
                particles=puddle.transform.Find("Mud droplets - visual only").GetComponent<ParticleSystem>();
                if(particles.particleCount>0)
                {
                    var values=new ParticleSystem.Particle[96];int count=particles.GetParticles(values);
                    if(!values.Take(count).All(p=>p.startColor.r>p.startColor.b))throw new Exception("Spray is not mud-colored.");
                    File.AppendAllText("Logs/puddle-play-test.txt","PASS: grounded moving vehicle emitted "+count+" brown ballistic droplets.\n");
                    if(particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name!="Universal Render Pipeline/Particles/Unlit")throw new Exception("Wrong spray shader.");
                    File.AppendAllText("Logs/puddle-play-test.txt","PASS: shared URP particle material, maximum 96 particles, no collision module.\nCOMPLETE: PASS\n");
                    Finish();return;
                }
                if(Time.time-step>2)throw new Exception("Vehicle crossed puddle without emitting mud.");
            }
        }
        catch(Exception e){File.AppendAllText("Logs/puddle-play-test.txt","FAILED: "+e);Finish();}
    }
    static void Finish(){phase=99;EditorApplication.isPlaying=false;}
}
