using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit editor operation: creates an independent scene using the existing asset library.
[InitializeOnLoad]
public static class Circuit03Builder
{
    public const string ScenePath = "Assets/Scenes/Circuit_03.unity";
    const string SourceScene = "Assets/Scenes/Circuit_01.unity";
    const string Env = "Assets/Art/Environment/";
    const string Forest = "Assets/Art/Forest/";
    const string Output = Forest + "Circuit03";
    const string People = Env + "QuaterniusPeople/";
    sealed class Section { public Vector3 origin; public float yaw, curvature, length, start; }
    struct Sample { public Vector3 p, right; public float d, width; }
    sealed class Basin { public Sample sample; public int id; }
    sealed class Crowd { public Vector3 center, target, outward; public float height; }
    [Serializable] sealed class Layout
    {
        public string scene = "Circuit_03", notes = "Geometry/environment only. No checkpoints, timer, water triggers or dynamic hay logic.";
        public float lengthMeters;
        public Vector3[] centerline, puddleCenters, crowdCenters;
        public float[] distances, roadWidths;
    }
    static readonly List<Section> sections = new List<Section>();
    static readonly List<Sample> route = new List<Sample>();
    static readonly List<Basin> basins = new List<Basin>();
    static readonly List<Crowd> crowds = new List<Crowd>();
    static readonly List<BoxCollider> walls = new List<BoxCollider>();
    static float length, crowdClearance;
    static Terrain terrain;
    static ProBuilderMesh road;
    static System.Random random;

