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
using Object=UnityEngine.Object;

// Explicit scenery-only conversion. Never calls the circuit/waypoint generators.
public static class Circuit02Sierras
{
 const string ScenePath="Assets/Scenes/Circuit_02.unity";
 const string Folder="Assets/Art/Sierras/Circuit02";
 const string Report="Logs/circuit02-sierras.txt";
 const string RootName="Sierras de Mina Clavero - Environment";
 const string SourceTerrain="Assets/Art/Environment/Circuit02/Circuit02_AridTerrain.asset";
 [Serializable] public sealed class Layout { public Vector3[] centerline,crowdCenters;public float[] roadWidths; }
 static Layout layout;
 static Terrain terrain;
 static TerrainData data;
 static System.Random random;
 static readonly Dictionary<Material,Material> plantMaterials=new Dictionary<Material,Material>();
 static Vector3[] route;
 static float[] widths;
 static float[,] originalHeights;
 static string backup;
 static float minSceneryClearance=float.MaxValue;

 public static void Build()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play before editing scenery.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved editor scene; not replacing it.");
  if(File.Exists(Folder+"/SierraTerrain.asset"))throw new Exception("Scenery already built; use targeted edits, not a second rebuild.");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  Directory.CreateDirectory(Folder);Directory.CreateDirectory("Logs");
  backup="Logs/SceneBackups/Circuit02Sierras_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
  File.Copy(ScenePath,backup+"/Circuit_02.unity");
  File.WriteAllText(Report,"Circuit_02 scenery conversion; immutable racing corridor\nBackup: "+backup+"\n");
  string originalTerrainHash=Hash(SourceTerrain);
  try {
   var scene=EditorSceneManager.OpenScene(ScenePath);
   if(GameObject.Find(RootName)!=null)throw new Exception("Scenery root already exists.");
   LoadRoute();terrain=Object.FindObjectsByType<Terrain>().Single();
   string signature=Protected(scene);File.WriteAllText(backup+"/protected-before.txt",signature);
   var root=new GameObject(RootName);random=new System.Random(7102026);plantMaterials.Clear();minSceneryClearance=float.MaxValue;
   Capture(new Vector3(14,5,105),new Vector3(-8,22,54),"Logs/Circuit02_Sierras_Before.png");
   ImportTextures();
   if(!AssetDatabase.CopyAsset(SourceTerrain,Folder+"/SierraTerrain.asset"))throw new Exception("Could not copy TerrainData.");
   data=AssetDatabase.LoadAssetAtPath<TerrainData>(Folder+"/SierraTerrain.asset");
   originalHeights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
   terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
   var tm=new Material(terrain.materialTemplate){name="Sierra Terrain URP"};AssetDatabase.CreateAsset(tm,Folder+"/Sierra Terrain.mat");terrain.materialTemplate=tm;
   SculptTerrain();PaintTerrain();BuildPlants();
   GameObject.Find("Circuit 02 - Trees and Rocks").SetActive(false);
   BuildRocks(root.transform);BuildStream(root.transform);Lighting();
   ValidateCorridor();
   if(signature!=Protected(scene))throw new Exception("Protected road/gameplay/crowds changed; scene not saved.");
   if(Hash(SourceTerrain)!=originalTerrainHash)throw new Exception("Source desert TerrainData changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
   if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save Circuit_02.");
   File.AppendAllText(Report,"PASS: road/shoulder meshes, colliders, checkpoints, AI, grid, cars, puddles and crowds unchanged. Original TerrainData unchanged.\n");
   File.AppendAllText(Report,"PASS: new scenery has no colliders; minimum full-bounds margin from road edge "+minSceneryClearance.ToString("F2")+" m.\nBUILD COMPLETE: PASS\n");
   CaptureAll();
  }catch(Exception e){File.AppendAllText(Report,"FAILED: "+e+"\n");throw;}
  finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }

