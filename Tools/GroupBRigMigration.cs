using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class GroupBRigMigration
{
    const string Backup="Logs/SceneBackups/GroupB_20261017";
    static readonly string[] Old={"Assets/auto/porsche-911-sc-rally", "Assets/Art/Vehicles/BmwE30", "Assets/Resources/Vehicles/BmwE30.prefab", "Assets/Resources/MenuVehicles/BmwE30.prefab", "Assets/Resources/MenuVehicles/Porsche911.prefab"};
    static bool OldPath(string path)=>Old.Any(p=>path==p||path.StartsWith(p+"/",StringComparison.Ordinal)) || path.StartsWith("Assets/Art/MenuVehicles/BmwE30_") || path.StartsWith("Assets/Art/MenuVehicles/Porsche911_");
    static bool Depends(string path)=>AssetDatabase.GetDependencies(path,true).Any(OldPath);
    public static void Plan()
    {
        var paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!OldPath(p)&&!AssetDatabase.IsValidFolder(p)&&Depends(p)).ToArray();
        Directory.CreateDirectory(Backup);
        File.WriteAllLines("Logs/GroupBCandidates/dependents.txt",paths);
        Debug.Log(string.Join("\n",paths));
    }
    public static void Migrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        File.WriteAllText("Logs/GroupBCandidates/rig-migration.txt","RUNNING\n");
        var paths=File.ReadAllLines("Logs/GroupBCandidates/dependents.txt");
        try
        {
            // Upgrade reusable rigs first so scene instances inherit the new model.
            foreach(string path in paths.Where(p=>p.EndsWith(".prefab")&&!p.Contains("MenuVehicles")))
            {
                BackUp(path);
                var root=PrefabUtility.LoadPrefabContents(path);
                try { if(Upgrade(root)>0)PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(string path in paths.Where(p=>p.EndsWith(".unity")))
            {
                BackUp(path);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                int count=0;foreach(var root in scene.GetRootGameObjects())count+=Upgrade(root);
                if(count>0)EditorSceneManager.SaveScene(scene);
            }
            File.AppendAllText("Logs/GroupBCandidates/rig-migration.txt","COMPLETE: PASS\n");
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static int Upgrade(GameObject root)
    {
        int count=0;
        foreach(var car in root.GetComponentsInChildren<JrsVehicleController>(true))
        {
            // Only rigs containing the retired Porsche geometry; original police untouched.
            bool usesOld=car.GetComponentsInChildren<MeshFilter>(true).Any(m=>OldPath(AssetDatabase.GetAssetPath(m.sharedMesh)));
            if(!usesOld)continue;
            // Detach model instance links before stripping geometry; preserve all
            // transforms and components referenced by controller/effects/cameras.
            foreach(var t in car.GetComponentsInChildren<Transform>(true).ToArray())
                if(t!=null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                   OldPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var rb=car.GetComponent<Rigidbody>();
            var dynamics=Object.FindObjectsByType<RallyVehicleDynamics>(FindObjectsInactive.Include).FirstOrDefault(d=>d.VehicleController==car);
            var colliders=car.GetComponentsInChildren<Collider>(true);
            string before=RallyVehiclePhysicsParityTest.Snapshot(car,dynamics,rb,colliders);
            var visual=car.GetComponentInParent<RallyBotController>(true)!=null?
                RallyPlayerVehicleSelection.ApplyBotVisual(car,RallyPlayerVehicleSelection.LanciaName):
                RallyPlayerVehicleSelection.ApplyVisual(car,RallyPlayerVehicleSelection.LanciaName);
            foreach(var m in car.GetComponentsInChildren<MeshFilter>(true))
                if(OldPath(AssetDatabase.GetAssetPath(m.sharedMesh)))
                {
                    m.sharedMesh=null;
                    var renderer=m.GetComponent<MeshRenderer>();
                    if(renderer!=null){renderer.enabled=false;renderer.sharedMaterials=Array.Empty<Material>();}
                }
            string after=RallyVehiclePhysicsParityTest.Snapshot(car,dynamics,rb,colliders);
            if(before!=after)throw new Exception("Tuning changed on "+car.name);
            count++;
            File.AppendAllText("Logs/GroupBCandidates/rig-migration.txt","PASS tuning and physics identities preserved: "+car.gameObject.scene.path+" / "+car.name+"; default "+visual.name+"\n");
        }
        return count;
    }
    static void BackUp(string path)
    {
        string dest=Backup+"/"+path;
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        if(!File.Exists(dest))File.Copy(path,dest);
        if(File.Exists(path+".meta")&&!File.Exists(dest+".meta"))File.Copy(path+".meta",dest+".meta");
    }
    public static void ArchiveRetired()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first");
        var paths=AssetDatabase.GetAllAssetPaths();
        var remaining=paths.Where(p=>p.StartsWith("Assets/")&&!OldPath(p)&&!AssetDatabase.IsValidFolder(p)&&Depends(p)).ToArray();
        if(remaining.Length>0)throw new Exception("Still referenced: "+string.Join("\n",remaining));
        string[] targets=Old.Concat(paths.Where(p=>p.StartsWith("Assets/Art/MenuVehicles/BmwE30_")||p.StartsWith("Assets/Art/MenuVehicles/Porsche911_"))).Distinct().ToArray();
        string project=Path.GetFullPath(".")+Path.DirectorySeparatorChar;
        foreach(string target in targets)
        {
            string full=Path.GetFullPath(target);
            if(!full.StartsWith(project+"Assets"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Target outside project Assets");
            if(!File.Exists(target)&&!Directory.Exists(target))continue;
            if(Directory.Exists(target))foreach(string file in Directory.GetFiles(target,"*",SearchOption.AllDirectories))BackUp(file.Replace('\\','/'));
            else BackUp(target);
            if(File.Exists(target+".meta"))BackUp(target+".meta");
            if(!AssetDatabase.MoveAssetToTrash(target))throw new Exception("Cannot archive "+target);
            File.AppendAllText("Logs/GroupBCandidates/retired-assets.txt",target+" -> "+Backup+" (also recycled)\n");
        }
        AssetDatabase.Refresh();
        File.AppendAllText("Logs/GroupBCandidates/retired-assets.txt","COMPLETE: PASS - no surviving asset dependencies to retired sources\n");
    }
}
