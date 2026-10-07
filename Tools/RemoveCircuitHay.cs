using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class RemoveCircuitHay
{
    static bool IsHay(Transform t) => t.name.StartsWith("HayBale_", StringComparison.OrdinalIgnoreCase) || t.name.IndexOf("Hay Bales", StringComparison.OrdinalIgnoreCase) >= 0;
    static Transform[] All(Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
    static int Remove(Transform[] all)
    {
        var targets = all.Where(IsHay).ToArray();
        var doomed = new HashSet<UnityEngine.Object>();
        foreach (var t in targets) foreach (var child in t.GetComponentsInChildren<Transform>(true))
        {
            doomed.Add(child.gameObject);
            foreach (var c in child.GetComponents<Component>()) if(c != null) doomed.Add(c);
        }
        foreach(var t in all) foreach(var c in t.GetComponents<Component>())
        {
            if(c == null || doomed.Contains(c) || c is Transform) continue;
            using(var so = new SerializedObject(c))
            {
                var p = so.GetIterator();
                while(p.Next(true)) if(p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && doomed.Contains(p.objectReferenceValue))
                    throw new Exception("External reference to hay: " + c.name + "." + p.propertyPath);
            }
        }
        int count=targets.Count(t=>t.name.StartsWith("HayBale_", StringComparison.OrdinalIgnoreCase));
        foreach(var t in targets) if(t != null && !AncestorsHay(t.parent)) UnityEngine.Object.DestroyImmediate(t.gameObject);
        return count;
    }
    static bool AncestorsHay(Transform t) { while(t != null) { if(IsHay(t)) return true; t=t.parent; } return false; }
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Editor must be idle.");
        var active=SceneManager.GetActiveScene();
        string folder="Logs/SceneBackups/RemoveHay_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(folder);
        var report=new List<string>();
        try
        {
            for(int i=1;i<=3;i++)
            {
                string path=$"Assets/Scenes/Circuit_{i:00}.unity";
                var original=SceneManager.GetSceneByPath(path);
                bool dirty=original.isLoaded && original.isDirty;
                string work=$"Assets/__HayRemoval_{DateTime.Now:yyyyMMdd_HHmmss}_Circuit_{i:00}.unity";
                var scene=original;bool opened=!scene.isLoaded || dirty;
                if(dirty)
                {
                    if(!EditorSceneManager.SaveScene(original,folder+$"/Unsaved_Circuit_{i:00}.unity",true)) throw new Exception("Unsaved backup failed.");
                    File.Copy(path,work,true);
                    AssetDatabase.ImportAsset(work,ImportAssetOptions.ForceSynchronousImport);
                    scene=EditorSceneManager.OpenScene(work,OpenSceneMode.Additive);
                }
                else if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    if(!EditorSceneManager.SaveScene(scene,folder+$"/Circuit_{i:00}.unity",true)) throw new Exception("Backup failed.");
                    var all=All(scene);
                    var before=all.Where(t=>!IsHay(t) && !AncestorsHay(t.parent)).SelectMany(t=>t.GetComponents<Component>())
                        .Where(c=>c!=null && !(c is Transform)).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                    int removed=Remove(all);
                    if(All(scene).Any(IsHay)) throw new Exception("Hay remains.");
                    foreach(var kv in before) if(kv.Key==null || EditorJsonUtility.ToJson(kv.Key)!=kv.Value) throw new Exception("Unrelated component changed: "+kv.Key);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed.");
                    report.Add($"PASS Circuit_{i:00}: removed {removed} bales; zero external hay references; {before.Count} unrelated components unchanged.");
                }
                finally {if(opened) EditorSceneManager.CloseScene(scene,true);}
                if(dirty)
                {
                    File.Copy(work,path,true);
                    AssetDatabase.DeleteAsset(work);
                    int removedLive=Remove(All(original));
                    EditorSceneManager.MarkSceneDirty(original);
                    report.Add($"PASS unsaved Circuit_{i:00}: removed {removedLive} bales in memory; unrelated unsaved edits preserved, not saved.");
                }
            }
            foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources/MenuBackdrops"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var all=root.GetComponentsInChildren<Transform>(true);
                    if(!all.Any(IsHay)) continue;
                    File.Copy(path,folder+"/"+Path.GetFileName(path),true);
                    int n=Remove(all);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    report.Add("PASS menu backdrop "+path+": removed "+n+" visual bales.");
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            report.Add("Backup: "+folder);
            report.Add("COMPLETE: PASS");
            return string.Join("\n",report);
        }
        catch(Exception e) {report.Add("FAIL: "+e);throw;}
        finally
        {
            if(active.isLoaded) SceneManager.SetActiveScene(active);
            File.WriteAllText("Logs/remove-hay.txt",string.Join("\n",report));
        }
    }
}
