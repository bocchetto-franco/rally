using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Explicit repair of this circuit's private TerrainData. Never regenerates a route or shared asset.
public static class Circuit01DetailRepair
{
    const string Folder="Assets/Art/Environment/Circuit01Dressing";
    const string TerrainPath=Folder+"/Circuit01_DressedTerrain.asset";
    const string ScenePath="Assets/Scenes/Circuit_01.unity";
    const string Report="Logs/Circuit01Dressing/details.txt";
    static string Hash(Array values)
    {
        var bytes=new byte[Buffer.ByteLength(values)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
        using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));
    }
    static string SurfaceSignature(TerrainData d)=>Hash(d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution))+Hash(d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight))+Hash(d.GetHoles(0,0,d.holesResolution,d.holesResolution));
    static string SceneSignature(Scene s)=>string.Join("\n",s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)));
    static float Clearance(Vector3 p,Vector3[] route)
    {
        float result=float.PositiveInfinity;for(int i=0;i<route.Length;i++)
        {var a=route[i];var d=route[(i+1)%route.Length]-a;d.y=0;var delta=p-a;delta.y=0;result=Mathf.Min(result,(delta-d*Mathf.Clamp01(Vector3.Dot(delta,d)/Mathf.Max(.001f,d.sqrMagnitude))).magnitude);}return result;
    }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Unity is busy.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene must be preserved.");
        var setup=EditorSceneManager.GetSceneManagerSetup();var log=new StringBuilder("Circuit_01 Terrain Detail repair\n");TerrainData data=null,snapshot=null;bool committed=false;
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
            data=terrain.terrainData;if(AssetDatabase.GetAssetPath(data)!=TerrainPath)throw new Exception("Unexpected TerrainData.");
            snapshot=Object.Instantiate(data);string sceneBefore=SceneSignature(scene),surfaceBefore=SurfaceSignature(data);
            string backup="Logs/SceneBackups/Circuit01Details_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(TerrainPath,backup+"/Terrain.asset");
            log.AppendLine("Backup: "+backup+"; previous scatter="+data.detailScatterMode+"; detail density="+terrain.detailObjectDensity+"; draw distance="+terrain.detailObjectDistance);
            var prototypes=data.detailPrototypes;var maps=new List<int[,]>();var sums=new List<int>();
            for(int layer=0;layer<prototypes.Length;layer++)
            {
                var map=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);maps.Add(map);int sum=0,max=0;foreach(int value in map){sum+=value;max=Math.Max(max,value);}sums.Add(sum);
                bool valid=prototypes[layer].Validate(out string error);log.AppendLine("BEFORE "+layer+" "+prototypes[layer].prototype.name+": valid="+valid+" error="+error+" counts="+sum+" maxCell="+max);
                if(max>1||sum>4000)throw new Exception("Unexpected non-count detail map; manual inspection required.");
                prototypes[layer]=Prepare(prototypes[layer],layer);
            }
            // Preserve the existing count maps rather than generating a different distribution.
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);data.detailPrototypes=prototypes;
            for(int layer=0;layer<maps.Count;layer++)data.SetDetailLayer(0,0,layer,maps[layer]);
            terrain.Flush();int total=0;float minClearance=float.PositiveInfinity;
            var route=GameObject.Find("AI_Waypoints").transform.Cast<Transform>().Select(t=>t.position).ToArray();
            for(int layer=0;layer<prototypes.Length;layer++)
            {
                if(!prototypes[layer].Validate(out string error))throw new Exception("Invalid repaired prototype: "+error);
                var plantBounds=prototypes[layer].prototype.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                int generated=0,atConfiguredDensity=0;for(int z=0;z<data.detailPatchCount;z++)for(int x=0;x<data.detailPatchCount;x++)
                {
                    var instances=data.ComputeDetailInstanceTransforms(x,z,layer,1,out var bounds);generated+=instances.Length;
                    atConfiguredDensity+=data.ComputeDetailInstanceTransforms(x,z,layer,terrain.detailObjectDensity,out bounds).Length;
                    foreach(var instance in instances)
                    {
                        var p=terrain.transform.position+new Vector3(instance.posX,instance.posY,instance.posZ);
                        float radius=new Vector2(plantBounds.extents.x,plantBounds.extents.z).magnitude*instance.scaleXZ;
                        float clear=Clearance(p,route)-radius;minClearance=Mathf.Min(minClearance,clear);
                        if(clear<8)throw new Exception("Visible detail approaches road: layer="+layer+" position="+p+" clearance="+clear);
                    }
                }
                if(generated!=sums[layer])throw new Exception("Count mismatch: "+layer+" expected="+sums[layer]+" actual="+generated);
                if(Hash(maps[layer])!=Hash(data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer)))throw new Exception("Placement map changed.");
                total+=generated;log.AppendLine("AFTER "+layer+": valid=True; generated instances="+generated+"; at configured density="+atConfiguredDensity+"; placement map identical.");
            }
            if(surfaceBefore!=SurfaceSignature(data)||sceneBefore!=SceneSignature(scene))throw new Exception("Surface or scene components changed.");
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);committed=true;
            log.AppendLine("COMPLETE: PASS. "+total+" actual detail transforms; all prototypes valid, one material/submesh. Minimum plant bound distance to centerline="+minClearance.ToString("F2")+"m. Heights, paint, holes, placement maps and ALL scene components unchanged. No global settings or shared prototypes modified.");
        }
        catch(Exception e){log.AppendLine("FAILED: "+e);throw;}
        finally
        {
            if(!committed&&data!=null&&snapshot!=null){EditorUtility.CopySerialized(snapshot,data);EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);}
            if(snapshot!=null)Object.DestroyImmediate(snapshot);File.WriteAllText(Report,log.ToString());EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
    static DetailPrototype Prepare(DetailPrototype original,int index)
    {
        string meshPath=Folder+"/Meshes/TerrainDetail_"+index+".asset",prefabPath=Folder+"/Prefabs/TerrainDetail_"+index+".prefab";
        if(AssetDatabase.GetAssetPath(original.prototype)==prefabPath&&original.Validate(out _))
        {
            var ready=original.prototype.GetComponentInChildren<MeshFilter>();
            if(ready!=null&&ready.sharedMesh.subMeshCount==1&&ready.GetComponent<Renderer>().sharedMaterials.Length==1)return new DetailPrototype(original);
        }
        var clone=(GameObject)PrefabUtility.InstantiatePrefab(original.prototype);var filters=clone.GetComponentsInChildren<MeshFilter>();
        try
        {
            if(filters.Length!=1)throw new Exception("Unexpected multi-renderer prototype.");var filter=filters[0];var renderer=filter.GetComponent<MeshRenderer>();
            if(renderer.sharedMaterials.Any(m=>m!=renderer.sharedMaterial))throw new Exception("Different materials require a separate atlas.");
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);var mesh=Object.Instantiate(filter.sharedMesh);var triangles=mesh.triangles;mesh.subMeshCount=1;mesh.SetTriangles(triangles,0);mesh.name="Terrain detail "+index+" all leaves and stems";
            if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
            filter.sharedMesh=mesh;renderer.sharedMaterials=new[]{renderer.sharedMaterial};
            var prefab=PrefabUtility.SaveAsPrefabAsset(clone,prefabPath);AssetDatabase.SaveAssetIfDirty(mesh);
            var prototype=new DetailPrototype(original){prototype=prefab};return prototype;
        }
        finally{Object.DestroyImmediate(clone);}
    }
}
