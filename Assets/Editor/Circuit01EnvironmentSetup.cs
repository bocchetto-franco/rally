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

// Explicit editor operation only. Never rebuild the environment when loading a race.
[InitializeOnLoad]
public static class Circuit01EnvironmentSetup
{
    const string Root = "Assets/Art/Environment";
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string Request = "Logs/environment-request.txt";
    const string EnvironmentName = "Circuit 01 - Arid Mountains";
    static double nextPoll;
    static bool busy;
    struct Segment { public Vector3 a, b; public float halfWidth; }
    static readonly List<Segment> segments = new List<Segment>();
    static readonly Dictionary<Vector2Int,List<int>> grid = new Dictionary<Vector2Int,List<int>>();
    const float Cell = 60;

    static Circuit01EnvironmentSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextPoll = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Move(Request, Request + ".consumed-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        try { if (command == "build") Build(); else if (command == "retry") { EditorSceneManager.OpenScene(ScenePath); Build(); } else if (command == "preview") Preview(); }
        catch (Exception e) { File.WriteAllText("Logs/environment-error.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Environment/Build Arid Landscape")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || busy) return;
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before building the landscape.");
        busy = true;
        try
        {
            Directory.CreateDirectory("Logs/SceneBackups");
            File.Copy(ScenePath, "Logs/SceneBackups/Circuit_01_before_environment_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var road = GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();
            var shoulders = GameObject.Find("Loop Runoff Shoulders").GetComponent<ProBuilderMesh>();
            var puddles = Object.FindObjectsByType<RallyPuddleSlowZone>();
            if (puddles.Length != 4) throw new InvalidOperationException("Expected four existing puddles.");
            string before = GameplaySignature(scene);
            var roadPositions = road.positions.ToArray();
            var shoulderPositions = shoulders.positions.ToArray();
            var old = GameObject.Find(EnvironmentName);
            if (old != null) Object.DestroyImmediate(old);
            var environment = new GameObject(EnvironmentName);
            Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Root+"/Terrain");
            Directory.CreateDirectory(Root+"/Prefabs");
            Directory.CreateDirectory(Root+"/PackedTextures");
            AssetDatabase.Refresh();
            ReadRoute(road);
            Debug.Log("ENVIRONMENT: preparing PBR textures");
            var ground = GroundLayer("dry_ground_01", 7);
            var sand = GroundLayer("gravelly_sand", 6);
            var rock = GroundLayer("rock_boulder_dry", 13);
            var roadMat = Lit("Road - dry gravel", "gravelly_sand", false);
            var shoulderMat = Lit("Shoulder - dry earth", "dry_ground_01", false);
            PlanarMaterial(road,roadMat,6);
            PlanarMaterial(shoulders,shoulderMat,7);
            Debug.Log("ENVIRONMENT: fitting Terrain to road and shoulders");
            var terrain = BuildTerrain(environment.transform, road.GetComponent<Renderer>().bounds, new[]{ground,sand,rock});
            Debug.Log("ENVIRONMENT: placing downloaded plants and rocks");
            PlaceProps(environment.transform,terrain);
            ApplyWater(puddles);
            Lighting(environment.transform);
            Physics.SyncTransforms();
            string report = Validate(terrain, road, shoulders, puddles);
            if (before != GameplaySignature(scene) || !roadPositions.SequenceEqual(road.positions) || !shoulderPositions.SequenceEqual(shoulders.positions))
                throw new InvalidOperationException("Gameplay configuration or road geometry changed unexpectedly; scene has not been saved.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene could not be saved.");
            File.WriteAllText("Logs/environment-validation.txt", report+"\nGameplay signature unchanged. Road/shoulder vertices unchanged.\nSaved "+DateTime.Now.ToString("O"));
            Debug.Log("CIRCUIT_01_ENVIRONMENT_OK\n"+report);
            Preview();
        }
        finally { busy = false; EditorUtility.ClearProgressBar(); }
    }