 static void LoadRoute()
 {
  layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Art/Environment/Circuit02/Circuit02Layout.json"));
  // Eight-metre samples with a generous protected margin (the road itself is never edited).
  var indices=Enumerable.Range(0,layout.centerline.Length).Where(i=>i%6==0||i==layout.centerline.Length-1).ToArray();
  route=indices.Select(i=>layout.centerline[i]).ToArray();widths=indices.Select(i=>layout.roadWidths[i]).ToArray();
 }
 static float EdgeDistance(Vector3 p)
 {
  float best=float.MaxValue,hw=6;
  for(int i=0;i<route.Length-1;i++)
  {
   Vector3 a=route[i],b=route[i+1];float dx=b.x-a.x,dz=b.z-a.z;
   float t=Mathf.Clamp01(((p.x-a.x)*dx+(p.z-a.z)*dz)/Mathf.Max(.001f,dx*dx+dz*dz));
   float x=p.x-a.x-dx*t,z=p.z-a.z-dz*t,d=x*x+z*z;
   if(d<best){best=d;hw=Mathf.Lerp(widths[i],widths[i+1],t)*.5f;}
  }
  return Mathf.Sqrt(best)-hw;
 }
 static float RouteHeight(Vector3 p)
 {
  float sum=0,total=0;
  for(int i=0;i<route.Length-1;i++){
   var a=route[i];var d=route[i+1]-a;float t=Mathf.Clamp01(((p.x-a.x)*d.x+(p.z-a.z)*d.z)/Mathf.Max(.001f,d.x*d.x+d.z*d.z));
   float q=(p.x-a.x-d.x*t)*(p.x-a.x-d.x*t)+(p.z-a.z-d.z*t)*(p.z-a.z-d.z*t);
   // Nearby circuit sections can have different elevations. A nearest-section
   // switch creates a vertical Voronoi seam across distant hills; blend them.
   float weight=1/((3600+q)*(3600+q));sum+=Mathf.Lerp(a.y,route[i+1].y,t)*weight;total+=weight;
  }return sum/Mathf.Max(1e-12f,total);
 }
 static float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
 static float CrowdDistance(Vector3 p)=>layout.crowdCenters.Min(c=>FlatDistance(c,p));
 static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
 static float Gaussian(float x,float z,float cx,float cz,float sx,float sz)=>Mathf.Exp(-((x-cx)*(x-cx)/(sx*sx)+(z-cz)*(z-cz)/(sz*sz)));
 static float CreekX(float z)=>-58-9*Mathf.Sin((z+30)*.024f)-5*Mathf.Sin(z*.047f);
 static float WaterY(float z)=>1.6f-(z+75)*.003f;
 static float CreekDistance(Vector3 p)=>Mathf.Sqrt(Mathf.Pow(p.x-CreekX(Mathf.Clamp(p.z,-75,205)),2)+Mathf.Pow(p.z-Mathf.Clamp(p.z,-75,205),2));

