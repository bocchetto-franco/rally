using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyLightArcadeSetup
{
    public static string Refresh()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();
        EditorApplication.QueuePlayerLoopUpdate();
        return "Compilation requested";
    }

    // Reload only a clean, single open scene. This removes transient
    // ExecuteAlways prefab changes left by the editor before this task's save.
    public static string ReloadSavedScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)
            throw new Exception("Editor must be idle");
        if(SceneManager.sceneCount!=1)throw new Exception("Expected one original scene; do not discard other work");
        Scene scene=SceneManager.GetActiveScene();
        if(scene.isDirty||string.IsNullOrEmpty(scene.path))throw new Exception("Open scene has unsaved work");
        string path=scene.path;
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        var dynamics=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RallyVehicleDynamics>(true))
            .Single(d=>d.name=="Porsche Rally Dynamics"&&d.VehicleController!=null&&d.VehicleController.name==RallyGameSession.VehicleName);
        var serialized=new SerializedObject(dynamics);
        foreach(var pair in Values)
            if(!Mathf.Approximately(serialized.FindProperty(pair.Key).floatValue,pair.Value))
                throw new Exception("Saved tuning mismatch: "+pair.Key);
        return "PASS restored "+path+"; stopped, saved player tune, dirty="+scene.isDirty;
    }

    static readonly Dictionary<string,float> Values=new Dictionary<string,float>
    {
        {"vehicleMass",1160f},{"vehicleAngularDamping",2.6f},{"steeringResponse",22f},
        {"lateralVelocityCorrection",5f},{"maximumLateralCorrectionAcceleration",12f},
        {"automaticBrakeStartSteering",.15f},{"automaticBrakeFreeSpeedKph",125f},
        {"automaticBrakeFullSteerSpeedKph",60f},{"automaticBrakeSpeedRangeKph",20f},
        {"automaticBrakeMaximumFraction",.55f},{"automaticBrakeEngageRate",4f},
        {"automaticBrakeReleaseRate",5f},{"automaticBrakeThrottleReduction",.95f}
    };

    public static string Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Editor must be idle");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        Scene active=SceneManager.GetActiveScene();
        string backup="Logs/SceneBackups/LightArcade_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        var report=new List<string>();
        try
        {
            for(int i=1;i<=3;i++)
            {
                string path=$"Assets/Scenes/Circuit_{i:00}.unity";
                Scene scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    File.Copy(path,backup+$"/Circuit_{i:00}.unity",true);
                    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
                    var dynamics=all.OfType<RallyVehicleDynamics>().Single(d=>d.name=="Porsche Rally Dynamics" && d.VehicleController!=null && d.VehicleController.name==RallyGameSession.VehicleName);
                    var body=dynamics.VehicleController.GetComponent<Rigidbody>();
                    var inputs=all.OfType<JrsInputController>().Where(c=>c.name=="Circuit Keyboard Input").ToArray();
                    var protectedBefore=all.Where(c=>c!=dynamics&&c!=body&&!inputs.Contains(c as JrsInputController)).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                    var serialized=new SerializedObject(dynamics);
                    foreach(var pair in Values)
                    {
                        var p=serialized.FindProperty(pair.Key);if(p==null)throw new Exception("Refresh scripts first: "+pair.Key);
                        p.floatValue=pair.Value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    body.mass=1160f;body.angularDamping=2.6f;
                    foreach(var input in inputs)input.steerSpeed=22f;
                    foreach(var kv in protectedBefore)if(kv.Key==null||EditorJsonUtility.ToJson(kv.Key)!=kv.Value)throw new Exception("Unrelated component changed: "+kv.Key);
                    EditorUtility.SetDirty(dynamics);EditorUtility.SetDirty(body);
                    foreach(var input in inputs)EditorUtility.SetDirty(input);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed: "+path);
                    report.Add($"PASS Circuit_{i:00}: player mass1160 damping2.6 steering22; progressive corner braking; {protectedBefore.Count} other components unchanged (bots, wheels, downforce, geometry, checkpoints).");
                }
                finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            report.Add("Backup: "+backup);report.Add("COMPLETE: PASS");
            return string.Join("\n",report);
        }
        catch(Exception e){report.Add("FAIL: "+e);throw;}
        finally {if(active.isLoaded)SceneManager.SetActiveScene(active);File.WriteAllText("Logs/light-arcade-setup.txt",string.Join("\n",report));}
    }
}
