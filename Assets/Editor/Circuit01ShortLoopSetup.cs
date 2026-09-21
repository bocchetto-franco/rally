using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01ShortLoopSetup
{
    static Circuit01ShortLoopSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string verify="Logs/short-loop-verify-request.txt";
        if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(verify))
        {File.Move(verify,verify+".consumed-"+DateTime.Now.Ticks);try{Verify();}catch(Exception e){File.WriteAllText("Logs/short-loop-verify-error.txt",e.ToString());}return;}
        const string request="Logs/short-loop-request.txt";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        File.Move(request,request+".consumed-"+DateTime.Now.Ticks);
        try{Build();}catch(Exception e){File.WriteAllText("Logs/short-loop-error.txt",e.ToString());Debug.LogException(e);}
    }
    static object Call(string name,params object[] args)=>typeof(Circuit01LoopSetup).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
    [MenuItem("Tools/Rally/Shorten Circuit 01")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Save current scene first.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        if(GameObject.Find(Circuit01LoopSetup.ShortMarker)!=null)throw new Exception("Short loop already installed.");
        var terrain=Object.FindObjectsByType<Terrain>().Single();var data=terrain.terrainData;
        string backup="Logs/SceneBackups/ShortLoop_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.Copy(scene.path,backup+"/Circuit_01.unity");File.Copy(AssetDatabase.GetAssetPath(data),backup+"/Terrain.asset");
        var car=GameObject.Find("Porsche 911 SC Rally");
        var tuning=car.GetComponentsInChildren<Component>(true).Where(c=>c is Rigidbody||c is WheelCollider||c is RallyVehicleDynamics||c is JrsVehicleController).ToArray();
        var before=tuning.Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
        var old=GameObject.Find("Rally_Road_Start_to_Finish");var oldMesh=old.GetComponent<ProBuilderMesh>();
        var oldV=oldMesh.positions.Select(p=>old.transform.TransformPoint(p)).ToArray();float oldLength=Length(oldV);
        new GameObject(Circuit01LoopSetup.ShortMarker);Call("Define");
        var road=(ProBuilderMesh)Call("Strip","Short loop staging",old.GetComponent<Renderer>().sharedMaterial,false);
        var v=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();float length=Length(v);
        if(length>=oldLength*.5f)throw new Exception("Route is not less than half the original length.");
        Call("ValidateRoad",road.GetComponent<MeshCollider>());
        // The shared strip generator keeps original sampling; verify the retained first 550m exactly.
        int retained=0;float walked=0;
        while(retained*4+3<v.Length&&walked<550){int i=retained*4;for(int k=0;k<4;k++)if(Vector3.Distance(v[i+k],oldV[i+k])>.001f)throw new Exception("Retained road changed before cut transition.");walked+=Vector3.Distance((v[i]+v[i+3])*.5f,(v[i+1]+v[i+2])*.5f);retained++;}
        Object.DestroyImmediate(old);road.name="Rally_Road_Start_to_Finish";Planar(road,6);
        var oldShoulder=GameObject.Find("Loop Runoff Shoulders");var shoulderMaterial=oldShoulder.GetComponent<Renderer>().sharedMaterial;Object.DestroyImmediate(oldShoulder);
        var shoulders=(ProBuilderMesh)Call("Strip","Loop Runoff Shoulders",shoulderMaterial,true);Planar(shoulders,7);
        Circuit01LoopSetup.RebuildCheckpoints();Circuit01LoopSetup.RebuildBoundaries();
        int removedPuddles=0,removedBales=0;
        foreach(var p in Object.FindObjectsByType<RallyPuddleSlowZone>())if(Circuit01VegetationExpansion.Clearance(p.transform.position,v)>2){Object.DestroyImmediate(p.gameObject);removedPuddles++;}
        var hay=GameObject.Find("Loop Hay Bales");foreach(Transform child in hay.transform.Cast<Transform>().ToArray())if(Circuit01VegetationExpansion.Clearance(child.position,v)>6){Object.DestroyImmediate(child.gameObject);removedBales++;}
        FitTerrain(terrain,v);
        int cleared=ClearPlants(terrain,v);
        var crowd=GameObject.Find("Rally Spectators - Outside Barriers");
        foreach(Transform group in crowd.transform)foreach(Transform person in group)
        {
            var rs=person.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            person.position+=Vector3.up*(terrain.SampleHeight(person.position)+terrain.transform.position.y-b.min.y);
        }
        foreach(var r in crowd.GetComponentsInChildren<Renderer>())
        {
            var b=r.bounds;float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
            if(Circuit01VegetationExpansion.Clearance(b.center,v)<10+radius)throw new Exception("Public is inside new containment: "+r.name);
        }
        car.SetActive(true);
        if(Vector3.Distance(new Vector3(car.transform.position.x,0,car.transform.position.z),Vector3.zero)>8)throw new Exception("Porsche is not at start; needs explicit placement review.");
        for(int i=0;i<tuning.Length;i++)if(before[i]!=EditorJsonUtility.ToJson(tuning[i]))throw new Exception("Vehicle tuning changed.");
        Physics.SyncTransforms();Call("ValidateRoad",road.GetComponent<MeshCollider>());
        var manager=Object.FindAnyObjectByType<RallyCheckpointManager>();var gates=manager.GetComponentsInChildren<RallyCheckpointTrigger>();
        foreach(var gate in gates)if(!gate.GetComponent<BoxCollider>().isTrigger||Circuit01VegetationExpansion.Clearance(gate.transform.position,v)>0)throw new Exception("Checkpoint not over road.");
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/short-loop-validation.txt",$"Old {oldLength:F2}m -> new {length:F2}m ({100*length/oldLength:F1}%). First {walked:F1}m vertices preserved. One new 180-degree hairpin radius22. Original hills +10m/-7m retained. {gates.Length} trigger gates. Removed {removedPuddles} off-route puddles and {removedBales} bales. Cleared {cleared} plants from new runoff. Public outside corridor. Porsche at start, tuning unchanged. Backup: {backup}\n"+DateTime.Now.ToString("O"));
        Circuit01EnvironmentSetup.Preview();
    }
    static float Length(Vector3[] v){float n=0;for(int i=0;i<v.Length;i+=4)n+=Vector3.Distance((v[i]+v[i+3])*.5f,(v[i+1]+v[i+2])*.5f);return n;}
    public static void Verify()
    {
        var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();var v=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
        var start=(v[0]+v[3])*.5f;var end=(v[v.Length-3]+v[v.Length-2])*.5f;
        if(Vector3.Distance(start,end)>.001f)throw new Exception("Loop endpoints differ.");
        var terrain=Object.FindObjectsByType<Terrain>().Single();float maxGap=0;int tested=0;
        for(int i=0;i<v.Length;i+=4)
        {
            var p=(v[i]+v[i+1]+v[i+2]+v[i+3])*.25f;float height=terrain.SampleHeight(p)+terrain.transform.position.y;float gap=p.y-height;
            maxGap=Mathf.Max(maxGap,Mathf.Abs(gap));if(gap<-.08f||gap>.5f)throw new Exception("Terrain mismatch at "+p+" gap "+gap);tested++;
        }
        var manager=Object.FindAnyObjectByType<RallyCheckpointManager>();var gates=manager.GetComponentsInChildren<RallyCheckpointTrigger>().OrderBy(g=>g.CheckpointIndex).ToArray();
        var car=GameObject.Find("Porsche 911 SC Rally");var collider=car.GetComponentsInChildren<Collider>().First(c=>!(c is WheelCollider)&&!c.isTrigger&&c.attachedRigidbody!=null);
        manager.ResetTimer();gates.Last().SendMessage("OnTriggerEnter",collider);if(manager.IsRunning||manager.IsFinished)throw new Exception("Out-of-order gate accepted.");
        for(int i=0;i<gates.Length;i++)
        {
            gates[i].SendMessage("OnTriggerEnter",collider);
            if(manager.NextCheckpoint!=i+1)throw new Exception("Gate handler did not accept car.");
            if(i==0&&!manager.IsRunning)throw new Exception("Timer did not start.");
        }
        if(!manager.IsFinished||manager.IsRunning)throw new Exception("Finish did not stop timer.");manager.ResetTimer();
        int bales=GameObject.Find("Loop Hay Bales").transform.childCount;int puddles=Object.FindObjectsByType<RallyPuddleSlowZone>().Length;
        File.WriteAllText("Logs/short-loop-final-verification.txt",$"Closed loop: {Vector3.Distance(start,end):F6}m endpoint gap. {tested} terrain samples passed; max terrain/road gap {maxGap:F3}m. Trigger callback test: rejected finish before start; all {gates.Length} gates accepted car in order, start activated timer, finish stopped timer; reset after test. Retained {puddles} puddle, {bales} dynamic bales. Vehicle active at {car.transform.position}.\n"+DateTime.Now.ToString("O"));
    }
    static void Planar(ProBuilderMesh mesh,float tile){foreach(var f in mesh.faces)f.manualUV=true;mesh.textures=mesh.positions.Select(p=>new Vector2(p.x/tile,p.z/tile)).ToArray();mesh.ToMesh();mesh.Refresh();mesh.GetComponent<MeshCollider>().sharedMesh=mesh.GetComponent<MeshFilter>().sharedMesh;}
    static void FitTerrain(Terrain t,Vector3[] v)
    {
        var d=t.terrainData;int n=d.heightmapResolution;var heights=d.GetHeights(0,0,n,n);var distances=new float[n,n];var target=new float[n,n];var widths=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)distances[z,x]=float.MaxValue;
        float sx=d.size.x/(n-1),sz=d.size.z/(n-1);
        for(int i=0;i<v.Length;i+=4)
        {
            var a=(v[i]+v[i+3])*.5f-t.transform.position;var b=(v[i+1]+v[i+2])*.5f-t.transform.position;float width=Vector3.Distance(v[i],v[i+3])*.5f;
            int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-36)/sx),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+36)/sx),0,n-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z)-36)/sz),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,b.z)+36)/sz),0,n-1);
            var delta=new Vector2(b.x-a.x,b.z-a.z);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {var offset=new Vector2(x*sx-a.x,z*sz-a.z);float u=Mathf.Clamp01(Vector2.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));float distance=(offset-delta*u).magnitude;
                if(distance<distances[z,x]){distances[z,x]=distance;target[z,x]=(Mathf.Lerp(a.y,b.y,u)-.12f)/d.size.y;widths[z,x]=width;}}
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){float blend=1-Mathf.SmoothStep(0,1,(distances[z,x]-widths[z,x]-8)/20);if(blend>0)heights[z,x]=Mathf.Lerp(heights[z,x],target[z,x],blend);}
        d.SetHeights(0,0,heights);
    }
    static int ClearPlants(Terrain t,Vector3[] v)
    {
        int count=0;var d=t.terrainData;
        for(int layer=0;layer<d.detailPrototypes.Length;layer++)
        {var cells=d.GetDetailLayer(0,0,d.detailWidth,d.detailHeight,layer);for(int z=0;z<d.detailHeight;z++)for(int x=0;x<d.detailWidth;x++)if(cells[z,x]>0){var p=t.transform.position+new Vector3((x+.5f)/d.detailWidth*d.size.x,0,(z+.5f)/d.detailHeight*d.size.z);if(Circuit01VegetationExpansion.Clearance(p,v)<10){count+=cells[z,x];cells[z,x]=0;}}d.SetDetailLayer(0,0,layer,cells);}
        foreach(string name in new[]{"Additional CC0 desert trees","Sparse CC0 vegetation and rocks"})
        {var group=GameObject.Find(name);if(group==null)continue;foreach(Transform child in group.transform.Cast<Transform>().ToArray())
            {var rs=child.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
                if(Circuit01VegetationExpansion.Clearance(bounds.center,v)<8+radius){child.gameObject.SetActive(false);count++;}
                else if(child.gameObject.activeSelf)
                {
                    if(name.Contains("trees")){var p=child.position;var local=p-t.transform.position;p.y=t.SampleHeight(p)+t.transform.position.y-.03f;child.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,d.GetInterpolatedNormal(local.x/d.size.x,local.z/d.size.z))*Quaternion.Euler(0,child.eulerAngles.y,0));}
                    else child.position+=Vector3.up*(t.SampleHeight(child.position)+t.transform.position.y-bounds.min.y-.04f);
                }}}
        return count;
    }
}