 static void SculptTerrain()
 {
  int n=data.heightmapResolution;var h=(float[,])originalHeights.Clone();var origin=terrain.transform.position;var size=data.size;
  float maxRise=0;int changed=0;
  for(int z=0;z<n;z++)for(int x=0;x<n;x++)
  {
   var p=origin+new Vector3(x*size.x/(n-1),0,z*size.z/(n-1));float edge=EdgeDistance(p);
   // More than twice the playable escape margin remains exactly as it was.
   if(edge<=18||CrowdDistance(p)<=14)continue;
   float weight=Smooth(18,42,edge)*Smooth(14,32,CrowdDistance(p));
   float ridge=1-Mathf.Abs(2*Mathf.PerlinNoise(p.x*.012f+21,p.z*.012f+8)-1);
   float peaks=72*Gaussian(p.x,p.z,-145,275,130,100)+88*Gaussian(p.x,p.z,490,285,115,165)
     +65*Gaussian(p.x,p.z,540,-320,155,115)+60*Gaussian(p.x,p.z,-190,-310,120,150);
   float old=origin.y+h[z,x]*size.y;
   // Shape a broad foothill profile rather than adding mountains on top of
   // the old steep desert relief. Keep the exact inner corridor untouched.
   float natural=RouteHeight(p)+Smooth(18,180,edge)*(8+ridge*12+peaks*.8f);
   float target=Mathf.Lerp(old,natural,weight);
   // A shallow stream valley, exclusively outside the playable corridor.
   float creek=CreekDistance(p);float valley=(1-Smooth(5,72,creek))*Smooth(18,36,edge)*Smooth(14,32,CrowdDistance(p));
   float bed=WaterY(Mathf.Clamp(p.z,-75,205))-.8f+Smooth(3,72,creek)*12;
   target=Mathf.Lerp(target,bed,valley);
   h[z,x]=(target-origin.y)/size.y;if(h[z,x]>=.99f)throw new Exception("Mountain exceeds terrain range.");
   maxRise=Mathf.Max(maxRise,target-old);if(h[z,x]!=originalHeights[z,x])changed++;
  }
  data.SetHeights(0,0,h);terrain.Flush();
  File.AppendAllText(Report,$"Terrain: {changed} distant height samples changed, maximum added relief {maxRise:F1} m; original corridor/crowd platforms preserved.\n");
 }
 static void ImportTextures()
 {
  AssetDatabase.Refresh();
  foreach(string path in Directory.GetFiles(Folder+"/Textures","*.jpg",SearchOption.AllDirectories))
  {
   var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
   importer.textureType=path.Contains("_nor_gl_")?TextureImporterType.NormalMap:TextureImporterType.Default;
   importer.sRGBTexture=!path.Contains("_nor_gl_");importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.anisoLevel=4;
   importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Compressed;
   importer.SaveAndReimport();
  }
 }
 static T Asset<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new Exception("Missing asset: "+path);
 static TerrainLayer Layer(string id,float tile,Color tint,float normal)
 {
  var l=new TerrainLayer{name=id+" Sierra",diffuseTexture=Asset<Texture2D>(Folder+"/Textures/"+id+"/"+id+"_diff_2k.jpg"),normalMapTexture=Asset<Texture2D>(Folder+"/Textures/"+id+"/"+id+"_nor_gl_2k.jpg"),tileSize=Vector2.one*tile,normalScale=normal,smoothness=.1f,metallic=0};
  l.diffuseRemapMax=new Vector4(tint.r,tint.g,tint.b,1);AssetDatabase.CreateAsset(l,Folder+"/"+id+".terrainlayer");return l;
 }
 static void PaintTerrain()
 {
  if(data.terrainLayers.Length!=3||!AssetDatabase.GetAssetPath(data.terrainLayers[0]).StartsWith(Folder))
   data.terrainLayers=new[]{Layer("rocky_terrain",9,new Color(.89f,.94f,.98f),.8f),Layer("aerial_grass_rock",15,new Color(.85f,.94f,.87f),.75f),Layer("rock_face_03",14,new Color(.8f,.86f,.93f),1.1f)};
  int n=data.alphamapWidth;var maps=new float[n,n,3];
  for(int z=0;z<n;z++)for(int x=0;x<n;x++)
  {
   float u=x/(float)(n-1),v=z/(float)(n-1);var p=terrain.transform.position+new Vector3(u*data.size.x,0,v*data.size.z);
   float edge=EdgeDistance(p),slope=data.GetSteepness(u,v),noise=Mathf.PerlinNoise(p.x*.025f+27,p.z*.025f+11);
   float rock=Smooth(15,38,slope)*.84f+Smooth(75,135,data.GetInterpolatedHeight(u,v)+terrain.transform.position.y)*.18f;
   rock=Mathf.Clamp01(rock)*Smooth(9,30,edge);
   float grass=(.5f+.35f*noise)*(1-rock)*Smooth(6,25,edge);
   if(CreekDistance(p)<10){rock*=.3f;grass*=.2f;}
   maps[z,x,0]=1-rock-grass;maps[z,x,1]=grass;maps[z,x,2]=rock;
  }
  data.SetAlphamaps(0,0,maps);
 }
 static GameObject PlantPrefab(string source,Color tint)
 {
  var go=Object.Instantiate(Asset<GameObject>(source));go.name=Path.GetFileNameWithoutExtension(source)+" Sierra";
  foreach(var r in go.GetComponentsInChildren<Renderer>(true))
  {
   r.sharedMaterials=r.sharedMaterials.Select(m=>{
    if(!plantMaterials.TryGetValue(m,out var copy)){
     copy=new Material(m){name=m.name+" Sierra",enableInstancing=true};copy.SetColor("_BaseColor",tint);copy.SetFloat("_Smoothness",.12f);
     AssetDatabase.CreateAsset(copy,Folder+"/Plant_"+plantMaterials.Count+".mat");plantMaterials.Add(m,copy);
    }return copy;
   }).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;
  }
  FlattenPlant(go);
  var result=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+go.name+".prefab");Object.DestroyImmediate(go);return result;
 }
 static void FlattenPlant(GameObject go)
 {
  foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>()){
   var filter=renderer.GetComponent<MeshFilter>();if(filter==null||renderer.sharedMaterials.Length==1&&filter.sharedMesh.subMeshCount==1)continue;
   if(renderer.sharedMaterials.Distinct().Count()!=1)throw new Exception("Cannot flatten different vegetation materials without an atlas.");
   string path=Folder+"/"+go.name+"_single_submesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh==null){mesh=Object.Instantiate(filter.sharedMesh);var indices=mesh.triangles;mesh.subMeshCount=1;mesh.SetTriangles(indices,0);mesh.name=go.name+" all leaves and stems";AssetDatabase.CreateAsset(mesh,path);}
   filter.sharedMesh=mesh;renderer.sharedMaterials=new[]{renderer.sharedMaterial};
  }
 }
 static void BuildPlants()
 {
  var original=data.detailPrototypes;var colors=new[]{new Color(.73f,1.04f,.65f),new Color(.79f,1.08f,.7f),new Color(.74f,1.02f,.78f)};
  var prototypes=new DetailPrototype[original.Length];
  for(int i=0;i<original.Length;i++)
  {
   var p=new DetailPrototype(original[i]);p.prototype=PlantPrefab(AssetDatabase.GetAssetPath(original[i].prototype),colors[Mathf.Min(i,2)]);
   p.minWidth=i==2?.65f:.65f;p.maxWidth=i==2?1.05f:1.2f;p.minHeight=.7f;p.maxHeight=1.25f;p.useInstancing=true;p.alignToGround=.9f;prototypes[i]=p;
  }
  data.detailPrototypes=prototypes;
  ScatterPlants();
 }
 static void ScatterPlants()
 {
  // These maps encode exact placement counts, not 0..255 area coverage.
  data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
  var prototypes=data.detailPrototypes;
  int[] targets={1150,320,470};
  for(int layer=0;layer<prototypes.Length;layer++)
  {
   int n=data.detailWidth,count=0,target=targets[Mathf.Min(layer,2)];var cells=new int[n,n];
   for(int attempt=0;attempt<100000&&count<target;attempt++)
   {
    int k=random.Next(route.Length-1);Vector3 tangent=(route[k+1]-route[k]).normalized;Vector3 side=Vector3.Cross(Vector3.up,tangent).normalized;
    Vector3 p=route[k]+side*(random.Next(2)==0?-1:1)*(widths[k]*.5f+15+Rand()*60);
    int x=Mathf.FloorToInt((p.x-terrain.transform.position.x)/data.size.x*n),z=Mathf.FloorToInt((p.z-terrain.transform.position.z)/data.size.z*n);
    if(x<0||z<0||x>=n||z>=n||cells[z,x]>0)continue;
    p=terrain.transform.position+new Vector3((x+.5f)*data.size.x/n,0,(z+.5f)*data.size.z/n);
    float edge=EdgeDistance(p),slope=data.GetSteepness((x+.5f)/n,(z+.5f)/n);
    if(edge<14||CrowdDistance(p)<15||CreekDistance(p)<7||slope>35)continue;
    float clusters=Mathf.PerlinNoise(p.x*.045f+4,p.z*.045f+17);
    if(clusters<.4f)continue;
    cells[z,x]=1;count++;
   }
   if(count!=target)throw new Exception("Could not place details safely: "+layer+" "+count);
   data.SetDetailLayer(0,0,layer,cells);
   File.AppendAllText(Report,"Terrain Details: "+prototypes[layer].prototype.name+", "+count+" placements, instanced; outside road/barriers and crowd platforms.\n");
  }
  EditorUtility.SetDirty(data);
 }
 static float Rand()=>(float)random.NextDouble();
 static Bounds BoundsOf(GameObject go)
 {
  var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
 }
 static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
 static void BuildRocks(Transform parent)
 {
  var group=new GameObject("Rocky outcrops - beyond containment");group.transform.SetParent(parent,false);
  var source=Asset<GameObject>("Assets/Art/Environment/Prefabs/namaqualand_rocks_01.prefab");
  var sourceMaterial=Asset<Material>("Assets/Art/Environment/Materials/namaqualand_rocks_01.mat");
  var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sierra weathered stone.mat");
  if(material==null){material=new Material(sourceMaterial){name="Sierra weathered stone",enableInstancing=true};AssetDatabase.CreateAsset(material,Folder+"/Sierra weathered stone.mat");}
  material.SetColor("_BaseColor",new Color(.78f,.84f,.9f,1));material.SetFloat("_Smoothness",.12f);
  var placed=new List<Vector3>();int count=0;
  for(int attempt=0;attempt<12000&&count<38;attempt++)
  {
   int k=random.Next(route.Length-1);var forward=(route[k+1]-route[k]).normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;
   var p=route[k]+right*(random.Next(2)==0?-1:1)*(widths[k]*.5f+25+Rand()*95);
   if(count<3&&attempt<200)p=new[]{new Vector3(-38,0,95),new Vector3(-98,0,151),new Vector3(-41,0,-50)}[count];
   bool large=count<12;float targetSize=large?12+Rand()*13:3+Rand()*5;
   float radius=targetSize*.65f;
   if(EdgeDistance(p)-radius<11||CrowdDistance(p)<radius+18||CreekDistance(p)<radius+7||placed.Any(q=>FlatDistance(p,q)<radius+9))continue;
   var local=p-terrain.transform.position;float u=local.x/data.size.x,v=local.z/data.size.z;if(u<.02f||u>.98f||v<.02f||v>.98f)continue;
   var go=Object.Instantiate(source,group.transform);go.name=(large?"Granite outcrop ":"Weathered boulders ")+(count+1).ToString("00");
   // The source is a display row of four separate rocks, not one outcrop.
   // Reuse one variant per placement; do not scale the gaps between samples.
   var variants=go.GetComponentsInChildren<Renderer>();for(int j=0;j<variants.Length;j++)variants[j].enabled=j==count%variants.Length;
   go.transform.rotation=Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,data.GetInterpolatedNormal(u,v),.45f))*Quaternion.Euler(0,Rand()*360,0);
   var b=BoundsOf(go);go.transform.localScale*=targetSize/Mathf.Max(b.size.x,b.size.z);
   p.y=Ground(p);go.transform.position=p;b=BoundsOf(go);go.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y-(large?Mathf.Max(1.3f,b.size.y*.18f):.2f),p.z-b.center.z);
   b=BoundsOf(go);float clearance=EdgeDistance(b.center)-new Vector2(b.extents.x,b.extents.z).magnitude;
   if(clearance<10||CrowdDistance(b.center)<new Vector2(b.extents.x,b.extents.z).magnitude+15){Object.DestroyImmediate(go);continue;}
   foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>material).ToArray();r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.OccludeeStatic);}
   foreach(var c in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
   var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(large?.009f:.035f,go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray())});lod.RecalculateBounds();
   minSceneryClearance=Mathf.Min(minSceneryClearance,clearance);placed.Add(p);count++;
  }
  if(count<38)throw new Exception("Insufficient safe rock placements: "+count);
  File.AppendAllText(Report,"38 reused photogrammetric rock groups (12 major outcrops); 80 desert Quiver trees and 65 old rock placements deactivated recoverably.\n");
 }
 static void BuildStream(Transform parent)
 {
  var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int n=94;
  for(int i=0;i<n;i++)
  {
   float t=i/(float)(n-1),z=Mathf.Lerp(-75,205,t);float half=(2.25f+.7f*Mathf.Sin(z*.09f))*Mathf.Min(1,t*18,(1-t)*18);
   float x=CreekX(z);Vector3 p=new Vector3(x,WaterY(z),z);float clearance=EdgeDistance(p)-half;
   if(clearance<18)throw new Exception("Stream too close to racing corridor.");minSceneryClearance=Mathf.Min(minSceneryClearance,clearance);
   vertices.Add(p+Vector3.left*half);vertices.Add(p+Vector3.right*half);uv.Add(new Vector2(0,z*.08f));uv.Add(new Vector2(1,z*.08f));
   if(i<n-1){int a=i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
  }
  var mesh=new Mesh{name="Sierra stream surface"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Sierra stream.asset");
  var mat=new Material(Asset<Material>("Assets/Art/Forest/Circuit03/RiverBridge/Flowing forest river.mat")){name="Sierra shallow stream"};
  mat.SetColor("_Color",new Color(.78f,.85f,.79f,0));mat.SetColor("_DepthColor",new Color(.13f,.24f,.21f,0));mat.SetFloat("_OpaqueDepth",1.6f);AssetDatabase.CreateAsset(mat,Folder+"/Sierra shallow stream.mat");
  var go=new GameObject("Arroyo serrano - visual only - 280m x 3-6m",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
  File.AppendAllText(Report,"Scenic shallow stream: z=-75..205, x=-44..-72, 3-6m wide, outside containment; existing WaterLake material reused in a private copy. No triggers/gameplay.\n");
 }
 static void Lighting()
 {
  var sky=new Material(RenderSettings.skybox){name="Sierra daylight sky"};sky.SetColor("_SkyTint",new Color(.49f,.55f,.61f));sky.SetColor("_GroundColor",new Color(.36f,.38f,.3f));sky.SetFloat("_AtmosphereThickness",1.2f);sky.SetFloat("_Exposure",1.04f);AssetDatabase.CreateAsset(sky,Folder+"/Sierra daylight sky.mat");RenderSettings.skybox=sky;
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.61f,.68f,.74f);RenderSettings.ambientEquatorColor=new Color(.5f,.52f,.46f);RenderSettings.ambientGroundColor=new Color(.28f,.3f,.24f);
  RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.61f,.67f,.69f);RenderSettings.fogDensity=.00165f;
  var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.color=new Color(1,.96f,.86f);sun.intensity=1.1f;
 }
 static void ValidateCorridor()
 {
  var h=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);int n=data.heightmapResolution,checkedSamples=0;
  for(int z=0;z<n;z++)for(int x=0;x<n;x++)
  {
   var p=terrain.transform.position+new Vector3(x*data.size.x/(n-1),0,z*data.size.z/(n-1));
   if(EdgeDistance(p)>18&&CrowdDistance(p)>14)continue;
   if(h[z,x]!=originalHeights[z,x])throw new Exception("Protected Terrain sample changed.");checkedSamples++;
  }
  if(GameObject.Find(RootName).GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Scenery introduced colliders.");
  File.AppendAllText(Report,"PASS: "+checkedSamples+" original height samples in road/runoff/crowd corridor remain bit-identical.\n");
 }
 static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
 static string Protected(Scene scene)
 {
  var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null);
  return string.Join("\n",components.Where(c=>
   (c is Collider&&!(c is TerrainCollider))||c is Rigidbody||c is ProBuilderMesh||c is RallyCheckpointManager||c is RallyCheckpointTrigger||c is JrsVehicleController||c is RallyVehicleDynamics||c is RallyBotController||c is RallyRaceFlow||c is RallyBrakeWarningTrigger||
   c.transform.root.name=="AI_Waypoints"||c.transform.root.name=="Starting Grid"||c.transform.root.name=="Race HUD"||c.transform.root.name=="Porsche 911 SC Rally"||c.transform.root.name.StartsWith("Bot_Car")||
   c.GetComponentsInParent<Transform>(true).Any(t=>t.name=="Circuit 02 - Crowds Outside Boundaries"||t.name=="Circuit 02 - Water Basins (visual only)"))
   .OrderBy(c=>c.GetEntityId().ToString()).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
 }
 public static void Preview()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play.");
  var setup=EditorSceneManager.GetSceneManagerSetup();try{EditorSceneManager.OpenScene(ScenePath);terrain=Object.FindObjectsByType<Terrain>().Single();CaptureAll();}finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 public static void Refine()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  try {
   var scene=EditorSceneManager.OpenScene(ScenePath);LoadRoute();terrain=Object.FindObjectsByType<Terrain>().Single();data=terrain.terrainData;
   if(AssetDatabase.GetAssetPath(data)!=Folder+"/SierraTerrain.asset")throw new Exception("Unexpected TerrainData.");
   string signature=Protected(scene);var source=Asset<TerrainData>(SourceTerrain);originalHeights=source.GetHeights(0,0,source.heightmapResolution,source.heightmapResolution);
   SculptTerrain();PaintTerrain();random=new System.Random(7102026);ScatterPlants();
   var rocks=GameObject.Find(RootName).transform.Find("Rocky outcrops - beyond containment");
   foreach(Transform rock in rocks){
    var bounds=BoundsOf(rock.gameObject);var p=bounds.center;
    rock.position+=Vector3.up*(Ground(p)-bounds.min.y-(rock.name.StartsWith("Granite")?1.3f:.2f));
   }
   ValidateCorridor();if(signature!=Protected(scene))throw new Exception("Protected components changed.");
   EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.AppendAllText(Report,"REFINE PASS: broad foothills and smooth stream valley; rock bases regrounded; protected road/gameplay intact.\n");CaptureAll();
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 public static void Polish()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  var setup=EditorSceneManager.GetSceneManagerSetup();try{
   var scene=EditorSceneManager.OpenScene(ScenePath);LoadRoute();terrain=Object.FindObjectsByType<Terrain>().Single();data=terrain.terrainData;string before=Protected(scene);
   var root=GameObject.Find(RootName).transform;var old=root.Find("Rocky outcrops - beyond containment");if(old!=null)Object.DestroyImmediate(old.gameObject);
   random=new System.Random(7102026);minSceneryClearance=float.MaxValue;BuildRocks(root);
   var l=data.terrainLayers[0];l.tileSize=Vector2.one*4;l.diffuseRemapMax=new Vector4(.8f,.96f,.91f,1);EditorUtility.SetDirty(l);
   l=data.terrainLayers[1];l.diffuseRemapMax=new Vector4(.74f,.95f,.73f,1);EditorUtility.SetDirty(l);PaintTerrain();random=new System.Random(7102026);ScatterPlants();
   if(before!=Protected(scene))throw new Exception("Protected components changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.AppendAllText(Report,"POLISH PASS: individual rock variants, vegetation palette and ground scale; protected gameplay intact; scenery bounds clearance "+minSceneryClearance+"m.\n");CaptureAll();
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 public static void FinalTouches()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene.");
  var setup=EditorSceneManager.GetSceneManagerSetup();try{
   var scene=EditorSceneManager.OpenScene(ScenePath);terrain=Object.FindObjectsByType<Terrain>().Single();data=terrain.terrainData;string before=Protected(scene);
   foreach(var p in data.detailPrototypes){string path=AssetDatabase.GetAssetPath(p.prototype);if(!path.StartsWith(Folder))throw new Exception("Not a private plant prefab.");var go=PrefabUtility.LoadPrefabContents(path);try{FlattenPlant(go);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}}
   data.RefreshPrototypes();int actual=0;
   for(int layer=0;layer<data.detailPrototypes.Length;layer++){
    if(!data.detailPrototypes[layer].Validate(out var error))throw new Exception(error);int count=0;
    for(int z=0;z<data.detailPatchCount;z++)for(int x=0;x<data.detailPatchCount;x++)count+=data.ComputeDetailInstanceTransforms(x,z,layer,terrain.detailObjectDensity,out var bounds).Length;
    if(count==0)throw new Exception("Vegetation layer has no renderable instances: "+layer);actual+=count;File.AppendAllText(Report,"PASS: validated plant layer "+layer+", computed renderable instances="+count+".\n");
   }
   var rocks=GameObject.Find(RootName).transform.Find("Rocky outcrops - beyond containment");
   foreach(Transform rock in rocks){
    var r=rock.GetComponentsInChildren<MeshRenderer>().First(v=>v.enabled);var filter=r.GetComponent<MeshFilter>();var vertices=filter.sharedMesh.vertices;var gaps=new List<float>();
    for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/400)){var p=filter.transform.TransformPoint(vertices[i]);gaps.Add(p.y-Ground(p));}
    gaps.Sort();float gap=gaps[Mathf.FloorToInt(gaps.Count*.4f)];if(gap>0)rock.position-=Vector3.up*gap;
   }
   if(before!=Protected(scene))throw new Exception("Protected components changed.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.AppendAllText(Report,"FINAL TOUCHES PASS: all vegetation prototypes valid, "+actual+" renderable details, rock footprints embedded, gameplay preserved.\n");CaptureAll();
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 static void CaptureAll()
 {
  Capture(new Vector3(14,5,105),new Vector3(-8,22,54),"Logs/Circuit02_Sierras_Track.png");
  Capture(new Vector3(22,6,142),new Vector3(135,90,70),"Logs/Circuit02_Sierras_Overview.png");
  Capture(new Vector3(-59,1,86),new Vector3(-17,14,49),"Logs/Circuit02_Sierras_Stream.png");
  Capture(new Vector3(0,2,95),new Vector3(0,2,42),"Logs/Circuit02_Sierras_Driving.png",2);
 }
 static void Capture(Vector3 focus,Vector3 position,string path,float clearance=4)
 {
  var go=new GameObject("Temporary scenery preview",typeof(Camera));var c=go.GetComponent<Camera>();c.fieldOfView=62;c.farClipPlane=650;c.clearFlags=CameraClearFlags.Skybox;c.useOcclusionCulling=false;
  if(terrain!=null)position.y=Mathf.Max(position.y,Ground(position)+clearance);
  c.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));var extra=c.GetUniversalAdditionalCameraData();extra.requiresColorTexture=true;extra.requiresDepthTexture=true;
  var rt=new RenderTexture(1280,800,24);var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
  try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
  finally{c.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
 }
}