    static string GameplaySignature(Scene scene)
    {
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true));
        return string.Join("\n",all.Where(c=>c is WheelCollider || c is Rigidbody || c is JrsVehicleController || c is RallyVehicleDynamics || c is RallyCheckpointManager || c is RallyCheckpointTrigger || c is RallyPuddleSlowZone || (c is BoxCollider && c.GetComponent<RallyPuddleSlowZone>() != null))
            .OrderBy(c=>c.GetEntityId().ToString()).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
    }
    static Texture2D Texture(string id,string suffix,bool normal=false,bool linear=false,bool readable=false)
    {
        string path=Directory.GetFiles(Root+"/PolyHaven/"+id,id+"_"+suffix+"_*").First(p=>!p.EndsWith(".meta"));
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal && !linear; importer.isReadable=readable;
        importer.maxTextureSize=id=="dry_ground_01"||id=="gravelly_sand"||id=="rock_boulder_dry"?2048:1024;
        importer.wrapMode=TextureWrapMode.Repeat; importer.anisoLevel=8; importer.mipmapEnabled=true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Texture2D Packed(string id,bool terrain,bool foliage)
    {
        string path=Root+"/PackedTextures/"+id+(foliage?"_albedo_alpha":terrain?"_terrain_mask":"_metal_smooth")+".png";
        if (!File.Exists(path))
        {
            var a=Texture(id,foliage?(id=="grass_medium_01"?"dry_diff":"diff"):"rough",false,!foliage,true);
            var b=Texture(id,foliage?"alpha":"ao",false,true,true);
            int size=foliage?1024:512;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,!foliage);
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                var ca=a.GetPixelBilinear(x/(float)size,y/(float)size);
                float cb=b.GetPixelBilinear(x/(float)size,y/(float)size).r;
                pixels[y*size+x]=foliage?new Color(ca.r,ca.g,ca.b,cb):new Color(0,terrain?cb:0,.5f,1-ca.r);
            }
            tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture=foliage;importer.alphaIsTransparency=foliage;importer.maxTextureSize=foliage?1024:512;importer.anisoLevel=8;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material MaterialAsset(string name,string shader)
    {
        string path=Root+"/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) {mat=new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat,path);}
        return mat;
    }
    static Material Lit(string name,string id,bool foliage)
    {
        var mat=MaterialAsset(name,"Universal Render Pipeline/Lit");
        mat.SetTexture("_BaseMap",foliage?Packed(id,false,true):Texture(id,"diff"));
        mat.SetTexture("_BumpMap",Texture(id,"nor_gl",true));mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_BumpScale",.7f);mat.SetColor("_BaseColor",Color.white);
        mat.SetTexture("_OcclusionMap",Texture(id,"ao",false,true));mat.EnableKeyword("_OCCLUSIONMAP");
        mat.SetTexture("_MetallicGlossMap",Packed(id,false,false));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",.65f);
        mat.enableInstancing=true;
        if(foliage) {mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.42f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
        EditorUtility.SetDirty(mat);return mat;
    }
    static TerrainLayer GroundLayer(string id,float size)
    {
        string path=Root+"/Terrain/"+id+".terrainlayer";
        var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if(layer==null) {layer=new TerrainLayer();AssetDatabase.CreateAsset(layer,path);}
        layer.diffuseTexture=Texture(id,"diff");layer.normalMapTexture=Texture(id,"nor_gl",true);
        layer.maskMapTexture=Packed(id,true,false);layer.tileSize=new Vector2(size,size);
        layer.normalScale=.7f;layer.metallic=0;layer.smoothness=.08f;
        EditorUtility.SetDirty(layer);return layer;
    }
    static void PlanarMaterial(ProBuilderMesh pb,Material mat,float tile)
    {
        pb.textures=pb.positions.Select(p=>{var w=pb.transform.TransformPoint(p);return new Vector2(w.x/tile,w.z/tile);}).ToArray();
        foreach(var face in pb.faces) face.manualUV=true;
        pb.ToMesh();pb.Refresh();pb.GetComponent<Renderer>().sharedMaterial=mat;
        EditorUtility.SetDirty(pb);
    }
    static Vector2Int Key(float x,float z) => new Vector2Int(Mathf.FloorToInt(x/Cell),Mathf.FloorToInt(z/Cell));
    static void ReadRoute(ProBuilderMesh road)
    {
        segments.Clear();grid.Clear();
        var v=road.positions;
        if(v.Count%4!=0) throw new InvalidOperationException("Expected the existing quad road strip.");
        for(int i=0;i<v.Count;i+=4)
        {
            Vector3 a=road.transform.TransformPoint((v[i]+v[i+3])*.5f),b=road.transform.TransformPoint((v[i+1]+v[i+2])*.5f);
            int index=segments.Count;segments.Add(new Segment{a=a,b=b,halfWidth=Vector3.Distance(v[i],v[i+3])*.5f});
            var lo=Key(Mathf.Min(a.x,b.x)-90,Mathf.Min(a.z,b.z)-90);var hi=Key(Mathf.Max(a.x,b.x)+90,Mathf.Max(a.z,b.z)+90);
            for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++) {var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(index);}
        }
    }
    static void Nearest(float x,float z,out float distance,out float height,out float halfWidth)
    {
        float best=float.MaxValue;height=0;halfWidth=6;
        grid.TryGetValue(Key(x,z),out var list);
        int count=list==null?segments.Count:list.Count;
        int bestIndex=0;
        int stride=list==null?12:1;
        for(int j=0;j<count;j+=stride)
        {
            var s=segments[list==null?j:list[j]];
            float dx=s.b.x-s.a.x,dz=s.b.z-s.a.z;
            float t=Mathf.Clamp01(((x-s.a.x)*dx+(z-s.a.z)*dz)/(dx*dx+dz*dz));
            float ex=x-s.a.x-dx*t,ez=z-s.a.z-dz*t,d=ex*ex+ez*ez;
            if(d<best) {best=d;height=Mathf.Lerp(s.a.y,s.b.y,t);halfWidth=s.halfWidth;bestIndex=j;}
        }
        if(list==null)
        for(int j=Mathf.Max(0,bestIndex-12);j<Mathf.Min(segments.Count,bestIndex+13);j++)
        {
            var s=segments[j];float dx=s.b.x-s.a.x,dz=s.b.z-s.a.z;
            float t=Mathf.Clamp01(((x-s.a.x)*dx+(z-s.a.z)*dz)/(dx*dx+dz*dz));
            float ex=x-s.a.x-dx*t,ez=z-s.a.z-dz*t,d=ex*ex+ez*ez;
            if(d<best){best=d;height=Mathf.Lerp(s.a.y,s.b.y,t);halfWidth=s.halfWidth;}
        }
        distance=Mathf.Sqrt(best);
    }
    static Terrain BuildTerrain(Transform parent,Bounds bounds,TerrainLayer[] layers)
    {
        const int resolution=1025;
        float width=Mathf.Ceil((bounds.size.x+700)/4)*4, length=Mathf.Ceil((bounds.size.z+700)/4)*4;
        var origin=new Vector3(bounds.center.x-width/2,-35,bounds.center.z-length/2);
        const float elevation=230;
        var heights=new float[resolution,resolution];
        for(int z=0;z<resolution;z++)
        {
            if(z%64==0)EditorUtility.DisplayProgressBar("Circuit_01","Fitting mountain terrain to existing road",z/(float)resolution);
            for(int x=0;x<resolution;x++)
            {
                float wx=origin.x+width*x/(resolution-1),wz=origin.z+length*z/(resolution-1);
                Nearest(wx,wz,out float distance,out float roadY,out float hw);
                float outside=Mathf.Max(0,distance-hw-7);
                float broad=Mathf.PerlinNoise(wx*.0033f+43,wz*.0033f+16);
                float ridge=1-Mathf.Abs(2*Mathf.PerlinNoise(wx*.009f+11,wz*.009f+72)-1);
                float detail=Mathf.PerlinNoise(wx*.035f+9,wz*.035f+19);
                float mountain=12+broad*95+ridge*22+detail*3;
                float blend=Mathf.SmoothStep(0,1,outside/155);
                float h=Mathf.Lerp(roadY-.065f,mountain,blend);
                heights[z,x]=(h-origin.y)/elevation;
            }
        }
        string path=Root+"/Terrain/Circuit01_AridTerrain.asset";
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,path);}
        data.heightmapResolution=resolution;data.size=new Vector3(width,elevation,length);data.SetHeights(0,0,heights);
        data.terrainLayers=layers;data.alphamapResolution=512;data.baseMapResolution=1024;
        var splat=new float[512,512,3];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            float u=x/511f,v=z/511f,wx=origin.x+u*width,wz=origin.z+v*length;
            Nearest(wx,wz,out float d,out _,out float hw);
            float near=Mathf.SmoothStep(0,1,(d-hw-9)/35);
            float rock=Mathf.Clamp01((data.GetSteepness(u,v)-13)/28)*near;
            float sand=(.2f+.55f*Mathf.PerlinNoise(wx*.013f+3,wz*.013f+7))*near*(1-rock);
            splat[z,x,0]=1-rock-sand;splat[z,x,1]=sand;splat[z,x,2]=rock;
        }
        data.SetAlphamaps(0,0,splat);
        var go=Terrain.CreateTerrainGameObject(data);go.name="Terrain - Desert mountain basin";go.transform.SetParent(parent);go.transform.position=origin;
        var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=MaterialAsset("Terrain - URP desert","Universal Render Pipeline/Terrain/Lit");
        terrain.drawInstanced=true;terrain.heightmapPixelError=3;terrain.basemapDistance=400;
        EditorUtility.SetDirty(data);return terrain;
    }
    static GameObject PropPrefab(string id,bool foliage)
    {
        string path=Root+"/PolyHaven/"+id+"/"+id+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name=id;
        var mat=Lit(id, id,foliage);
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();
        var bounds=BoundsOf(instance);
        // Standardize real-world scale without changing the downloaded model asset.
        float target=foliage?(id=="grass_medium_01"?.7f:1.3f):4.0f;
        float extent=foliage?bounds.size.y:Mathf.Max(bounds.size.x,bounds.size.z);
        instance.transform.localScale*=target/Mathf.Max(.001f,extent);
        var lod=instance.GetComponent<LODGroup>();
        if(lod==null)lod=instance.AddComponent<LODGroup>();
        lod.SetLODs(new[]{new LOD(foliage?.012f:.004f,instance.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        string prefabPath=Root+"/Prefabs/"+id+".prefab";
        var prefab=PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);Object.DestroyImmediate(instance);return prefab;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    static void PlaceProps(Transform parent,Terrain terrain)
    {
        var shrub=PropPrefab("wild_rooibos_bush",true);var grass=PropPrefab("grass_medium_01",true);var rock=PropPrefab("namaqualand_rocks_01",false);
        var props=new GameObject("Sparse CC0 vegetation and rocks");props.transform.SetParent(parent);
        var random=new System.Random(170926);
        int count=0;
        for(int i=0;i<segments.Count;i+=13)
        {
            var s=segments[i];var right=Vector3.Cross(Vector3.up,(s.b-s.a).normalized);
            foreach(int side in new[]{-1,1})
            {
                float offset=s.halfWidth+10+(float)random.NextDouble()*32;
                Vector3 p=s.a+right*offset*side;
                Nearest(p.x,p.z,out float d,out _,out float hw);
                if(d<hw+8)continue;
                var prefab=count%5==0?rock:count%3==0?shrub:grass;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.SetParent(props.transform);
                instance.name=prefab.name+"_"+count.ToString("000");
                instance.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                instance.transform.localScale*=.75f+(float)random.NextDouble()*.7f;
                p.y=terrain.SampleHeight(p)+terrain.transform.position.y;instance.transform.position=p;
                var b=BoundsOf(instance);instance.transform.position+=Vector3.up*(p.y-b.min.y-.04f);
                count++;
            }
        }
        Debug.Log("ENVIRONMENT: "+count+" downloaded prop instances placed outside driveable road/shoulders.");
    }
    static void ApplyWater(RallyPuddleSlowZone[] puddles)
    {
        string source=Root+"/UnityWaterSample/ProductionReady/Environment/Water/Water.mat";
        var template=AssetDatabase.LoadAssetAtPath<Material>(source);
        if(template==null || template.shader==null)throw new InvalidOperationException("Official Unity water sample not imported.");
        string path=Root+"/Materials/Puddles - Unity sample.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(template);AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_Color",new Color(.37f,.42f,.38f,0));mat.SetColor("_DepthColor",new Color(.11f,.15f,.12f,0));
        mat.SetFloat("_OpaqueDepth",.35f);mat.SetFloat("_RefractionStrength",.006f);
        mat.SetVector("_RippleSpeed",new Vector4(-.035f,.014f,-.02f,-.025f));
        mat.SetVector("_RippleScale",new Vector4(.45f,.35f,.17f,.4f));
        foreach(var zone in puddles)zone.GetComponent<Renderer>().sharedMaterial=mat;
        foreach(var camera in Object.FindObjectsByType<Camera>())
        {var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;}
        EditorUtility.SetDirty(mat);
    }
    static void Lighting(Transform parent)
    {
        var sky=MaterialAsset("Sky - warm procedural desert","Skybox/Procedural");
        sky.SetColor("_SkyTint",new Color(.58f,.57f,.51f));sky.SetColor("_GroundColor",new Color(.43f,.34f,.23f));
        sky.SetFloat("_AtmosphereThickness",1.15f);sky.SetFloat("_Exposure",1.1f);sky.SetFloat("_SunSize",.035f);
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.5f,.58f,.67f);RenderSettings.ambientEquatorColor=new Color(.52f,.45f,.34f);RenderSettings.ambientGroundColor=new Color(.23f,.20f,.17f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.66f,.63f,.55f);RenderSettings.fogDensity=.00065f;
        var sun=Object.FindObjectsByType<Light>().First(l=>l.type==LightType.Directional);
        sun.transform.rotation=Quaternion.Euler(38,-32,0);sun.color=new Color(1,.87f,.7f);sun.intensity=1.5f;sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
        foreach(var camera in Object.FindObjectsByType<Camera>())camera.clearFlags=CameraClearFlags.Skybox;
        var go=new GameObject("Desert sky reflection");go.transform.SetParent(parent);
        var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.resolution=128;probe.cullingMask=0;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.size=new Vector3(3000,500,3000);probe.center=new Vector3(300,100,500);
        EditorUtility.SetDirty(sky);
    }
    static string Validate(Terrain terrain,ProBuilderMesh road,ProBuilderMesh shoulders,RallyPuddleSlowZone[] puddles)
    {
        float maxGap=0;int checks=0;
        foreach(var s in segments)
        {
            var right=Vector3.Cross(Vector3.up,(s.b-s.a).normalized);
            foreach(float offset in new[]{-s.halfWidth-7,-s.halfWidth,0,s.halfWidth,s.halfWidth+7})
            {
                var p=(s.a+s.b)*.5f+right*offset;
                float y=terrain.SampleHeight(p)+terrain.transform.position.y;
                float gap=p.y-y;maxGap=Mathf.Max(maxGap,gap);
                if(gap<-.035f || gap>.6f)throw new InvalidOperationException("Terrain/road seam outside tolerance: "+p+" gap "+gap);
                checks++;
            }
        }
        foreach(var p in puddles)if(!p.GetComponent<BoxCollider>().isTrigger)throw new InvalidOperationException("Puddle trigger disabled.");
        var car=GameObject.Find("Porsche 911 SC Rally");
        if(car==null || !car.activeInHierarchy || car.transform.position.z>10)throw new InvalidOperationException("Porsche must remain active at the start.");
        if(ShaderUtil.ShaderHasError(puddles[0].GetComponent<Renderer>().sharedMaterial.shader))throw new InvalidOperationException("Water shader has compile errors.");
        return "Terrain samples checked: "+checks+"; maximum gap below road/shoulder: "+maxGap.ToString("F3")+"m; 4 puddles preserved; Porsche active at start; official water shader imported.\nTerrain size: "+terrain.terrainData.size;
    }
    [MenuItem("Tools/Rally/Environment/Capture Previews")]
    public static void Preview()
    {
        if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory("Logs/EnvironmentPreviews");
        var go=new GameObject("Temporary environment preview camera");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=2500;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
        try
        {
            RenderPreview(camera,new Vector3(-16,10,-20),new Vector3(5,0,48),"start");
            var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<Renderer>().bounds;
            RenderPreview(camera,road.center+new Vector3(-600,800,-650),road.center,"overview");
            var puddle=Object.FindObjectsByType<RallyPuddleSlowZone>().OrderBy(p=>p.name).First();
            RenderPreview(camera,puddle.transform.position+new Vector3(-7,4,-10),puddle.transform.position,"puddle");
        }
        finally {Object.DestroyImmediate(go);}
    }
    static void RenderPreview(Camera camera,Vector3 eye,Vector3 target,string name)
    {
        camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=58;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
        File.WriteAllBytes("Logs/EnvironmentPreviews/"+name+".png",tex.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}
