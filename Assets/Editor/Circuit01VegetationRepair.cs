using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01VegetationRepair
{
    static Circuit01VegetationRepair() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Logs/vegetation-repair-request.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        var mode = File.ReadAllText(request).Trim();
        File.Move(request, request + ".consumed-" + DateTime.Now.Ticks);
        try { if(mode=="repair") Repair(); else Audit(); } catch(Exception e) { File.WriteAllText("Logs/vegetation-repair-error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Rally/Environment/Repair and Densify Vegetation")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Save the scene before repair.");
        const string scenePath="Assets/Scenes/Circuit_01.unity";
        var scene=EditorSceneManager.OpenScene(scenePath);
        var terrain=Object.FindObjectsByType<Terrain>().Single(); var data=terrain.terrainData;
        var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();
        var vertices=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
        string signature=ProtectedState(scene);
        string backup="Logs/SceneBackups/VegetationRepair_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup); File.Copy(scenePath,backup+"/Circuit_01.unity");
        File.Copy(AssetDatabase.GetAssetPath(data),backup+"/Terrain.asset");
        var tree=Circuit01VegetationExpansion.CreatePrefab("quiver_tree_01",3.5f,false);
        var shrub=Circuit01VegetationExpansion.CreatePrefab("searsia_lucida",1.2f,true);
        var prototypes=data.detailPrototypes;
        for(int i=0;i<prototypes.Length;i++)
        {
            var p=prototypes[i]; string name=p.prototype.name;
            if(name.Contains("searsia")) p.prototype=shrub;
            else if(name.Contains("wild_rooibos")) p.prototype=SingleDetail("wild_rooibos_bush",1.3f,p.prototype);
            else if(name.Contains("grass_medium")) p.prototype=SingleDetail("grass_medium_01",.7f,p.prototype);
            else throw new Exception("Unknown detail: "+name);
            p.alignToGround=1; p.positionJitter=.5f; p.useInstancing=true;
        }
        data.detailPrototypes=prototypes;
        var random=new System.Random(200926);
        var group=GameObject.Find("Additional CC0 desert trees");
        if(group==null)throw new Exception("Existing vegetation group not found.");
        var trees=group.GetComponentsInChildren<MeshRenderer>().Select(r=>r.transform).ToList();
        foreach(var t in trees) Plant(t,terrain,t.position,t.eulerAngles.y);
        int oldTrees=trees.Count;
        for(int attempt=0;attempt<30000 && trees.Count<80;attempt++)
        {
            var p=Candidate(random,vertices);
            if(!Safe(p,terrain,vertices,10)||trees.Any(t=>Vector3.Distance(t.position,p)<8))continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(tree,group.transform);
            go.name="Quiver desert tree "+trees.Count.ToString("00");
            go.transform.localScale=Vector3.one*(.8f+(float)random.NextDouble()*.5f);
            Plant(go.transform,terrain,p,(float)random.NextDouble()*360);trees.Add(go.transform);
        }
        int[] totals=new int[prototypes.Length]; int relocated=0;
        for(int layer=0;layer<prototypes.Length;layer++)
        {
            var cells=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);
            var mesh=prototypes[layer].prototype.GetComponent<MeshFilter>().sharedMesh;
            // Conservative radius includes full plant height at any slope plus cell jitter.
            float margin=Mathf.Max(6,mesh.bounds.size.magnitude*Mathf.Max(prototypes[layer].maxWidth,prototypes[layer].maxHeight)+data.size.x/data.detailWidth);
            int oldCount=0;
            for(int z=0;z<data.detailHeight;z++)for(int x=0;x<data.detailWidth;x++)
            {
                oldCount+=cells[z,x]; if(cells[z,x]==0)continue;
                if(!Safe(Cell(x,z,terrain),terrain,vertices,margin)){relocated+=cells[z,x];cells[z,x]=0;} else totals[layer]+=cells[z,x];
            }
            int target=prototypes[layer].prototype.name.Contains("searsia")?Mathf.Max(oldCount,850):prototypes[layer].prototype.name.Contains("rooibos")?Mathf.Max(oldCount,180):oldCount;
            for(int attempt=0;attempt<50000&&totals[layer]<target;attempt++)
            {
                var p=Candidate(random,vertices)-terrain.transform.position;
                int x=Mathf.FloorToInt(p.x/data.size.x*data.detailWidth),z=Mathf.FloorToInt(p.z/data.size.z*data.detailHeight);
                if(x<0||z<0||x>=data.detailWidth||z>=data.detailHeight||cells[z,x]>0)continue;
                if(!Safe(Cell(x,z,terrain),terrain,vertices,margin))continue;
                cells[z,x]=1;totals[layer]++;
            }
            if(totals[layer]!=target)throw new Exception("Could not reach safe planting target.");
            data.SetDetailLayer(0,0,layer,cells);
        }
        float min=999;
        foreach(var t in trees)
        {
            var b=t.GetComponent<Renderer>().bounds;
            float edge=Circuit01VegetationExpansion.Clearance(t.position,vertices)-new Vector2(b.extents.x,b.extents.z).magnitude;
            if(edge<2)throw new Exception("Tree bounds too close to road: "+t.name);
            min=Mathf.Min(min,edge);
            var n=t.position-terrain.transform.position;
            if(Vector3.Angle(t.up,data.GetInterpolatedNormal(n.x/data.size.x,n.z/data.size.z))>.1f)throw new Exception("Tree orientation validation failed.");
        }
        if(trees.Count<80||signature!=ProtectedState(scene))throw new Exception("Vegetation target or protected gameplay validation failed.");
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/vegetation-repair-validation.txt",$"Trees {oldTrees} -> {trees.Count}; details per layer: {string.Join(",",totals)}; relocated unsafe details: {relocated}; minimum tree bounds road clearance: {min:F2}m. All trees aligned to terrain normals; all details alignToGround=1 and validated with conservative plant+jitter radius. Gameplay/road unchanged.\n"+DateTime.Now.ToString("O"));
        Circuit01EnvironmentSetup.Preview();
    }
    static Vector3 Cell(int x,int z,Terrain t)=>t.transform.position+new Vector3((x+.5f)/t.terrainData.detailWidth*t.terrainData.size.x,0,(z+.5f)/t.terrainData.detailHeight*t.terrainData.size.z);
    static bool Safe(Vector3 p,Terrain t,Vector3[] road,float margin)
    {
        var n=p-t.transform.position;var d=t.terrainData;
        return n.x>=0&&n.z>=0&&n.x<d.size.x&&n.z<d.size.z&&d.GetSteepness(n.x/d.size.x,n.z/d.size.z)<28&&Circuit01VegetationExpansion.Clearance(p,road)>margin;
    }
    static Vector3 Candidate(System.Random random,Vector3[] v)
    {
        int i=random.Next(v.Length/4)*4;var a=(v[i]+v[i+3])*.5f;var b=(v[i+1]+v[i+2])*.5f;
        return Vector3.Lerp(a,b,(float)random.NextDouble())+Vector3.Cross(Vector3.up,(b-a).normalized)*(random.Next(2)==0?-1:1)*(Vector3.Distance(v[i],v[i+3])*.5f+12+(float)random.NextDouble()*40);
    }
    static void Plant(Transform t,Terrain terrain,Vector3 p,float yaw)
    {
        var n=p-terrain.transform.position;var d=terrain.terrainData;
        p.y=terrain.SampleHeight(p)+terrain.transform.position.y-.03f;
        t.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,d.GetInterpolatedNormal(n.x/d.size.x,n.z/d.size.z))*Quaternion.Euler(0,yaw,0));
    }
    static GameObject SingleDetail(string id,float height,GameObject old)
    {
        string path="Assets/Art/Environment/PolyHaven/"+id+"/"+id+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var selected=source.GetComponentsInChildren<MeshFilter>().OrderBy(f=>f.sharedMesh.vertexCount).First();
        var mesh=Object.Instantiate(selected.sharedMesh);
        var points=mesh.vertices.Select(p=>selected.transform.localToWorldMatrix.MultiplyPoint3x4(p)).ToArray();
        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
        mesh.vertices=points.Select(p=>(p-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*height/bounds.size.y).ToArray();
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        string basePath="Assets/Art/Environment/Prefabs/"+id+"_upright";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(basePath+".asset");
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,basePath+".asset");
        var go=new GameObject(id+"_upright");go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(old.GetComponentInChildren<Renderer>().sharedMaterial,mesh.subMeshCount).ToArray();
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,basePath+".prefab");Object.DestroyImmediate(go);return prefab;
    }
    static string ProtectedState(UnityEngine.SceneManagement.Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is Rigidbody||c is WheelCollider||c is RallyVehicleDynamics||c is JrsVehicleController||c is RallyCheckpointManager||c is RallyCheckpointTrigger||c is RallyPuddleSlowZone||c is ProBuilderMesh).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+EditorJsonUtility.ToJson(c.transform)));
    public static void Audit()
    {
        string report = "";
        foreach(var id in new[]{"quiver_tree_01", "searsia_lucida", "wild_rooibos_bush", "grass_medium_01"})
        {
            var paths = AssetDatabase.FindAssets("t:Model " + id).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx"));
            foreach(var path in paths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report += path + " root rot=" + root.transform.localEulerAngles + " scale=" + root.transform.localScale + "\n";
                foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    report += mf.name + " rot=" + mf.transform.eulerAngles + " size=" + mf.sharedMesh.bounds.size.ToString("F6") + " matrix=" + mf.transform.localToWorldMatrix + "\n";
            }
        }
        foreach(var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            foreach(var d in t.terrainData.detailPrototypes) report += "DETAIL " + d.prototype.name + " align=" + d.alignToGround + "\n";
        foreach(var r in GameObject.Find("Additional CC0 desert trees").GetComponentsInChildren<MeshRenderer>().Take(3))report += "TREE " + r.name + " rot="+r.transform.eulerAngles+" mesh="+r.GetComponent<MeshFilter>().sharedMesh.bounds+" world="+r.bounds+"\n";
        File.WriteAllText("Logs/vegetation-repair-audit.txt", report);
        // Push serialized mesh data after in-place updates before visual verification.
        foreach(var r in GameObject.Find("Additional CC0 desert trees").GetComponentsInChildren<MeshFilter>())
        {
            var m=r.sharedMesh; m.vertices=m.vertices; m.UploadMeshData(false);
        }
        Circuit01EnvironmentSetup.Preview();
    }
}
