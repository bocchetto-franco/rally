using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01WaterBasins
{
    const string Request = "Logs/water-basins-request.txt";
    const string Root = "Additional Water Basins v1";
    sealed class Basin { public Vector3 p, right, forward; public float distance; }
    static readonly List<Basin> basins = new List<Basin>();
    static Circuit01WaterBasins() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Move(Request, Request + ".consumed-" + DateTime.Now.Ticks);
        try { Build(); } catch (Exception e) { File.WriteAllText("Logs/water-basins-error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Rally/Add Water Basins")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new Exception("Exit Play and save the current scene before building basins.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        if (GameObject.Find(Root) != null) throw new Exception("Basins already installed; refusing cumulative deformation.");
        var original = Object.FindObjectsByType<RallyPuddleSlowZone>().Single();
        string originalState = EditorJsonUtility.ToJson(original) + EditorJsonUtility.ToJson(original.transform) + EditorJsonUtility.ToJson(original.GetComponent<BoxCollider>());
        var road = GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();
        var shoulder = GameObject.Find("Loop Runoff Shoulders").GetComponent<ProBuilderMesh>();
        var terrain = Object.FindObjectsByType<Terrain>().Single();
        string backup = "Logs/SceneBackups/WaterBasins_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/Circuit_01.unity");
        File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData), backup + "/Terrain.asset");
        var vertices = road.positions.Select(road.transform.TransformPoint).ToArray();
        basins.Clear();
        foreach (float target in new[] { 300f, 850f, 1200f })
        {
            Basin best = null; float score = float.MaxValue, walked = 0;
            for (int i = 0; i < vertices.Length; i += 4)
            {
                Vector3 a = (vertices[i] + vertices[i + 3]) / 2, b = (vertices[i + 1] + vertices[i + 2]) / 2;
                float length = Vector3.Distance(a, b), d = walked + length / 2; walked += length;
                if (Mathf.Abs(d - target) > 85 || Mathf.Abs(a.y - b.y) > .002f) continue;
                Vector3 p = (a + b) / 2;
                // Require a flat plateau around the complete depression, so water stays level.
                if (vertices.Where(v => new Vector2(v.x-p.x,v.z-p.z).magnitude < 20).Any(v => Mathf.Abs(v.y-p.y) > .02f)) continue;
                if (Mathf.Abs(d-target) >= score) continue;
                score = Mathf.Abs(d-target);
                best = new Basin { p=p, forward=(b-a).normalized, right=(vertices[i+3]-vertices[i]).normalized, distance=d };
            }
            if (best == null) throw new Exception("No flat location near " + target);
            basins.Add(best);
        }
        Deform(road); Deform(shoulder);
        var data = terrain.terrainData; int n = data.heightmapResolution; var heights = data.GetHeights(0,0,n,n);
        for (int z=0;z<n;z++) for(int x=0;x<n;x++)
        {
            Vector3 p = terrain.transform.position + new Vector3(x*data.size.x/(n-1),0,z*data.size.z/(n-1));
            heights[z,x] = Mathf.Max(0, heights[z,x] - Drop(p)/data.size.y);
        }
        data.SetHeights(0,0,heights);
        var root = new GameObject(Root);
        foreach(var basin in basins)
        {
            var v = new List<Vector3> { Vector3.zero }; var faces = new List<Face>();
            const int sides=32;
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*5));}
            for(int i=0;i<sides;i++) faces.Add(new Face(new[]{0,1+(i+1)%sides,1+i}));
            var water=ProBuilderMesh.Create(v,faces); water.name=$"Water_Basin_{basins.IndexOf(basin)+1:00}_{basin.distance:F0}m_6x10m_Depth030";
            water.transform.SetParent(root.transform);water.transform.SetPositionAndRotation(basin.p-Vector3.up*.10f,Quaternion.LookRotation(basin.forward));
            water.GetComponent<Renderer>().sharedMaterial=original.GetComponent<Renderer>().sharedMaterial;
            var solid=water.GetComponent<MeshCollider>();if(solid!=null)Object.DestroyImmediate(solid);
            var box=water.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(6,1.3f,10);box.center=new Vector3(0,.35f,0);
            var zone=water.gameObject.AddComponent<RallyPuddleSlowZone>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original),zone);
        }
        Physics.SyncTransforms();
        var roadCollider=road.GetComponent<MeshCollider>();
        foreach(var basin in basins)
        {
            if(!roadCollider.Raycast(new Ray(basin.p+Vector3.up*5,Vector3.down),out var hit,10) || basin.p.y-hit.point.y<.25f)
                throw new Exception("Depression collider missing at " + basin.distance);
            if(terrain.SampleHeight(basin.p)+terrain.transform.position.y>hit.point.y+.015f)
                throw new Exception("Terrain protrudes through basin at " + basin.distance);
        }
        if(originalState!=EditorJsonUtility.ToJson(original)+EditorJsonUtility.ToJson(original.transform)+EditorJsonUtility.ToJson(original.GetComponent<BoxCollider>()))throw new Exception("Original puddle changed.");
        var car=GameObject.Find("Porsche 911 SC Rally");if(!car.activeSelf || new Vector2(car.transform.position.x,car.transform.position.z).magnitude>8)throw new Exception("Porsche must remain active at start.");
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/water-basins-result.txt","PASS: three basins saved, road collider depression >=25cm, terrain below road, existing puddle unchanged, Porsche at start. Backup: "+backup+"\n"+string.Join("\n",basins.Select(b=>$"Distance {b.distance:F1}m; position {b.p}; depression .30m; water 6x10m")));
    }
    static float Drop(Vector3 p)
    {
        float drop=0;
        // Match the 6x10m shoreline to the -10cm contour of the 30cm bowl.
        float shoreline = Mathf.Sqrt(1f-Mathf.Sqrt(1f/3f));
        foreach(var b in basins){Vector3 delta=p-b.p;float z=Vector3.Dot(delta,b.forward)/(5f/shoreline),x=Vector3.Dot(delta,b.right)/(3f/shoreline);float radius=x*x+z*z;if(radius<1)drop=Mathf.Max(drop,.30f*Mathf.Pow(1-radius,2));}
        return drop;
    }
    static void Deform(ProBuilderMesh mesh)
    {
        var old=mesh.positions.ToArray();var uv=mesh.textures.ToArray();var positions=new List<Vector3>();var textures=new List<Vector2>();var faces=new List<Face>();
        foreach(var face in mesh.faces)
        {
            var ids=face.distinctIndexes.ToArray();if(ids.Length!=4)throw new Exception("Expected quad strip.");
            // Quad strips store sequential corners a,b,c,d.
            Array.Sort(ids);Vector3 a=old[ids[0]],b=old[ids[1]],c=old[ids[2]],d=old[ids[3]];
            Vector3 center=mesh.transform.TransformPoint((a+b+c+d)/4);
            bool nearby=basins.Any(q=>Vector3.ProjectOnPlane(center-q.p,Vector3.up).magnitude<30);
            int along=nearby?Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b))):1;
            int across=nearby?Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,d))):1;
            for(int j=0;j<along;j++)for(int k=0;k<across;k++)
            {
                int start=positions.Count;
                foreach(var t in new[]{new Vector2(j/(float)along,k/(float)across),new Vector2((j+1f)/along,k/(float)across),new Vector2((j+1f)/along,(k+1f)/across),new Vector2(j/(float)along,(k+1f)/across)})
                {
                    Vector3 p=Vector3.Lerp(Vector3.Lerp(a,b,t.x),Vector3.Lerp(d,c,t.x),t.y);Vector3 world=mesh.transform.TransformPoint(p);world.y-=Drop(world);positions.Add(mesh.transform.InverseTransformPoint(world));
                    textures.Add(Vector2.Lerp(Vector2.Lerp(uv[ids[0]],uv[ids[1]],t.x),Vector2.Lerp(uv[ids[3]],uv[ids[2]],t.x),t.y));
                }
                faces.Add(new Face(new[]{start,start+1,start+2,start,start+2,start+3}){manualUV=true,submeshIndex=face.submeshIndex,smoothingGroup=face.smoothingGroup});
            }
        }
        mesh.RebuildWithPositionsAndFaces(positions,faces);mesh.textures=textures;mesh.ToMesh();mesh.Refresh();var collider=mesh.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh.GetComponent<MeshFilter>().sharedMesh;EditorUtility.SetDirty(mesh);
    }
}
