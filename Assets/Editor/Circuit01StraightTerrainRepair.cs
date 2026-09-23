using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Circuit01StraightTerrainRepair
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    static void Route(out Vector3 a, out Vector3 b)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(Circuit01LoopSetup).GetMethod("Define", flags).Invoke(null, null);
        var sections = (IEnumerable)typeof(Circuit01LoopSetup).GetField("sections", flags).GetValue(null);
        a = b = Vector3.zero; float longest = 0;
        foreach (var s in sections)
        {
            var t=s.GetType(); float length=(float)t.GetField("length").GetValue(s);
            if(Mathf.Abs((float)t.GetField("curvature").GetValue(s))>.00001f || length<longest)continue;
            longest=length; a=(Vector3)t.GetField("origin").GetValue(s);
            float yaw=(float)t.GetField("yaw").GetValue(s); b=a+new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw))*length;
        }
    }
    public static void Inspect()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Route(out var a,out var b); var terrain=Object.FindObjectsByType<Terrain>().Single();
        Vector3 right=Vector3.Cross(Vector3.up,(b-a).normalized);
        var report=new StringBuilder($"Straight {a} -> {b}; length {Vector3.Distance(a,b)}; terrain {terrain.transform.position}, {terrain.terrainData.size}\n");
        for(int i=0;i<=4;i++)
        {
            var p=Vector3.Lerp(a,b,i/4f); report.Append($"Point {p}: ");
            foreach(float x in new[]{-120f,-80,-50,-35,-20,0,20,35,50,80,120})
                report.Append($"{x}:{terrain.SampleHeight(p+right*x)+terrain.transform.position.y:F1} ");
            report.AppendLine();
        }
        File.WriteAllText("Logs/straight-terrain-inspect.txt",report.ToString());
    }

    [MenuItem("Tools/Rally/Soften Long Straight Terrain")]
    public static void Repair()
    {
        if(File.Exists("Logs/straight-terrain-repair.txt"))
            throw new InvalidOperationException("Repair already applied. Restore its terrain/scene backup before repeating it.");
        if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the scene and exit Play first.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        Route(out var a,out var b);
        var terrain=Object.FindObjectsByType<Terrain>().Single(); var data=terrain.terrainData;
        var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<MeshCollider>();
        var origin=terrain.transform.position; var size=data.size; int n=data.heightmapResolution;
        var old=data.GetHeights(0,0,n,n); var next=(float[,])old.Clone();
        var dir=(b-a).normalized; var right=Vector3.Cross(Vector3.up,dir); float length=Vector3.Distance(a,b);
        var protectedBounds=new List<Bounds>();
        foreach(string name in new[]{"Large Rally Crowds - Outside Barriers","Rally Spectators - Outside Barriers"})
        {
            var root=GameObject.Find(name); if(root==null)continue;
            foreach(Transform group in root.transform)
            {
                var renderers=group.GetComponentsInChildren<Renderer>(); if(renderers.Length==0)continue;
                var bound=renderers[0].bounds; foreach(var r in renderers)bound.Encapsulate(r.bounds);
                bound.Expand(4); protectedBounds.Add(bound);
            }
        }
        // Preserve other road sections and their immediate runoff, including the old puddle.
        var roadMesh=road.GetComponent<UnityEngine.ProBuilder.ProBuilderMesh>();
        var rv=roadMesh.positions.Select(road.transform.TransformPoint).ToArray();
        var centers=new List<Vector3>();
        for(int i=0;i<rv.Length;i+=4)
        {
            var c=(rv[i]+rv[i+1]+rv[i+2]+rv[i+3])*.25f;
            float along=Vector3.Dot(c-a,dir);
            if(Mathf.Abs(Vector3.Dot(c-a,right))>8 || along < -2 || along > length+2) centers.Add(c);
        }
        string backup="Logs/SceneBackups/StraightTerrain_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup); File.Copy(ScenePath,backup+"/Circuit_01.unity");
        File.Copy(AssetDatabase.GetAssetPath(data),backup+"/Terrain.asset");
        int changed=0; float maximumLowering=0;
        var mask=new bool[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            Vector3 p=origin+new Vector3(x*size.x/(n-1),0,z*size.z/(n-1));
            float along=Vector3.Dot(p-a,dir), side=Mathf.Abs(Vector3.Dot(p-a,right));
            if(along < -60 || along > length+60 || side <=14 || side>=155)continue;
            var center=a+dir*Mathf.Clamp(along,0,length);
            if(!road.Raycast(new Ray(center+Vector3.up*180,Vector3.down),out var hit,300))continue;
            float fade=Mathf.SmoothStep(0,1,(along+60)/60)*Mathf.SmoothStep(0,1,(length+60-along)/60);
            // Fade away from any nearby preserved section, not just its centerline.
            float nearest=centers.Min(c=>new Vector2(c.x-p.x,c.z-p.z).magnitude);
            fade*=Mathf.SmoothStep(0,1,(nearest-14)/12);
            foreach(var bounds in protectedBounds)
            {
                float dx=Mathf.Max(bounds.min.x-p.x,0,p.x-bounds.max.x);
                float dz=Mathf.Max(bounds.min.z-p.z,0,p.z-bounds.max.z);
                fade*=Mathf.SmoothStep(0,1,Mathf.Sqrt(dx*dx+dz*dz)/12);
            }
            float u=Mathf.Clamp01((side-14)/141);
            float blend=u*u*u*(u*(u*6-15)+10); // Zero slope at both limits.
            float ground=old[z,x]*size.y+origin.y;
            float target=Mathf.Lerp(hit.point.y-.12f,ground,blend);
            float lowered=Mathf.Lerp(ground,Mathf.Min(ground,target),fade);
            if(ground-lowered<.001f)continue;
            next[z,x]=(lowered-origin.y)/size.y;mask[z,x]=true;changed++;
            maximumLowering=Mathf.Max(maximumLowering,ground-lowered);
        }
        if(changed==0)throw new Exception("No terrain samples selected.");
        var props=GameObject.Find("Sparse CC0 vegetation and rocks");
        var anchors=new Dictionary<Transform,float>();
        if(props!=null)foreach(Transform prop in props.transform)anchors[prop]=terrain.SampleHeight(prop.position);
        var trees=data.treeInstances;
        var treeHeights=trees.Select(t=>terrain.SampleHeight(origin+Vector3.Scale(t.position,size))).ToArray();
        data.SetHeights(0,0,next);
        int movedProps=0,movedTrees=0;
        foreach(var entry in anchors)
        {
            float delta=terrain.SampleHeight(entry.Key.position)-entry.Value;
            if(Mathf.Abs(delta)<.01f)continue;
            entry.Key.position+=Vector3.up*delta;movedProps++;
        }
        for(int i=0;i<trees.Length;i++)
        {
            float delta=terrain.SampleHeight(origin+Vector3.Scale(trees[i].position,size))-treeHeights[i];
            if(Mathf.Abs(delta)<.01f)continue;
            var p=trees[i].position;p.y+=delta/size.y;trees[i].position=p;movedTrees++;
        }
        data.SetTreeInstances(trees,false);
        var saved=data.GetHeights(0,0,n,n); float outsideChange=0;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(!mask[z,x])outsideChange=Mathf.Max(outsideChange,Mathf.Abs(saved[z,x]-old[z,x])*size.y);
        if(outsideChange>.008f)throw new Exception("Terrain changed outside the repair mask.");
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        if(movedProps>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        File.WriteAllText("Logs/straight-terrain-repair.txt",$"PASS: straight {a} -> {b}, {length:F1}m. Widened transition from 20m to 141m outside the 14m road corridor, tapered over 60m at ends. Changed {changed} samples; max lowering {maximumLowering:F2}m; outside-mask change {outsideChange:F4}m. Regrounded {movedProps} props and {movedTrees} trees. Spectator platforms and all road geometry preserved. Backup {backup}.");
    }
}