    static Circuit03Builder()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Circuit03.FreeDrivePending",false))return;
            string previous=SessionState.GetString("Circuit03.PreviousPlayStartScene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool("Circuit03.FreeDrivePending",false);
        };
    }

    [MenuItem("Tools/Rally/Play Circuit 03 - Free Drive")]
    public static void PlayFreeDrive()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetString("Circuit03.PreviousPlayStartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("Circuit03.FreeDrivePending",true);
        EditorSceneManager.playModeStartScene=Asset<SceneAsset>(ScenePath);
        EditorApplication.isPlaying=true;
    }

    static string Hash(string path) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))); }
    static T Asset<T>(string path) where T:Object
    { var a=AssetDatabase.LoadAssetAtPath<T>(path); if(a==null)throw new Exception("Missing existing asset: "+path); return a; }
    static GameObject Root(string name, Transform parent=null)
    { var go=new GameObject(name); if(parent!=null)go.transform.SetParent(parent,false); return go; }

    [MenuItem("Tools/Rally/Create Circuit 03")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new Exception("Save the active scene and leave Play before creating Circuit_03.");
        if(File.Exists(ScenePath) || Directory.Exists(Output))throw new Exception("Circuit_03 already exists; refusing to overwrite it.");
        string sourceHash=Hash(SourceScene), terrainPath=Env+"Terrain/Circuit01_AridTerrain.asset", terrainHash=Hash(terrainPath);
        var scene=EditorSceneManager.OpenScene(SourceScene);
        var sourceTerrain=Object.FindObjectsByType<Terrain>().Single();
        var detailPrototypes=new DetailPrototype[0];
        var car=GameObject.Find("Porsche 911 SC Rally");
        var tuning=car.GetComponentsInChildren<Component>(true).Where(c=>c is Rigidbody || c is WheelCollider || c is JrsVehicleController).ToArray();
        var signature=tuning.Select(EditorJsonUtility.ToJson).ToArray();
        var dynamics=Object.FindAnyObjectByType<RallyVehicleDynamics>(); string dynamicsBefore=EditorJsonUtility.ToJson(dynamics);
        if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new Exception("Cannot create the new scene.");
        var keep=new HashSet<string>{"Porsche 911 SC Rally","Circuit Keyboard Input","Main Camera","Directional Light","Porsche Rally Dynamics"};
        foreach(var root in scene.GetRootGameObjects())if(!keep.Contains(root.name))Object.DestroyImmediate(root);
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        Circuit03ForestAssets.Build();
        DefineRoute(); random=new System.Random(240926);
        var environment=Root("Circuit 03 - Forest Mountain Rally");
        road=RoadStrip("Circuit03_Road",false,environment.transform);
        RoadStrip("Circuit03_Runoff",true,environment.transform);
        PlanCrowds(); terrain=BuildTerrain(environment.transform,detailPrototypes);
        BuildWalls(environment.transform);
        BuildWater(environment.transform);
        BuildHay(environment.transform);
        BuildCrowds(environment.transform);
        BuildVegetation(environment.transform);
        BuildLighting(environment.transform);
        PositionCar(car);
        for(int i=0;i<tuning.Length;i++)if(signature[i]!=EditorJsonUtility.ToJson(tuning[i]))throw new Exception("Vehicle tuning changed: "+tuning[i]);
        if(dynamicsBefore!=EditorJsonUtility.ToJson(dynamics))throw new Exception("Vehicle assistance settings changed.");
        Validate();
        var layout=new Layout{lengthMeters=length, centerline=route.Select(s=>Surface(s.p)).ToArray(),distances=route.Select(s=>s.d).ToArray(),roadWidths=route.Select(s=>s.width).ToArray(),puddleCenters=basins.Select(b=>b.sample.p-Vector3.up*.1f).ToArray(),crowdCenters=crowds.Select(c=>c.center).ToArray()};
        File.WriteAllText(Output+"/Circuit03Layout.json",JsonUtility.ToJson(layout,true));
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Cannot save Circuit_03.");
        if(sourceHash!=Hash(SourceScene)||terrainHash!=Hash(terrainPath))throw new Exception("Circuit_01 source files changed.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/circuit03-build.txt",$"PASS: Circuit_03 saved. Length {length:F1}m; two 180-degree hairpins R28/22m; four-arc chicane; one 130m straight; remaining straights <=110m; three hills (+20,+28,-10m); width 7-12m. Four water basins, 0.28m depression, visual only. 80 spectators in four combined crowds; minimum full-body barrier clearance {crowdClearance:F2}m. 380 pines, 240 bushes, 180 ferns and 50 rocks; instanced shared materials and distance culling. No checkpoint/timer/water-zone components; hay without physics. Source Circuit_01 and terrain hashes unchanged; Porsche tuning unchanged; road collider and basin raycasts passed.\n"+DateTime.Now.ToString("O"));
        Debug.Log("CIRCUIT03_COMPLETE: "+length.ToString("F1")+"m");
    }

    static void DefineRoute()
    {
        sections.Clear();route.Clear();basins.Clear();crowds.Clear();walls.Clear();length=0;
        Vector3 p=Vector3.zero; float heading=0;
        Add(130,0,ref p,ref heading); Turn(180,28,ref p,ref heading);
        Add(80,0,ref p,ref heading); Turn(-180,22,ref p,ref heading);
        Winding(110,25,1,ref p,ref heading); Turn(90,55,ref p,ref heading);
        Add(30,0,ref p,ref heading);
        foreach(float angle in new[]{32f,-32,-32,32})Turn(angle,36,ref p,ref heading);
        Turn(90,70,ref p,ref heading);
        Winding(p.z+160,40,2,ref p,ref heading);
        Turn(90,65,ref p,ref heading);
        Winding(p.x-65,32,2,ref p,ref heading);
        Turn(90,65,ref p,ref heading);
        Winding(-p.z,25,1,ref p,ref heading);
        if(p.magnitude>.01f)throw new Exception("Route does not close: "+p);
        var distances=new SortedSet<float>{0,length};
        foreach(var s in sections){int count=Mathf.CeilToInt(s.length/1f);for(int i=0;i<=count;i++)distances.Add(s.start+s.length*i/count);}
        foreach(float d in distances)route.Add(At(d));
        foreach(float d in new[]{60f,480f,1000f,1500f})basins.Add(new Basin{sample=At(d),id=basins.Count+1});
    }
    static void Add(float distance,float curvature,ref Vector3 p,ref float heading)
    {
        var s=new Section{origin=p,yaw=heading,curvature=curvature,length=distance,start=length};sections.Add(s);
        Position(s,distance,out p,out heading);length+=distance;
    }
    static void Turn(float angle,float radius,ref Vector3 p,ref float heading)=>Add(Mathf.Abs(angle)*Mathf.Deg2Rad*radius,Mathf.Sign(angle)/radius,ref p,ref heading);
    static void Winding(float displacement,float angle,int count,ref Vector3 p,ref float h)
    {
        float r=displacement/count/(4*Mathf.Sin(angle*Mathf.Deg2Rad));
        for(int i=0;i<count;i++)foreach(float turn in new[]{angle,-angle,-angle,angle})Turn(turn*(i%2==0?1:-1),r,ref p,ref h);
    }
    static void Position(Section s,float d,out Vector3 p,out float heading)
    {
        heading=s.yaw+s.curvature*d;
        p=Mathf.Abs(s.curvature)<.00001f?s.origin+new Vector3(Mathf.Sin(s.yaw),0,Mathf.Cos(s.yaw))*d:
            s.origin+new Vector3((Mathf.Cos(s.yaw)-Mathf.Cos(heading))/s.curvature,0,(Mathf.Sin(heading)-Mathf.Sin(s.yaw))/s.curvature);
    }
    static float Hill(float d,float a,float b,float height)=>d<=a||d>=b?0:height*.5f*(1-Mathf.Cos(2*Mathf.PI*(d-a)/(b-a)));
    static Sample At(float d)
    {
        var s=sections.FirstOrDefault(v=>d<=v.start+v.length+.00001f)??sections.Last();
        Position(s,Mathf.Clamp(d-s.start,0,s.length),out var p,out float heading);
        p.y=Hill(d,110,370,20)+Hill(d,560,890,28)+Hill(d,1110,1420,-10);
        if(d>=length-.00001f)p=Vector3.zero;
        float width=12;
        foreach(var bend in sections.Where(v=>Mathf.Abs(v.curvature)>.0001f))
        {
            float gap=Mathf.Max(bend.start-d,d-bend.start-bend.length,0);
            float narrow=Mathf.Abs(bend.curvature)>.03f?7:9;
            width=Mathf.Min(width,Mathf.Lerp(narrow,12,Mathf.SmoothStep(0,1,gap/18)));
        }
        return new Sample{p=p,right=new Vector3(Mathf.Cos(heading),0,-Mathf.Sin(heading)),d=d,width=width};
    }
    static float Drop(Vector3 p)
    {
        float depth=0, contour=Mathf.Sqrt(1-Mathf.Sqrt(.1f/.28f));
        foreach(var b in basins)
        {
            var delta=p-b.sample.p;var forward=Vector3.Cross(b.sample.right,Vector3.up);
            float x=Vector3.Dot(delta,b.sample.right)/(3/contour),z=Vector3.Dot(delta,forward)/(5/contour),r=x*x+z*z;
            if(r<1)depth=Mathf.Max(depth,.28f*(1-r)*(1-r));
        }
        return depth;
    }
    static Vector3 Surface(Vector3 p){p.y-=Drop(p);return p;}
    static void Quad(List<Vector3> v,List<Face> f,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {int n=v.Count;v.AddRange(new[]{a,b,c,d});f.Add(new Face(new[]{n,n+1,n+2,n,n+2,n+3}){manualUV=true,smoothingGroup=1});}
    static ProBuilderMesh RoadStrip(string name,bool shoulders,Transform parent)
    {
        var v=new List<Vector3>();var f=new List<Face>();
        for(int i=0;i<route.Count-1;i++)
        {
            var a=route[i];var b=route[i+1];
            int pieces=shoulders?2:8;
            for(int j=0;j<pieces;j++)
            {
                float a0,a1,b0,b1;
                if(shoulders){float sign=j==0?-1:1;a0=sign<0?-a.width/2-7:a.width/2;a1=a0+7;b0=sign<0?-b.width/2-7:b.width/2;b1=b0+7;}
                else {a0=Mathf.Lerp(-a.width/2,a.width/2,j/(float)pieces);a1=Mathf.Lerp(-a.width/2,a.width/2,(j+1f)/pieces);b0=Mathf.Lerp(-b.width/2,b.width/2,j/(float)pieces);b1=Mathf.Lerp(-b.width/2,b.width/2,(j+1f)/pieces);}
                Quad(v,f,Surface(a.p+a.right*a0),Surface(b.p+b.right*b0),Surface(b.p+b.right*b1),Surface(a.p+a.right*a1));
            }
        }
        var mesh=ProBuilderMesh.Create(v,f);mesh.name=name;mesh.transform.SetParent(parent,false);
        mesh.textures=v.Select(p=>new Vector2(p.x/6,p.z/6)).ToArray();mesh.ToMesh();mesh.Refresh();
        mesh.GetComponent<Renderer>().sharedMaterial=Asset<Material>(Forest+"Materials/"+(shoulders?"Forest floor":"Wet forest road")+".mat");
        var collider=mesh.GetComponent<MeshCollider>();
        if(collider==null)collider=mesh.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh=mesh.GetComponent<MeshFilter>().sharedMesh;
        return mesh;
    }
    static void Nearest(Vector3 p,out float distance,out float height,out float halfWidth)
    {
        float best=float.MaxValue, weighted=0,weights=0;height=0;halfWidth=6;
        // Four-metre segments are sufficient for terrain fitting; the road is sampled at 1m.
        for(int i=0;i<route.Count-1;i+=4)
        {
            var a=route[i];var b=route[Mathf.Min(i+4,route.Count-1)];
            float dx=b.p.x-a.p.x,dz=b.p.z-a.p.z;
            float t=Mathf.Clamp01(((p.x-a.p.x)*dx+(p.z-a.p.z)*dz)/Mathf.Max(.0001f,dx*dx+dz*dz));
            float x=a.p.x+dx*t-p.x,z=a.p.z+dz*t-p.z,d2=x*x+z*z,h=Mathf.Lerp(a.p.y,b.p.y,t);
            if(d2<best){best=d2;height=h;halfWidth=Mathf.Lerp(a.width,b.width,t)/2;}
            if(d2<6400){float w=1/((d2+64)*(d2+64));weighted+=h*w;weights+=w;}
        }
        distance=Mathf.Sqrt(best);
        if(weights>0)height=Mathf.Lerp(height,weighted/weights,Mathf.SmoothStep(0,1,(distance-halfWidth-7)/8));
    }
    static float BaseHeight(Vector3 p)
    {
        Nearest(p,out float distance,out float y,out float hw);
        float ridge=1-Mathf.Abs(2*Mathf.PerlinNoise(p.x*.008f+13,p.z*.008f+37)-1);
        float mountain=24+Mathf.PerlinNoise(p.x*.003f+71,p.z*.003f+26)*110+ridge*35;
        return Mathf.Lerp(y-.18f,mountain,Mathf.SmoothStep(0,1,Mathf.Max(0,distance-hw-7)/210))-Drop(p);
    }
    static void PlanCrowds()
    {
        var bends=sections.Where(s=>Mathf.Abs(s.curvature*s.length*Mathf.Rad2Deg)>89).ToArray();
        foreach(int i in new[]{0,1,2,4})
        {
            var bend=bends[i];var s=At(bend.start+bend.length*.5f);var outward=-Mathf.Sign(bend.curvature)*s.right;
            var c=new Crowd{target=s.p,outward=outward,center=s.p+outward*(s.width/2+25)};
            c.height=BaseHeight(c.center);c.center.y=c.height;crowds.Add(c);
        }
    }
    static Terrain BuildTerrain(Transform parent,DetailPrototype[] details)
    {
        var bounds=new Bounds(route[0].p,Vector3.zero);foreach(var s in route)bounds.Encapsulate(s.p);bounds.Expand(new Vector3(640,0,640));
        var origin=new Vector3(bounds.min.x,-35,bounds.min.z);var size=new Vector3(Mathf.Ceil(bounds.size.x),230,Mathf.Ceil(bounds.size.z));
        const int n=1025;var data=new TerrainData{name="Circuit03_ForestTerrain",heightmapResolution=n,size=size};
        var heights=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var p=origin+new Vector3(x*size.x/(n-1),0,z*size.z/(n-1));float h=BaseHeight(p);
            foreach(var crowd in crowds){float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(crowd.center.x,crowd.center.z));h=Mathf.Lerp(crowd.height,h,Mathf.SmoothStep(0,1,(d-9)/9));}
            heights[z,x]=(h-origin.y)/size.y;
        }
        data.SetHeights(0,0,heights);data.terrainLayers=new[]{"mud_forest","forest_floor","mossy_rock"}.Select(id=>Asset<TerrainLayer>(Forest+"Terrain/"+id+".terrainlayer")).ToArray();
        data.alphamapResolution=512;data.baseMapResolution=1024;var maps=new float[512,512,3];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            float u=x/511f,v=z/511f;var p=origin+new Vector3(u*size.x,0,v*size.z);
            Nearest(p,out float d,out _,out float hw);float far=Mathf.SmoothStep(0,1,(d-hw-8)/40);
            float rock=Mathf.Clamp01((data.GetSteepness(u,v)-13)/28)*far;
            float sand=(.25f+.45f*Mathf.PerlinNoise(p.x*.014f,p.z*.014f))*(1-rock)*far;
            maps[z,x,0]=1-rock-sand;maps[z,x,1]=sand;maps[z,x,2]=rock;
        }
        data.SetAlphamaps(0,0,maps);data.SetDetailResolution(512,32);data.detailPrototypes=details;
        AssetDatabase.CreateAsset(data,Output+"/Circuit03_ForestTerrain.asset");
        var go=Terrain.CreateTerrainGameObject(data);go.name="Circuit03_Terrain";go.transform.SetParent(parent,false);go.transform.position=origin;
        var t=go.GetComponent<Terrain>();t.materialTemplate=Asset<Material>(Forest+"Materials/Forest Terrain.mat");t.drawInstanced=true;t.heightmapPixelError=3;t.basemapDistance=400;t.detailObjectDistance=180;t.detailObjectDensity=1;
        return t;
    }
    static void BuildWalls(Transform parent)
    {
        var root=Root("Circuit 03 - Soft Track Boundaries",parent);var mat=Asset<PhysicsMaterial>("Assets/Scenes/Circuit_01_SoftBoundary.physicMaterial");
        for(int i=0;i<route.Count-1;i+=4)
        {
            var a=route[i];var b=route[Mathf.Min(i+4,route.Count-1)];
            foreach(int sign in new[]{-1,1})
            {
                var p=a.p+sign*a.right*(a.width/2+6.5f);var q=b.p+sign*b.right*(b.width/2+6.5f);
                var go=Root($"Boundary_{sign}_{i:0000}",root.transform);go.transform.SetPositionAndRotation((p+q)/2+Vector3.up*1.5f,Quaternion.LookRotation(q-p));
                var box=go.AddComponent<BoxCollider>();box.size=new Vector3(1,3,Vector3.Distance(p,q)+.8f);box.sharedMaterial=mat;walls.Add(box);
            }
        }
    }
    static void BuildWater(Transform parent)
    {
        var root=Root("Circuit 03 - Water Basins (visual only)",parent);
        foreach(var b in basins)
        {
            var v=new List<Vector3>{Vector3.zero};var f=new List<Face>();const int count=48,rings=16;
            for(int ring=1;ring<=rings;ring++)for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;v.Add(new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*5)*ring/rings);}
            for(int i=0;i<count;i++)f.Add(new Face(new[]{0,1+(i+1)%count,1+i}){manualUV=true,smoothingGroup=1});
            for(int ring=1;ring<rings;ring++)for(int i=0;i<count;i++)
            {
                int a=1+(ring-1)*count+i,next=1+(ring-1)*count+(i+1)%count,c=1+ring*count+(i+1)%count,d=1+ring*count+i;
                f.Add(new Face(new[]{a,next,c,a,c,d}){manualUV=true,smoothingGroup=1});
            }
            var mesh=ProBuilderMesh.Create(v,f);mesh.name=$"Puddle_{b.id:00}_d{b.sample.d:F0}m_6x10m_Depth028";
            mesh.textures=v.Select(p=>new Vector2(p.x/6+.5f,p.z/10+.5f)).ToArray();mesh.ToMesh();mesh.Refresh();
            mesh.transform.SetParent(root.transform);mesh.transform.SetPositionAndRotation(b.sample.p-Vector3.up*.1f,Quaternion.LookRotation(Vector3.Cross(b.sample.right,Vector3.up)));
            // The shared sample adds object-space Gerstner waves. Flatten only their
            // vertical displacement to centimetres; keep footprint/material unchanged.
            mesh.transform.localScale=new Vector3(1,.025f,1);
            mesh.GetComponent<Renderer>().sharedMaterial=Asset<Material>(Env+"Materials/Puddles - Unity sample.mat");
            foreach(var col in mesh.GetComponents<Collider>())Object.DestroyImmediate(col);
        }
    }
    static void BuildHay(Transform parent)
    {
        var root=Root("Circuit 03 - Hay Bales (visual only)",parent);int id=0;
        foreach(var bend in sections.Where(s=>Mathf.Abs(s.curvature*s.length*Mathf.Rad2Deg)>=89))for(int i=0;i<3;i++)
        {
            var s=At(bend.start+bend.length*(.35f+i*.15f));var p=s.p-Mathf.Sign(bend.curvature)*s.right*(s.width/2+3);
            var mesh=ShapeGenerator.GenerateCylinder(PivotLocation.Center,12,.7f,1.3f,0);mesh.name=$"HayBale_{++id:00}";mesh.transform.SetParent(root.transform);
            p.y=Ground(p)+.65f;mesh.transform.position=p;mesh.GetComponent<Renderer>().sharedMaterial=Asset<Material>("Assets/Scenes/Circuit_01_HayPlaceholder.mat");
            foreach(var c in mesh.GetComponents<Collider>())Object.DestroyImmediate(c);
        }
    }
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void BuildCrowds(Transform parent)
    {
        var root=Root("Circuit 03 - Crowds Outside Boundaries",parent);crowdClearance=float.MaxValue;
        string[] names={"Male_Standing_Waving","Female_Standing_CoveringEyes","Male_Standing","Female_Standing_Hips"};
        for(int index=0;index<crowds.Count;index++)
        {
            var crowd=crowds[index];var group=Root($"Crowd_{index+1:00}_20_spectators",root.transform);group.transform.position=crowd.center;
            var tangent=Vector3.Cross(Vector3.up,crowd.outward);var people=new List<GameObject>();
            for(int k=0;k<20;k++)
            {
                var p=crowd.center+tangent*((k%5-2)*1.9f+Jitter(.65f))+crowd.outward*((k/5-1.5f)*1.9f+Jitter(.65f));p.y=Ground(p);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(People+names[k%4]+".prefab"),group.transform);people.Add(go);
                var dir=crowd.target-p;dir.y=0;go.transform.SetPositionAndRotation(p,Quaternion.LookRotation(dir)*Quaternion.Euler(0,Jitter(28),0));go.transform.localScale*=.94f+(float)random.NextDouble()*.12f;
                var bounds=BoundsOf(go);go.transform.position+=Vector3.up*(p.y-bounds.min.y);bounds=BoundsOf(go);
                float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
                Nearest(bounds.center,out float distance,out _,out float hw);if(distance-hw-radius<11)throw new Exception("Spectator inside playable corridor.");
                foreach(var wall in walls)
                {
                    var local=wall.transform.InverseTransformPoint(bounds.center)-wall.center;
                    float dx=Mathf.Max(0,Mathf.Abs(local.x)-wall.size.x/2),dz=Mathf.Max(0,Mathf.Abs(local.z)-wall.size.z/2);
                    float gap=Mathf.Sqrt(dx*dx+dz*dz)-radius;crowdClearance=Mathf.Min(crowdClearance,gap);if(gap<3)throw new Exception("Spectator too close to containment.");
                }
                foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Shirt")?Asset<Material>(People+"Crowd_Shirt_"+(k%4)+".mat"):m).ToArray();
            }
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var go in people)foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var materials=filter.GetComponent<Renderer>().sharedMaterials;
                for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
                {var m=materials[i];if(!batches.ContainsKey(m))batches[m]=new List<CombineInstance>();batches[m].Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=group.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});}
            }
            foreach(var entry in batches)
            {
                var mesh=new Mesh{name=$"Crowd{index+1}_"+entry.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray(),true,true);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Output+"/"+mesh.name+".asset");
                var go=Root(entry.Key.name,group.transform);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=entry.Key;r.shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(var go in people)Object.DestroyImmediate(go);
            var lod=group.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,group.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        }
    }
    static float Jitter(float span)=>((float)random.NextDouble()-.5f)*span;
    static Vector3 Candidate()
    {
        var s=route[random.Next(route.Count)];return s.p+s.right*(random.Next(2)==0?-1:1)*(s.width/2+17+(float)random.NextDouble()*55);
    }
    static bool Plantable(Vector3 p,float margin)
    {
        var d=terrain.terrainData;var n=p-terrain.transform.position;
        if(n.x<0||n.z<0||n.x>=d.size.x||n.z>=d.size.z||d.GetSteepness(n.x/d.size.x,n.z/d.size.z)>28)return false;
        Nearest(p,out float distance,out _,out float hw);
        return distance-hw>margin && crowds.All(c=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(c.center.x,c.center.z))>18);
    }
    static void BuildVegetation(Transform parent)
    {
        var root=Root("Circuit 03 - Forest",parent);var placed=new List<Vector3>();
        string[] names={"PineA","PineB","PineC","Bush","Fern","Rock"};
        int[] targets={140,140,100,240,180,50};
        for(int kind=0;kind<names.Length;kind++)
        {
            int count=0;var prefab=Asset<GameObject>(Forest+"Prefabs/"+names[kind]+".prefab");
            for(int attempt=0;attempt<50000&&count<targets[kind];attempt++)
            {
                var p=Candidate();bool tree=kind<3;float spacing=tree?6:2.3f;
                if(!Plantable(p,tree?14:10)||placed.Any(q=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(q.x,q.z))<spacing))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);go.name=names[kind]+"_"+(++count).ToString("000");
                p.y=Ground(p);var n=p-terrain.transform.position;var d=terrain.terrainData;
                var normal=Vector3.Slerp(Vector3.up,d.GetInterpolatedNormal(n.x/d.size.x,n.z/d.size.z),tree?.2f:.8f);
                go.transform.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)random.NextDouble()*360,0));
                go.transform.localScale*=.75f+(float)random.NextDouble()*.5f;
                var bounds=BoundsOf(go);go.transform.position+=Vector3.up*(p.y-bounds.min.y-.03f);
                Nearest(bounds.center,out float clearance,out _,out float hw);
                if(clearance-hw-new Vector2(bounds.extents.x,bounds.extents.z).magnitude<8){Object.DestroyImmediate(go);count--;continue;}
                placed.Add(p);
                var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(tree?.012f:.025f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
            }
            if(count!=targets[kind])throw new Exception("Cannot safely place "+names[kind]);
        }
        // GPU instancing uses shared meshes/materials, no individual scripts or colliders.
    }
    static void BuildLighting(Transform parent)
    {
        // Circuit_01's baked visibility/lighting is not valid for a different layout.
        Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=Array.Empty<LightmapData>();
        foreach(var camera in Object.FindObjectsByType<Camera>())camera.useOcclusionCulling=false;
        RenderSettings.skybox=Asset<Material>(Forest+"Materials/Overcast sky.mat");
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.47f,.53f,.58f);RenderSettings.ambientEquatorColor=new Color(.34f,.39f,.38f);RenderSettings.ambientGroundColor=new Color(.21f,.24f,.22f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.58f,.65f,.68f);RenderSettings.fogDensity=.001f;
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.color=new Color(.87f,.94f,1);sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(42,-35,0);
        var probe=Root("Overcast sky reflection",parent).AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;probe.cullingMask=0;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.size=new Vector3(2000,500,2000);probe.center=new Vector3(150,100,0);
        foreach(var camera in Object.FindObjectsByType<Camera>()){camera.clearFlags=CameraClearFlags.Skybox;var urp=camera.GetUniversalAdditionalCameraData();urp.requiresDepthTexture=true;urp.requiresColorTexture=true;urp.renderPostProcessing=false;}
    }
    static void PositionCar(GameObject car)
    {
        car.SetActive(true);car.transform.SetPositionAndRotation(new Vector3(0,0,4),Quaternion.identity);
        var controller=car.GetComponent<JrsVehicleController>();var wheels=new[]{controller.frontLeftWheel,controller.frontRightWheel,controller.rearLeftWheel,controller.rearRightWheel};
        float bottom=wheels.Min(w=>w.transform.position.y-(w.radius+w.suspensionDistance)*Mathf.Abs(w.transform.lossyScale.y));car.transform.position+=Vector3.up*(.05f-bottom);
        var camera=GameObject.Find("Main Camera").GetComponent<Camera>();var follow=camera.GetComponent<JrsFollowCamera>();follow.target=car.transform;camera.transform.position=car.transform.TransformPoint(follow.offset);camera.transform.LookAt(car.transform.position+Vector3.up*.5f);
        Object.FindAnyObjectByType<JrsInputController>().cameras=new[]{camera};Physics.SyncTransforms();
    }
    static void Validate()
    {
        if(length<1350||length>1750||Vector3.Distance(route[0].p,route.Last().p)>.01f)throw new Exception("Invalid circuit length/closure.");
        var col=road.GetComponent<MeshCollider>();Physics.SyncTransforms();float maxGap=0;
        for(int i=2;i<route.Count-2;i+=3)
        {
            var s=route[i];foreach(float side in new[]{-.4f,0,.4f})
            {
                var p=Surface(s.p+s.right*s.width*side);
                if(!col.Raycast(new Ray(p+Vector3.up*5,Vector3.down),out var hit,10)||Mathf.Abs(hit.point.y-p.y)>.06f)throw new Exception("Road collider gap at "+s.d);
                float gap=hit.point.y-Ground(p);maxGap=Mathf.Max(maxGap,gap);if(gap<-.025f||gap>.4f)throw new Exception("Terrain/road fit at "+s.d+": "+gap);
            }
        }
        foreach(var b in basins)if(!col.Raycast(new Ray(b.sample.p+Vector3.up*5,Vector3.down),out var hit,10)||b.sample.p.y-hit.point.y<.23f)throw new Exception("Missing basin depression.");
        if(Object.FindObjectsByType<RallyCheckpointManager>().Length!=0||Object.FindObjectsByType<RallyCheckpointTrigger>().Length!=0||Object.FindObjectsByType<RallyPuddleSlowZone>().Length!=0||Object.FindObjectsByType<RallyRaceFlow>().Length!=0||Object.FindObjectsByType<RallyRaceHud>().Length!=0)throw new Exception("Race logic unexpectedly retained.");
        if(GameObject.Find("Circuit 03 - Hay Bales (visual only)").GetComponentsInChildren<Rigidbody>().Length!=0)throw new Exception("Hay physics unexpectedly present.");
        Debug.Log("Circuit03 road/terrain fit: maximum gap "+maxGap);
    }
    // Only our disposable generation checkout may reset incomplete generated output.
    public static void RebuildStaging()
    {
        if(new DirectoryInfo(Directory.GetCurrentDirectory()).Name!="rally-basin-staging")throw new Exception("Staging-only operation.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);AssetDatabase.DeleteAsset(Output);Build();
    }
    [MenuItem("Tools/Rally/Preview Circuit 03")]
    public static void Preview()
    {
        EditorSceneManager.OpenScene(ScenePath);Directory.CreateDirectory("Logs");
        var go=Root("Temporary Circuit03 preview");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1600;camera.fieldOfView=58;
        var urp=camera.GetUniversalAdditionalCameraData();urp.requiresColorTexture=true;urp.requiresDepthTexture=true;
        Vector3[] positions={new Vector3(660,540,-620),new Vector3(-40,14,102),new Vector3(115,10,28),new Vector3(-13,5,47)};
        Vector3[] targets={new Vector3(150,0,0),new Vector3(22,6,170),new Vector3(72,0,43),new Vector3(0,-.1f,60)};
        for(int i=0;i<positions.Length;i++)
        {
            camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("Logs/Circuit03_Preview_"+i+".png",tex.EncodeToPNG());
            RenderTexture.active=prior;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
        }
        Object.DestroyImmediate(go);
    }
}
