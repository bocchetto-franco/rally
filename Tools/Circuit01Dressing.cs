using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Circuit01Dressing
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string Folder = "Assets/Art/Environment/Circuit01Dressing";
    const string RootName = "Circuit 01 - Desert Rally Dressing";
    static Scene scene;
    static Terrain terrain;
    static Vector3[] route;
    static float[] stations;
    static float length;
    static readonly List<Bounds> protectedCrowds=new List<Bounds>();
    static readonly List<Bounds> occupied=new List<Bounds>();
    static readonly List<Vector3> placements=new List<Vector3>();
    static readonly Dictionary<string,GameObject> props=new Dictionary<string,GameObject>();
    static readonly List<GameObject> rocks=new List<GameObject>();
    static readonly System.Random random=new System.Random(71026);
    static StringBuilder report;
    static Transform dressing;
    static float Next(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    static Vector3 Normal(Vector3 p)
    {
        var local=p-terrain.transform.position; var size=terrain.terrainData.size;
        return terrain.terrainData.GetInterpolatedNormal(local.x/size.x,local.z/size.z);
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;
        foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
    }
    static string ProtectedState()=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
        .Where(c=>c is Rigidbody||c is Collider&&!(c is TerrainCollider)||c is JrsVehicleController||c is RallyVehicleDynamics||c is RallyBotController||c is RallyCheckpointManager||c is RallyCheckpointTrigger||c is RallyBrakeWarningTrigger||c is ProBuilderMesh)
        .Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
    public static void Build()
    {
        RequireIdle();var setup=EditorSceneManager.GetSceneManagerSetup();
        string backup="Logs/SceneBackups/Circuit01Dressing_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Prefabs");
        report=new StringBuilder("Circuit 01 desert rally dressing\nBackup: "+backup+"\n");
        try
        {
            AssetDatabase.Refresh();EditorSceneManager.OpenScene(ScenePath);ReadScene();
            if(GameObject.Find(RootName)!=null)throw new Exception("Dressing already exists; refusing duplicate placements.");
            File.Copy(ScenePath,backup+"/Circuit_01.unity");
            string before=ProtectedState();
            foreach(string name in new[]{"Large Rally Crowds - Outside Barriers","Rally Spectators - Outside Barriers"})
            {
                var root=GameObject.Find(name);if(root==null)continue;
                foreach(Transform group in root.transform) { var b=BoundsOf(group.gameObject);b.Expand(8);protectedCrowds.Add(b); }
            }
            PrepareProps();PrepareRocks();
            var original=terrain.terrainData;string sourcePath=AssetDatabase.GetAssetPath(original),copyPath=Folder+"/Circuit01_DressedTerrain.asset";
            var priorCopy=AssetDatabase.LoadAssetAtPath<TerrainData>(copyPath);
            if(priorCopy!=null) { EditorUtility.CopySerialized(original,priorCopy);EditorUtility.SetDirty(priorCopy); }
            else if(!AssetDatabase.CopyAsset(sourcePath,copyPath))throw new Exception("Terrain snapshot failed.");
            terrain.terrainData=AssetDatabase.LoadAssetAtPath<TerrainData>(copyPath);terrain.GetComponent<TerrainCollider>().terrainData=terrain.terrainData;
            dressing=new GameObject(RootName).transform;
            Sculpt();Decorate();PlantClusters();AddDetails();AddRocks();
            Physics.SyncTransforms();
            if(before!=ProtectedState())throw new Exception("Protected road, physics, checkpoints or pacenotes changed; refusing scene save.");
            if(dressing.GetComponentsInChildren<Collider>().Length!=0||dressing.GetComponentsInChildren<MonoBehaviour>().Length!=0)throw new Exception("Dressing contains unexpected gameplay components.");
            float minimum=placements.Min(Clearance);
            foreach(var renderer in dressing.GetComponentsInChildren<Renderer>())
            {
                if(renderer.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported))throw new Exception("Invalid decorative material: "+renderer.name);
                var b=renderer.bounds;float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
                // Combined shrub patches have individual samples verified separately.
                if(renderer.name.StartsWith("Patch"))continue;
                if(Clearance(b.center)-radius<13.7f)throw new Exception("Decor bounds approach playable runoff: "+renderer.name);
            }
            var player=GameObject.Find(RallyGameSession.VehicleName);var grid=GameObject.Find("Starting Grid").transform;
            if(!player.activeInHierarchy||Vector3.Distance(player.transform.position,grid.GetChild(0).position)>12)throw new Exception("Player is not active at the start grid.");
            EditorUtility.SetDirty(terrain.terrainData);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
            report.AppendLine("Minimum added plant/prop center distance to centerline: "+minimum.ToString("F2")+"m. All prop bounds outside 13.7m corridor; shrubs validated individually. No new colliders/scripts; road and gameplay signature unchanged. Player at start.");
            report.AppendLine("Added renderers="+dressing.GetComponentsInChildren<Renderer>().Length+", LOD groups="+dressing.GetComponentsInChildren<LODGroup>().Length+". Existing render quality settings unchanged.");
            Capture(0,"after-start",true);Capture(320,"after-curves",true);Capture(1000,"after-straight",true);Capture(600,"after-hairpin",true);Capture(25,"after-driving",false);
            report.AppendLine("BUILD COMPLETE: PASS");
        }
        catch(Exception e){report.AppendLine("FAILED: "+e);throw;}
        finally{File.WriteAllText("Logs/Circuit01Dressing/build.txt",report.ToString());EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static Material Lit(string name,Color color,Texture texture=null)
    {
        string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.16f);m.SetFloat("_Metallic",0);m.SetFloat("_Cull",0);
        if(texture!=null)m.SetTexture("_BaseMap",texture);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static void PrepareProps()
    {
        string source=Folder+"/KenneyRacing/Models/";
        foreach(string texture in Directory.GetFiles(source+"Textures","*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(texture);importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;importer.anisoLevel=2;importer.SaveAndReimport();
        }
        var materialCache=new Dictionary<Material,Material>();
        foreach(string id in new[]{"billboardLow","billboardLower","flagCheckers","flagRed","flagGreen","flagTankco","tent","tentLong","bannerTowerRed"})
        {
            string path=source+id+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.addCollider=false;importer.SaveAndReimport();
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach(var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials=r.sharedMaterials.Select(old=>
                {
                    if(materialCache.TryGetValue(old,out var mat))return mat;
                    string name=old.name.Replace(" (Instance)","");Color color=old.HasProperty("_Color")?old.color:Color.white;Texture tex=old.mainTexture;
                    if(name.ToLowerInvariant().Contains("tankco"))tex=AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Textures/tankco.png");
                    if(name.ToLowerInvariant().Contains("checker"))tex=AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Textures/checkers.png");
                    mat=Lit("Kenney "+name,color,tex);materialCache[old]=mat;return mat;
                }).ToArray();SetRenderer(r);
            }
            var wrapper=new GameObject(id);go.transform.SetParent(wrapper.transform,false);
            Bounds b=BoundsOf(wrapper);float target=id.StartsWith("billboard")?5.5f:id.StartsWith("tent")?3.4f:3.6f;
            float scale=target/(id.StartsWith("billboard")?b.size.x:b.size.y);go.transform.localScale*=scale;
            b=BoundsOf(wrapper);go.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            AddLod(wrapper,.018f);
            props[id]=PrefabUtility.SaveAsPrefabAsset(wrapper,Folder+"/Prefabs/"+id+".prefab");Object.DestroyImmediate(wrapper);
        }
    }
    static void SetRenderer(Renderer r)
    {
        r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.OccludeeStatic);
    }
    static void AddLod(GameObject go,float threshold)
    {
        var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(threshold,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
    }
    static void PrepareRocks()
    {
        string src="Assets/Art/Environment/PolyHaven/namaqualand_rocks_01/namaqualand_rocks_01.fbx",dst=Folder+"/RockSource.fbx";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(dst)==null&&!AssetDatabase.CopyAsset(src,dst))throw new Exception("Cannot copy existing rock source.");
        var importer=(ModelImporter)AssetImporter.GetAtPath(dst);importer.isReadable=true;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(dst);
        var rockMaterial=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/namaqualand_rocks_01.prefab").GetComponentInChildren<Renderer>().sharedMaterial;
        int index=0;
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="Desert rock "+index;
            var vertices=mesh.vertices.Select(filter.transform.localToWorldMatrix.MultiplyPoint3x4).ToArray();var b=new Bounds(vertices[0],Vector3.zero);foreach(var p in vertices)b.Encapsulate(p);
            float scale=2.6f/Mathf.Max(b.size.x,b.size.z);var offset=new Vector3(b.center.x,b.min.y,b.center.z);
            mesh.vertices=vertices.Select(v=>(v-offset)*scale).ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string meshPath=Folder+"/Meshes/Rock_"+index+".asset";mesh=SaveMesh(mesh,meshPath);
            var go=new GameObject("Desert rock "+index,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=rockMaterial;SetRenderer(go.GetComponent<Renderer>());AddLod(go,.02f);
            rocks.Add(PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Prefabs/Rock_"+index+".prefab"));Object.DestroyImmediate(go);index++;
        }
    }
    static Mesh SaveMesh(Mesh mesh,string path)
    {
        var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(previous==null)AssetDatabase.CreateAsset(mesh,path);
        else { EditorUtility.CopySerialized(mesh,previous);EditorUtility.SetDirty(previous);Object.DestroyImmediate(mesh);mesh=previous; }
        return mesh;
    }
    static bool Safe(Vector3 p,float radius,float slope=28)
    {
        var local=p-terrain.transform.position;var size=terrain.terrainData.size;
        if(local.x<2||local.z<2||local.x>size.x-2||local.z>size.z-2||Clearance(p)<14+radius||Vector3.Angle(Normal(p),Vector3.up)>slope)return false;
        foreach(var b in protectedCrowds.Concat(occupied))
        {
            float dx=Mathf.Max(b.min.x-p.x,0,p.x-b.max.x),dz=Mathf.Max(b.min.z-p.z,0,p.z-b.max.z);
            if(dx*dx+dz*dz<(radius+1)*(radius+1))return false;
        }
        return true;
    }
    static void Sculpt()
    {
        var data=terrain.terrainData;int n=data.heightmapResolution;var old=data.GetHeights(0,0,n,n);var next=(float[,])old.Clone();
        var mounds=new List<Vector4>();
        foreach(float s in new[]{110f,215,440,770,910,1030,1200,1350})
        {
            Sample(s,out var p,out var f);Vector3 right=Vector3.Cross(Vector3.up,f).normalized;
            foreach(int side in new[]{-1,1})
            {
                Vector3 candidate=p+right*side*Next(32,60);
                if(!Safe(candidate,8,18))continue;
                mounds.Add(new Vector4(candidate.x,candidate.z,Next(17,30),Next(1.2f,3.4f)));break;
            }
        }
        var plants=new List<Transform>();
        foreach(string name in new[]{"Sparse CC0 vegetation and rocks","Additional CC0 desert trees"}) { var root=GameObject.Find(name);if(root!=null)plants.AddRange(root.transform.Cast<Transform>()); }
        var grounds=plants.Select(t=>Ground(t.position)).ToArray();int changed=0;float maxDelta=0;
        var size=data.size;var origin=terrain.transform.position;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            Vector3 p=origin+new Vector3(x*size.x/(n-1),0,z*size.z/(n-1));float delta=0;
            foreach(var m in mounds){float r=new Vector2((p.x-m.x)/m.z,(p.z-m.y)/m.z).magnitude;if(r<1){float a=1-r*r;delta+=m.w*a*a*a;}}
            if(delta<.01f)continue;
            float distance=Clearance(p);if(distance<=22)continue;delta*=Mathf.SmoothStep(0,1,(distance-22)/12);
            foreach(var b in protectedCrowds){float dx=Mathf.Max(b.min.x-p.x,0,p.x-b.max.x),dz=Mathf.Max(b.min.z-p.z,0,p.z-b.max.z);delta*=Mathf.SmoothStep(0,1,Mathf.Sqrt(dx*dx+dz*dz)/8);}
            if(delta<.01f)continue;next[z,x]+=delta/size.y;changed++;maxDelta=Mathf.Max(maxDelta,delta);
        }
        if(mounds.Count<3||changed<100)throw new Exception("Insufficient local terrain variation.");
        data.SetHeights(0,0,next);int anchored=0;
        for(int i=0;i<plants.Count;i++){float delta=Ground(plants[i].position)-grounds[i];if(Mathf.Abs(delta)>.002f){plants[i].position+=Vector3.up*delta;anchored++;}}
        var actual=data.GetHeights(0,0,n,n);float outside=0;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(old[z,x]==next[z,x])outside=Mathf.Max(outside,Mathf.Abs(old[z,x]-actual[z,x])*size.y);
        if(outside>.008f)throw new Exception("Unexpected Terrain edits outside local mound masks.");
        report.AppendLine(mounds.Count+" local mounds, "+changed+" height samples, maximum raise "+maxDelta.ToString("F2")+"m; road/runoff 22m centerline corridor preserved. Regrounded "+anchored+" existing props; outside-mask height delta="+outside.ToString("F4")+"m.");
    }
    static GameObject PlaceProp(string id,float station,int side,float offset,float scale=1)
    {
        var prefab=props[id];var pb=BoundsOf(prefab);float radius=new Vector2(pb.extents.x,pb.extents.z).magnitude*scale;
        bool tent=id.StartsWith("tent");
        for(int attempt=0;attempt<240;attempt++)
        {
            Sample(station+(attempt==0?0:Next(tent?-100:-35,tent?100:35)),out var p,out var f);var track=p;
            int chosenSide=attempt>100?-side:side;
            float lateral=tent?Next(15+radius,36+radius):offset+Next(0,8);
            p+=Vector3.Cross(Vector3.up,f).normalized*chosenSide*lateral;
            if(!Safe(p,radius,tent?12:25))continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,dressing);go.name=(tent?"Team canopy":id.StartsWith("billboard")?"Sponsor board":"Circuit pennant")+" - "+station.ToString("0000")+" - "+side;
            p.y=Ground(p);Vector3 look=track-p;look.y=0;
            go.transform.SetPositionAndRotation(p,Quaternion.LookRotation(look.normalized,Vector3.up));go.transform.localScale=Vector3.one*scale;
            var b=BoundsOf(go);p.y+=Ground(b.center)-b.min.y;go.transform.position=p;
            b=BoundsOf(go);if(Clearance(b.center)-new Vector2(b.extents.x,b.extents.z).magnitude<13.8f){Object.DestroyImmediate(go);continue;}
            if(tent)
            {
                // A thin gravel pad follows the ground under the canopy feet.
                var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(pad.GetComponent<Collider>());pad.name="Service gravel pad";pad.transform.SetParent(go.transform,true);
                pad.transform.position=new Vector3(b.center.x,b.min.y-.13f,b.center.z);pad.transform.rotation=go.transform.rotation;pad.transform.localScale=new Vector3(pb.size.x+.35f,.25f,pb.size.z+.35f);
                pad.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Materials/Road - dry gravel.mat");SetRenderer(pad.GetComponent<Renderer>());AddLod(go,.015f);
            }
            occupied.Add(BoundsOf(go));placements.Add(p);return go;
        }
        throw new Exception("No safe location for "+id+" near "+station);
    }
    static void Decorate()
    {
        foreach(var item in new[]{(15f,-1),(360f,1),(1250f,-1)})PlaceProp(item.Item1==360?"tentLong":"tent",item.Item1,item.Item2,24);
        int signs=0,flags=0;
        foreach(float s in new[]{0f,80,180,285,355,460,565,650,775,910,1040,1150,1290,1390}) { PlaceProp(signs%3==0?"billboardLower":"billboardLow",s,signs%2==0?1:-1,19);signs++; }
        foreach(float s in new[]{5f,60,280,335,560,610,910,1140,1390})foreach(int side in new[]{-1,1}){PlaceProp(s==5?"flagCheckers":side<0?"flagRed":"flagTankco",s,side,17);flags++;}
        PlaceProp("bannerTowerRed",30,1,24,.85f);PlaceProp("bannerTowerRed",600,-1,23,.85f);
        report.AppendLine("Decor: "+signs+" textured fictional sponsor boards, "+flags+" flags, 2 banner towers, 3 team canopies with gravel pads. Kenney CC0; all outside containment.");
    }
    static void PlantClusters()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/wild_rooibos_bush_upright.prefab");
        var source=prefab.GetComponent<MeshFilter>().sharedMesh;var material=prefab.GetComponent<Renderer>().sharedMaterial;int count=0,groups=0;
        for(float station=15;station<length;station+=34)
        {
            Sample(station,out var center,out var f);int side=groups%2==0?1:-1;center+=Vector3.Cross(Vector3.up,f).normalized*side*Next(22,36);
            var full=new List<CombineInstance>();var reduced=new List<CombineInstance>();
            int plantsInGroup=0;
            for(int trial=0;trial<80&&plantsInGroup<14;trial++)
            {
                var p=center+new Vector3(Next(-9,9),0,Next(-9,9));if(!Safe(p,2.3f,30))continue;
                if(placements.Any(q=>new Vector2(q.x-p.x,q.z-p.z).sqrMagnitude<2.8f*2.8f))continue;
                p.y=Ground(p)-.08f;float scale=Next(1.2f,1.8f);var rotation=Quaternion.FromToRotation(Vector3.up,Normal(p))*Quaternion.Euler(0,Next(0,360),0);
                // All three submeshes use the same atlas. Submesh zero alone is only a few stems.
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var combine=new CombineInstance{mesh=source,subMeshIndex=sub,transform=Matrix4x4.TRS(p,rotation,Vector3.one*scale)};
                    full.Add(combine);if(plantsInGroup%3==0)reduced.Add(combine);
                }
                plantsInGroup++;
                placements.Add(p);count++;
            }
            if(full.Count==0)continue;
            var group=new GameObject("Dry shrub cluster "+groups.ToString("00"));group.transform.SetParent(dressing,false);
            var high=Combined("Patch nearby "+groups,full,material,group.transform);var low=Combined("Patch distant "+groups,reduced,material,group.transform);
            var lod=group.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.055f,new[]{high}),new LOD(.012f,new[]{low})});lod.RecalculateBounds();groups++;
        }
        if(count<220)throw new Exception("Insufficient safely spaced shrub clumps.");
        report.AppendLine(count+" dry rooibos shrubs combined into "+groups+" patches, two LOD levels and culling; no per-plant GameObjects.");
    }
    public static void Refine()
    {
        RequireIdle();var setup=EditorSceneManager.GetSceneManagerSetup();report=new StringBuilder("Visual refinement\n");
        try
        {
            EditorSceneManager.OpenScene(ScenePath);ReadScene();string before=ProtectedState();
            dressing=GameObject.Find(RootName).transform;
            foreach(string name in new[]{"Large Rally Crowds - Outside Barriers","Rally Spectators - Outside Barriers"})
            {
                var root=GameObject.Find(name);if(root==null)continue;
                foreach(Transform group in root.transform){var b=BoundsOf(group.gameObject);b.Expand(8);protectedCrowds.Add(b);}
            }
            foreach(var group in dressing.Cast<Transform>().Where(t=>t.name.StartsWith("Dry shrub cluster")).ToArray())Object.DestroyImmediate(group.gameObject);
            int supports=0,embedded=0;
            foreach(Transform item in dressing)
            {
                if(item.name.Contains("rock")||item.name.StartsWith("Rock outcrop"))
                {
                    var mf=item.GetComponentInChildren<MeshFilter>();var verts=mf.sharedMesh.vertices;
                    float floor=mf.sharedMesh.bounds.min.y+mf.sharedMesh.bounds.size.y*.18f;
                    var distances=verts.Where(v=>v.y<=floor).Select(v=>mf.transform.TransformPoint(v)).Select(v=>v.y-Ground(v)).OrderBy(v=>v).ToArray();
                    if(distances.Length>0)item.position-=Vector3.up*(distances[distances.Length/2]+.15f);
                    embedded++;
                }
                if(item.name.StartsWith("Sponsor board"))
                {
                    float nearest=0,dist=float.PositiveInfinity;
                    for(float s=0;s<length;s+=3){Sample(s,out var p,out var f);float d=(p-item.position).sqrMagnitude;if(d<dist){dist=d;nearest=s;}}
                    Sample(nearest-24,out var approach,out var direction);Vector3 look=approach-item.position;look.y=0;item.rotation=Quaternion.LookRotation(-look);
                    var b=BoundsOf(item.gameObject);item.position+=Vector3.up*(Ground(b.center)-b.min.y-.07f);
                }
                if(item.name.StartsWith("Team canopy"))
                {
                    Transform pad=item.Find("Service gravel pad");var pb=pad.GetComponent<Renderer>().bounds;
                    float lo=float.PositiveInfinity,hi=float.NegativeInfinity;
                    foreach(float x in new[]{-.5f,0,.5f})foreach(float z in new[]{-.5f,0,.5f})
                    {var p=pad.TransformPoint(new Vector3(x,0,z));float h=Ground(p);lo=Mathf.Min(lo,h);hi=Mathf.Max(hi,h);}
                    float shift=hi+.08f-pb.max.y;item.position+=Vector3.up*shift;
                    var scale=pad.localScale;scale.y=hi-lo+.25f;pad.localScale=scale;
                    var position=pad.position;position.y=(hi+lo)/2-.045f;pad.position=position;
                    supports++;
                }
                occupied.Add(BoundsOf(item.gameObject));
            }
            PlantClusters();
            if(before!=ProtectedState())throw new Exception("Gameplay changed during visual refinement.");
            foreach(var r in dressing.GetComponentsInChildren<Renderer>())
            {
                if(r.name.StartsWith("Patch"))continue;var b=r.bounds;
                if(Clearance(b.center)-new Vector2(b.extents.x,b.extents.z).magnitude<13.7f)throw new Exception("Prop encroaches on runoff: "+r.name);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Capture(0,"after-start",true);Capture(320,"after-curves",true);Capture(1000,"after-straight",true);Capture(600,"after-hairpin",true);Capture(25,"after-driving",false);
            report.AppendLine("Rock seating="+embedded+", supported canopy pads="+supports+"; all billboard faces angled toward approaching cars. Gameplay unchanged; prop bounds outside containment. COMPLETE: PASS");
        }
        finally{File.WriteAllText("Logs/Circuit01Dressing/refine.txt",report.ToString());EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    public static void AssetPreview()
    {
        RequireIdle();var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);var r=new StringBuilder();
            var camera=Camera.main;camera.transform.SetPositionAndRotation(new Vector3(0,3,-12),Quaternion.Euler(8,0,0));camera.fieldOfView=50;
            var paths=new[]{"Assets/Art/Environment/Prefabs/wild_rooibos_bush_upright.prefab","Assets/Art/Environment/Prefabs/searsia_lucida_optimized.prefab",Folder+"/Prefabs/billboardLow.prefab"};
            for(int i=0;i<paths.Length;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));go.transform.position=new Vector3((i-1)*5,0,0);
                foreach(var lod in go.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>())r.AppendLine(go.name+" / "+mf.name+" mesh="+mf.sharedMesh.name+" submeshes="+mf.sharedMesh.subMeshCount+" bounds="+mf.sharedMesh.bounds+" materials="+string.Join(",",mf.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name)));
            }
            var rt=new RenderTexture(1440,810,24);camera.targetTexture=rt;var old=RenderTexture.active;camera.Render();RenderTexture.active=rt;var img=new Texture2D(1440,810,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1440,810),0,0);img.Apply();File.WriteAllBytes("Logs/Circuit01Dressing/asset-preview.png",img.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(img);
            File.WriteAllText("Logs/Circuit01Dressing/asset-preview.txt",r.ToString());
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    public static void FinishVegetation()
    {
        RequireIdle();var setup=EditorSceneManager.GetSceneManagerSetup();report=new StringBuilder("Final scenery audit\n");
        try
        {
            EditorSceneManager.OpenScene(ScenePath);ReadScene();string before=ProtectedState();dressing=GameObject.Find(RootName).transform;
            if(dressing.Find("Dry tree groves")!=null)throw new Exception("Tree groves already present.");
            foreach(string name in new[]{"Large Rally Crowds - Outside Barriers","Rally Spectators - Outside Barriers"})
            {
                var root=GameObject.Find(name);if(root==null)continue;
                foreach(Transform group in root.transform){var b=BoundsOf(group.gameObject);b.Expand(8);protectedCrowds.Add(b);}
            }
            foreach(Transform item in dressing)
            {
                if(item.name.StartsWith("Sponsor board"))item.rotation*=Quaternion.Euler(0,180,0);
                if(!item.name.StartsWith("Dry shrub"))occupied.Add(BoundsOf(item.gameObject));
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/quiver_tree_01_optimized.prefab");
            var grove=new GameObject("Dry tree groves");grove.transform.SetParent(dressing,false);int count=0;
            for(int attempt=0;attempt<1500&&count<24;attempt++)
            {
                float station=20+(count/3)*185+Next(-28,28);Sample(station,out var p,out var f);p+=Vector3.Cross(Vector3.up,f).normalized*(count%2==0?-1:1)*Next(22,40);
                if(!Safe(p,4,24))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,grove.transform);go.name="Dry quiver tree "+count.ToString("00");
                var b=BoundsOf(go);go.transform.localScale*=Next(3.7f,5.4f)/b.size.y;
                p.y=Ground(p);go.transform.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,Normal(p))*Quaternion.Euler(0,Next(0,360),0));
                b=BoundsOf(go);go.transform.position+=Vector3.up*(Ground(b.center)-b.min.y-.18f);b=BoundsOf(go);
                if(Clearance(b.center)-new Vector2(b.extents.x,b.extents.z).magnitude<14){Object.DestroyImmediate(go);continue;}
                foreach(var r in go.GetComponentsInChildren<Renderer>())SetRenderer(r);
                AddLod(go,.013f);occupied.Add(b);count++;
            }
            report.AppendLine("Trees placed="+count+"; gameplay same="+(before==ProtectedState()));
            if(count<12||before!=ProtectedState())throw new Exception("Incomplete planting or gameplay changed: trees="+count);
            if(dressing.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Unexpected decoration collision.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Capture(0,"after-start",true);Capture(320,"after-curves",true);Capture(1000,"after-straight",true);Capture(600,"after-hairpin",true);Capture(25,"after-driving",false);
            report.AppendLine(count+" existing optimized quiver trees, height 3.7-5.4m, planted outside containment. Billboard front faces toward approaching cars. Gameplay and geometry preserved; no added colliders. COMPLETE: PASS");
        }
        finally{File.WriteAllText("Logs/Circuit01Dressing/final.txt",report.ToString());EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static MeshRenderer Combined(string name,List<CombineInstance> parts,Material material,Transform parent)
    {
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true);mesh.RecalculateBounds();mesh=SaveMesh(mesh,Folder+"/Meshes/"+name+".asset");
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;SetRenderer(r);return r;
    }
    static void AddDetails()
    {
        var data=terrain.terrainData;int layer=Array.FindIndex(data.detailPrototypes,p=>p.prototype!=null&&p.prototype.name.Contains("rooibos"));if(layer<0)throw new Exception("Missing existing rooibos detail layer.");
        int[,] cells=data.GetDetailLayer(0,0,data.detailWidth,data.detailHeight,layer);int added=0;
        for(int attempt=0;attempt<18000&&added<650;attempt++)
        {
            Sample(Next(0,length),out var p,out var f);p+=Vector3.Cross(Vector3.up,f).normalized*(random.Next(2)==0?-1:1)*Next(19,48);
            var local=p-terrain.transform.position;int x=Mathf.FloorToInt(local.x/data.size.x*data.detailWidth),z=Mathf.FloorToInt(local.z/data.size.z*data.detailHeight);
            if(x<0||z<0||x>=data.detailWidth||z>=data.detailHeight||cells[z,x]!=0)continue;
            p=terrain.transform.position+new Vector3((x+.5f)/data.detailWidth*data.size.x,0,(z+.5f)/data.detailHeight*data.size.z);
            if(!Safe(p,3,24))continue;cells[z,x]=1;added++;
        }
        data.SetDetailLayer(0,0,layer,cells);report.AppendLine("Added "+added+" dry shrub Terrain Detail cells using the existing instanced prototype; original detail settings retained.");
    }
    static void AddRocks()
    {
        int count=0,formations=0;
        for(int attempt=0;attempt<3000&&count<72;attempt++)
        {
            bool large=count%7==0;float scale=large?Next(2.1f,3.8f):Next(.55f,1.4f);
            Sample(Next(0,length),out var p,out var f);p+=Vector3.Cross(Vector3.up,f).normalized*(random.Next(2)==0?-1:1)*Next(large?30:18,large?62:36);
            if(!Safe(p,scale*2,large?34:28))continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(rocks[count%rocks.Count],dressing);go.name=(large?"Rock outcrop ":"Loose desert rock ")+count.ToString("00");p.y=Ground(p)-scale*.15f;
            go.transform.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,Normal(p))*Quaternion.Euler(0,Next(0,360),0));go.transform.localScale=Vector3.one*scale;
            var b=BoundsOf(go);float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
            if(Clearance(b.center)-radius<13.8f){Object.DestroyImmediate(go);continue;}
            occupied.Add(b);placements.Add(p);count++;if(large)formations++;
        }
        if(count<50)throw new Exception("Insufficient safe rock sites.");
        report.AppendLine(count+" reused photogrammetry rocks including "+formations+" larger outcrops, shared meshes/materials and LOD culling.");
    }
    static void ReadScene()
    {
        scene = SceneManager.GetActiveScene();
        terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>()).Single();
        var points = GameObject.Find("AI_Waypoints").transform;
        route = points.Cast<Transform>().Select(t => t.position).ToArray();
        stations = new float[route.Length + 1];
        for (int i=0;i<route.Length;i++) stations[i+1]=stations[i]+Vector3.Distance(route[i],route[(i+1)%route.Length]);
        length = stations[route.Length];
    }
    static void RequireIdle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play first.");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Preserve unsaved scenes before dressing.");
    }
    public static void Inspect()
    {
        RequireIdle(); var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(ScenePath); ReadScene();
            var report=new StringBuilder();
            report.AppendLine("Terrain: "+AssetDatabase.GetAssetPath(terrain.terrainData)+" position="+terrain.transform.position+" size="+terrain.terrainData.size+" resolution="+terrain.terrainData.heightmapResolution);
            foreach(var root in scene.GetRootGameObjects()) report.AppendLine("ROOT "+root.name+" children="+root.transform.childCount+" renderers="+root.GetComponentsInChildren<Renderer>().Length);
            for(int i=0;i<terrain.terrainData.detailPrototypes.Length;i++) report.AppendLine("DETAIL "+i+" "+AssetDatabase.GetAssetPath(terrain.terrainData.detailPrototypes[i].prototype));
            foreach(string path in new[]{"namaqualand_rocks_01","wild_rooibos_bush_upright","searsia_lucida_optimized"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/"+path+".prefab");
                foreach(var f in prefab.GetComponentsInChildren<MeshFilter>()) report.AppendLine(path+" mesh="+f.name+" vertices="+f.sharedMesh.vertexCount+" bounds="+f.sharedMesh.bounds+" transform="+f.transform.localToWorldMatrix);
            }
            for(float s=0;s<length;s+=100) { Sample(s,out var p,out var f); report.AppendLine("ROUTE "+s+" "+p+" f="+f); }
            Directory.CreateDirectory("Logs/Circuit01Dressing");
            File.WriteAllText("Logs/Circuit01Dressing/inventory.txt",report.ToString());
            Capture(0,"before-start",true); Capture(320,"before-curves",true); Capture(1000,"before-straight",true);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    static void Sample(float distance,out Vector3 p,out Vector3 forward)
    {
        distance=Mathf.Repeat(distance,length); int i=0; while(i+1<route.Length&&stations[i+1]<distance)i++;
        forward=(route[(i+1)%route.Length]-route[i]).normalized;
        p=Vector3.Lerp(route[i],route[(i+1)%route.Length],(distance-stations[i])/(stations[i+1]-stations[i]));
    }
    static float Clearance(Vector3 point)
    {
        float nearest=float.PositiveInfinity;
        for(int i=0;i<route.Length;i++)
        {
            Vector3 a=route[i], d=route[(i+1)%route.Length]-a; d.y=0; Vector3 delta=point-a;delta.y=0;
            float t=Mathf.Clamp01(Vector3.Dot(delta,d)/Mathf.Max(.001f,d.sqrMagnitude));
            nearest=Mathf.Min(nearest,(delta-d*t).magnitude);
        }
        return nearest;
    }
    static void Capture(float station,string name,bool elevated)
    {
        Sample(station,out var point,out var forward); var right=Vector3.Cross(Vector3.up,forward).normalized;
        var go=new GameObject("Temporary dressing preview",typeof(Camera)); go.hideFlags=HideFlags.HideAndDontSave;
        var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=900;camera.fieldOfView=60;camera.useOcclusionCulling=false;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
        go.transform.position=point-forward*(elevated?35:8)+right*(elevated?22:0)+Vector3.up*(elevated?18:3);
        go.transform.LookAt(point+forward*35+Vector3.up*2);
        var rt=new RenderTexture(1440,810,24); var old=RenderTexture.active;camera.targetTexture=rt;
        Texture2D image=null;
        try { camera.Render();RenderTexture.active=rt;image=new Texture2D(1440,810,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,810),0,0);image.Apply();File.WriteAllBytes("Logs/Circuit01Dressing/"+name+".png",image.EncodeToPNG()); }
        finally { RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);if(image!=null)Object.DestroyImmediate(image);Object.DestroyImmediate(go); }
    }
}
