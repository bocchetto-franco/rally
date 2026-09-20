using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01VegetationExpansion
{
    const string Root = "Assets/Art/Environment";
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string Request = "Logs/vegetation-expansion-request.txt";
    static double nextPoll;
    static Circuit01VegetationExpansion() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextPoll = EditorApplication.timeSinceStartup + 3;
        if (!File.Exists(Request)) return;
        File.Move(Request, Request + ".consumed-" + DateTime.Now.Ticks);
        try { Build(); } catch(Exception e) { File.WriteAllText("Logs/vegetation-expansion-error.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Environment/Add Desert Trees and Dry Shrubs")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene first.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var terrain = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
        var road = GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();
        var vertices = road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
        string before = Signature(scene);
        Directory.CreateDirectory("Logs/SceneBackups");
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(ScenePath,"Logs/SceneBackups/Circuit_01_before_vegetation_"+stamp+".unity");
        File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData),"Logs/SceneBackups/Terrain_before_vegetation_"+stamp+".asset");
        var tree=CreatePrefab("quiver_tree_01",3.5f,false);
        var shrub=CreatePrefab("searsia_lucida",1.2f,true);
        string groupName="Additional CC0 desert trees";
        var group=GameObject.Find(groupName);
        if(group==null) { group=new GameObject(groupName); group.transform.SetParent(terrain.transform.parent); }
        else throw new InvalidOperationException("Expansion already exists; refusing to duplicate placements.");

        // Append a detail layer: all existing grass/shrub layers remain intact.
        var data=terrain.terrainData;
        int layer=data.detailPrototypes.Length;
        var prototypes=data.detailPrototypes.ToList();
        prototypes.Add(new DetailPrototype { prototype=shrub,usePrototypeMesh=true,useInstancing=true,
            renderMode=DetailRenderMode.VertexLit,minWidth=.8f,maxWidth=1.3f,minHeight=.85f,maxHeight=1.25f,
            noiseSeed=190926,noiseSpread=.3f,healthyColor=Color.white,dryColor=Color.white });
        data.detailPrototypes=prototypes.ToArray();
        var cells=new int[data.detailHeight,data.detailWidth];
        var random=new System.Random(190926);
        int trees=0,shrubs=0;
        float minClearance=float.MaxValue;
        var placed=new List<Vector3>();
        for(int attempt=0;attempt<20000 && (trees<48 || shrubs<500);attempt++)
        {
            bool isTree=trees<48 && attempt%8==0;
            if(!isTree && shrubs>=500)continue;
            int i=random.Next(vertices.Length/4)*4;
            var a=(vertices[i]+vertices[i+3])*.5f;
            var b=(vertices[i+1]+vertices[i+2])*.5f;
            var right=Vector3.Cross(Vector3.up,(b-a).normalized);
            float width=Vector3.Distance(vertices[i],vertices[i+3])*.5f;
            var p=Vector3.Lerp(a,b,(float)random.NextDouble())+right*(random.Next(2)==0?-1:1)*(width+13+(float)random.NextDouble()*40);
            var n=p-terrain.transform.position;
            if(n.x<0||n.z<0||n.x>=data.size.x||n.z>=data.size.z)continue;
            int x=Mathf.FloorToInt(n.x/data.size.x*data.detailWidth),z=Mathf.FloorToInt(n.z/data.size.z*data.detailHeight);
            if(!isTree) {p.x=terrain.transform.position.x+(x+.5f)/data.detailWidth*data.size.x;p.z=terrain.transform.position.z+(z+.5f)/data.detailHeight*data.size.z;}
            float clearance=Clearance(p,vertices);
            // 10m minimum clearance includes runoff, model radius and detail jitter.
            if(clearance<10 || data.GetSteepness(n.x/data.size.x,n.z/data.size.z)>28)continue;
            if(isTree && placed.Any(q=>Vector3.Distance(q,p)<9))continue;
            if(!isTree && cells[z,x]!=0)continue;
            minClearance=Mathf.Min(minClearance,clearance);
            if(isTree)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(tree,group.transform);
                go.name="Quiver desert tree "+trees.ToString("00");
                p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,(float)random.NextDouble()*360,0));
                go.transform.localScale=Vector3.one*(.8f+(float)random.NextDouble()*.5f);
                placed.Add(p);trees++;
            }
            else { cells[z,x]=1;shrubs++; }
        }
        if(trees!=48||shrubs!=500)throw new InvalidOperationException("Insufficient safe planting positions.");
        data.SetDetailLayer(0,0,layer,cells);
        if(before!=Signature(scene))throw new InvalidOperationException("Gameplay changed; refusing to save.");
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save scene.");
        File.WriteAllText("Logs/vegetation-expansion-validation.txt",$"Added {trees} Quiver trees and {shrubs} Searsia Lucida Terrain Details. Minimum center clearance from road edge: {minClearance:F2}m. Original Terrain Details preserved. Gameplay and road signature unchanged.\n"+DateTime.Now.ToString("O"));
        Circuit01EnvironmentSetup.Preview();
    }

    static float Clearance(Vector3 p,Vector3[] v)
    {
        float result=float.MaxValue;
        for(int i=0;i<v.Length;i+=4)
        {
            var a=(v[i]+v[i+3])*.5f; var b=(v[i+1]+v[i+2])*.5f;
            a.y=b.y=p.y;
            var d=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude));
            result=Mathf.Min(result,Vector3.Distance(p,a+d*t)-Vector3.Distance(v[i],v[i+3])*.5f);
        }
        return result;
    }

    static GameObject CreatePrefab(string id,float height,bool shrub)
    {
        string folder=Root+"/PolyHaven/"+id;
        string modelPath=folder+"/"+id+"_1k.fbx";
        AssetDatabase.ImportAsset(modelPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;importer.isReadable=true;importer.SaveAndReimport();
        foreach(string path in Directory.GetFiles(folder).Where(p=>p.EndsWith(".jpg")||p.EndsWith(".png")))
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            ti.maxTextureSize=1024;ti.textureCompression=TextureImporterCompression.Compressed;
            ti.textureType=path.Contains("nor_gl")?TextureImporterType.NormalMap:TextureImporterType.Default;
            ti.isReadable=shrub&&!path.Contains("nor_gl");ti.SaveAndReimport();
        }
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var meshes=source.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null).ToArray();
        File.AppendAllText("Logs/vegetation-mesh-audit.txt",id+"\n"+string.Join("\n",meshes.Select(f=>f.name+" vertices="+f.sharedMesh.vertexCount+" materials="+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name))))+"\n");
        // Use one supplied low-detail mesh, never render all FBX LODs together.
        var selected=meshes.OrderBy(f=>f.sharedMesh.vertexCount).First();
        var go=new GameObject(id+" optimized");
        var mf=go.AddComponent<MeshFilter>();
        var mesh=Object.Instantiate(selected.sharedMesh);
        var points=mesh.vertices.Select(p=>source.transform.worldToLocalMatrix.MultiplyPoint3x4(selected.transform.localToWorldMatrix.MultiplyPoint3x4(p))).ToArray();
        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
        float scale=height/Mathf.Max(.01f,bounds.size.y);
        mesh.vertices=points.Select(p=>(p-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale).ToArray();
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        string meshPath=Root+"/Prefabs/"+id+"_optimized.asset";AssetDatabase.CreateAsset(mesh,meshPath);mf.sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        var sourceMaterials=selected.GetComponent<Renderer>().sharedMaterials;
        renderer.sharedMaterials=sourceMaterials.Select(m=>MakeMaterial(id,shrub?"":m.name.ToLower().Contains("leaf")?"leaf_":"trunk_",shrub)).ToArray();
        renderer.shadowCastingMode=shrub?ShadowCastingMode.Off:ShadowCastingMode.On;
        if(!shrub){var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,new Renderer[]{renderer})});lod.RecalculateBounds();}
        string prefabPath=Root+"/Prefabs/"+id+"_optimized.prefab";
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);Object.DestroyImmediate(go);return prefab;
    }

    static Material MakeMaterial(string id,string part,bool alpha)
    {
        string folder=Root+"/PolyHaven/"+id+"/";
        var diffuse=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+id+"_"+part+"diff_1k.jpg");
        if(diffuse==null)throw new InvalidOperationException("Missing diffuse: "+id+part);
        if(alpha)
        {
            var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+id+"_alpha_1k.png");
            var pixels=diffuse.GetPixels();var packed=new Texture2D(diffuse.width,diffuse.height,TextureFormat.RGBA32,false);
            for(int y=0;y<diffuse.height;y++)for(int x=0;x<diffuse.width;x++)pixels[y*diffuse.width+x].a=mask.GetPixelBilinear(x/(float)diffuse.width,y/(float)diffuse.height).r;
            packed.SetPixels(pixels);packed.Apply();string p=Root+"/PackedTextures/"+id+"_albedo_alpha.png";File.WriteAllBytes(p,packed.EncodeToPNG());Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(p);var ti=(TextureImporter)AssetImporter.GetAtPath(p);ti.alphaIsTransparency=true;ti.maxTextureSize=1024;ti.textureCompression=TextureImporterCompression.Compressed;ti.SaveAndReimport();diffuse=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=id+"_"+part;mat.SetTexture("_BaseMap",diffuse);
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+id+"_"+part+"nor_gl_1k.jpg"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_Smoothness",.05f);mat.enableInstancing=true;
        if(alpha){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.4f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
        string path=Root+"/Materials/"+id+"_"+part+"URP.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null){Object.DestroyImmediate(mat);return old;}AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static string Signature(UnityEngine.SceneManagement.Scene scene) => string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is Rigidbody||c is WheelCollider||c is RallyVehicleDynamics||c is JrsVehicleController||c is RallyCheckpointManager||c is RallyCheckpointTrigger||c is RallyPuddleSlowZone||c is ProBuilderMesh).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)));
}
