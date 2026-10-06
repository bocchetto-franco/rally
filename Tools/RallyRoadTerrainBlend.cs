using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run via Unity Pipeline run_script. Dressing is saved; no runtime script is needed.
public static class RallyRoadTerrainBlend
{
    const string Folder="Assets/Art/RoadTerrainBlend";
    const string Root="Road to terrain - soft edges";
    const string Report="Logs/road-terrain-blend.txt";
    static readonly float[] Distances={-1.8f,-.9f,0,1.5f,4.6f,6.1f,7};
    static readonly Dictionary<string,float> HeightCache=new Dictionary<string,float>();

    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play before dressing roads.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene; refusing to overwrite.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs");AssetDatabase.Refresh();
        string backup="Logs/SceneBackups/RoadBlend_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.WriteAllText(Report,"Road/shoulder/Terrain blending\nBackup: "+backup+"\n");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for(int track=1;track<=3;track++)
            {
                string name="Circuit_0"+track,path="Assets/Scenes/"+name+".unity";
                File.Copy(path,backup+"/"+name+".unity");
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);Physics.SyncTransforms();
                string before=Protected(scene);
                var road=GameObject.Find(track==1?"Rally_Road_Start_to_Finish":"Circuit0"+track+"_Road").GetComponent<ProBuilderMesh>();
                var shoulder=GameObject.Find(track==1?"Loop Runoff Shoulders":"Circuit0"+track+"_Runoff").GetComponent<ProBuilderMesh>();
                var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
                var points=GameObject.Find("AI_Waypoints").transform;
                int sample=Mathf.Clamp(points.childCount/7,2,points.childCount-3);
                Vector3 focus=points.GetChild(sample).position,forward=(points.GetChild(sample+2).position-points.GetChild(sample-2).position).normalized;
                if(!File.Exists("Logs/"+name+"_RoadBlend_Before.png"))Capture(focus,forward,"Logs/"+name+"_RoadBlend_Before.png");
                var prior=GameObject.Find(Root);if(prior!=null)Object.DestroyImmediate(prior);
                var root=new GameObject(Root);root.transform.SetParent(shoulder.transform.parent,false);
                var originalMaterial=road.GetComponent<Renderer>().sharedMaterial;
                string materialPath=AssetDatabase.GetAssetPath(originalMaterial);
                if(!File.Exists(backup+"/"+Path.GetFileName(materialPath)))File.Copy(materialPath,backup+"/"+Path.GetFileName(materialPath));
                originalMaterial.SetColor("_BaseColor",track==3?new Color(.78f,.82f,.76f,1):new Color(.98f,.94f,.84f,1));
                EditorUtility.SetDirty(originalMaterial);
                BuildStrips(name,road,shoulder,terrain,root.transform);
                shoulder.GetComponent<Renderer>().enabled=false;
                if(Protected(scene)!=before)throw new Exception(name+": protected physics, geometry or gameplay changed. Not saved.");
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var material=renderer.sharedMaterial;
                    if(material.shader==null||!material.shader.isSupported||material.renderQueue>=2950||material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"))
                        throw new Exception(name+": unexpected blend shader or render order after material validation.");
                }
                Capture(focus,forward,"Logs/"+name+"_RoadBlend_After.png");
                File.AppendAllText(Report,name+": PASS. Original vertices/colliders/terrain/gameplay preserved; original road texture and normal retained.\n");
            }
            File.AppendAllText(Report,"COMPLETE: PASS\n");
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }

    static void BuildStrips(string track,ProBuilderMesh road,ProBuilderMesh shoulder,Terrain terrain,Transform parent)
    {
        HeightCache.Clear();
        // Bridge grading can differ by millimetres at a shared road/shoulder vertex.
        // Match identical XZ locations with a bounded vertical tolerance, never a guessed nearest edge.
        var roadVertices=road.positions.Select(p=>road.transform.TransformPoint(p)).GroupBy(KeyXZ).ToDictionary(g=>g.Key,g=>g.ToArray());
        Func<Vector3,bool> onRoad=p=>roadVertices.TryGetValue(KeyXZ(p),out var matches)&&matches.Any(q=>Mathf.Abs(q.y-p.y)<.15f);
        var data=terrain.terrainData;var layers=data.terrainLayers;
        var weights=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
        var source=shoulder.positions.Select(p=>shoulder.transform.TransformPoint(p)).ToArray();
        var strips=new List<Vector3[]>();
        foreach(var face in shoulder.faces)
        {
            var ids=face.distinctIndexes.OrderBy(i=>i).ToArray();
            if(ids.Length!=4)throw new Exception("Expected original shoulder quad; source remains intact.");
            Vector3 a=source[ids[0]],b=source[ids[1]],c=source[ids[2]],d=source[ids[3]];
            bool ab=onRoad(a)&&onRoad(b);
            bool cd=onRoad(c)&&onRoad(d);
            if(!ab&&!cd)throw new Exception("Cannot resolve actual road edge at "+a+"; refusing guessed placement.");
            strips.Add(ab?new[]{a,b,d,c}:new[]{d,c,a,b});
        }
        int total=0;
        for(int layer=0;layer<layers.Length;layer++)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var tri=new List<int>();
            foreach(var strip in strips)
            {
                var grid=new Vector3[Distances.Length*2];var alpha=new float[grid.Length];
                for(int side=0;side<2;side++)for(int j=0;j<Distances.Length;j++)
                {
                    float distance=Distances[j];Vector3 inner=strip[side],outer=strip[side+2];
                    float width=Vector3.ProjectOnPlane(outer-inner,Vector3.up).magnitude;
                    Vector3 p=Vector3.LerpUnclamped(inner,outer,distance/width);
                    if(distance<0)
                    {
                        string key=Key(p);
                        if(!HeightCache.TryGetValue(key,out float y))
                        {
                            if(!road.GetComponent<MeshCollider>().Raycast(new Ray(p+Vector3.up*3,Vector3.down),out RaycastHit hit,6))throw new Exception("Edge overlay missing road underneath.");
                            y=hit.point.y;HeightCache.Add(key,y);
                        }
                        p.y=y;
                    }
                    float jitter=(Mathf.PerlinNoise(p.x*.32f,p.z*.32f)-.5f)*.6f;
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1.8f,.25f,distance+jitter));
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.8f,7,distance));
                    blend*=fade;
                    float nx=Mathf.Clamp01((p.x-terrain.transform.position.x)/data.size.x),nz=Mathf.Clamp01((p.z-terrain.transform.position.z)/data.size.z);
                    int x=Mathf.Clamp(Mathf.RoundToInt(nx*(data.alphamapWidth-1)),0,data.alphamapWidth-1),z=Mathf.Clamp(Mathf.RoundToInt(nz*(data.alphamapHeight-1)),0,data.alphamapHeight-1);
                    float higher=0;for(int k=layer+1;k<layers.Length;k++)higher+=weights[z,x,k];
                    // Layer alpha accounts for later layers so their composite equals Terrain splat weights.
                    float opacity=blend*weights[z,x,layer]/Mathf.Max(.00001f,1-blend*higher);
                    p.y+=.009f+layer*.001f;
                    int index=side*Distances.Length+j;grid[index]=p;alpha[index]=opacity;
                }
                for(int j=0;j<Distances.Length-1;j++)
                {
                    int[] q={j,Distances.Length+j,Distances.Length+j+1,j+1};
                    if(q.All(i=>alpha[i]<.002f))continue;
                    int start=v.Count;
                    foreach(int index in q)
                    {
                        Vector3 p=grid[index];v.Add(parent.InverseTransformPoint(p));uv.Add(new Vector2(p.x,p.z));colors.Add(new Color(1,1,1,alpha[index]));
                    }
                    // Both shoulder directions must have upward-facing winding.
                    bool up=Vector3.Cross(grid[q[1]]-grid[q[0]],grid[q[2]]-grid[q[0]]).y>=0;
                    tri.AddRange(up?new[]{start,start+1,start+2,start,start+2,start+3}:new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
            if(v.Count==0)continue;
            var mesh=new Mesh{name=track+" Terrain transition "+layer,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=Folder+"/"+track+"_Layer"+layer+".asset";
            var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(stored==null){stored=mesh;AssetDatabase.CreateAsset(stored,path);}else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}
            var go=new GameObject("Terrain blend - "+layers[layer].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=stored;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=MaterialFor(track,terrain,layers[layer],layer);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
            GameObjectUtility.SetStaticEditorFlags(go,0);total+=tri.Count/3;
        }
        File.AppendAllText(Report,track+": "+strips.Count+" real edge sections, "+total+" dressing triangles, "+parent.childCount+" renderers; zero additional colliders.\n");
    }

    static Material MaterialFor(string track,Terrain terrain,TerrainLayer layer,int index)
    {
        string path=Folder+"/"+track+"_Layer"+index+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")){name=track+" - "+layer.name+" blend"};AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",layer.diffuseTexture);m.SetTexture("_BumpMap",layer.normalMapTexture);
        m.SetTextureScale("_BaseMap",new Vector2(1/layer.tileSize.x,1/layer.tileSize.y));
        m.SetTextureOffset("_BaseMap",new Vector2((layer.tileOffset.x-terrain.transform.position.x)/layer.tileSize.x,(layer.tileOffset.y-terrain.transform.position.z)/layer.tileSize.y));
        m.SetColor("_BaseColor",new Color(layer.diffuseRemapMax.x,layer.diffuseRemapMax.y,layer.diffuseRemapMax.z,1));
        m.SetFloat("_Smoothness",layer.smoothness);m.SetFloat("_Metallic",layer.metallic);m.SetFloat("_BumpScale",layer.normalScale);
        if(layer.normalMapTexture!=null)m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",2);
        m.SetFloat("_BlendModePreserveSpecular",0);m.SetFloat("_QueueOffset",-200+index);
        m.SetFloat("_SrcBlendAlpha",1);m.SetFloat("_DstBlendAlpha",10);
        m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=2800+index;
        EditorUtility.SetDirty(m);return m;
    }
    static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
    static string KeyXZ(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.z*1000);
    static string Protected(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
        .Where(c=>c is Collider||c is Rigidbody||c is ProBuilderMesh||c is Terrain||c is RallyPuddleSlowZone||c is JrsVehicleController||c is RallyVehicleDynamics||c is RallyCheckpointManager||c is RallyCheckpointTrigger)
        .Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
    static void Capture(Vector3 focus,Vector3 forward,string path)
    {
        var go=new GameObject("Temporary road blend preview",typeof(Camera));var cam=go.GetComponent<Camera>();
        Vector3 offset=Vector3.Cross(Vector3.up,forward).normalized*11+Vector3.up*14-forward*9;
        cam.transform.SetPositionAndRotation(focus+offset,Quaternion.LookRotation(-offset));cam.fieldOfView=58;cam.farClipPlane=300;
        var data=cam.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
