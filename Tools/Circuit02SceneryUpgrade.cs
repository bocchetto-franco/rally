using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using MeshUtility=UnityEditor.MeshUtility;

// Saved, scenery-only authoring pass. No runtime component or track generator.
public static class Circuit02SceneryUpgrade
{
 const string ScenePath="Assets/Scenes/Circuit_02.unity";
 const string Source="Assets/Art/Sierras/Circuit02";
 const string Folder=Source+"/SceneryUpgrade";
 const string RootName="Circuit 02 - Sierra Landscape Detail";
 const string Log="Logs/Circuit02SceneryUpgrade";
 [Serializable] public class Layout { public Vector3[] centerline,crowdCenters; public float[] roadWidths; }
 static Layout layout; static Vector3[] route; static float[] widths,lengths;
 static Terrain terrain; static TerrainData data; static Transform root;
 static System.Random random; static StringBuilder report;
 static readonly List<Vector3> plants=new List<Vector3>();
 static readonly List<Bounds> occupied=new List<Bounds>();
 static float minimumMargin=10000;
 static float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
 static T Asset<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new Exception("Missing "+path);
 static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
 static float Flat(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
 static float Crowd(Vector3 p)=>layout.crowdCenters.Min(c=>Flat(p,c));
 static float Creek(Vector3 p){float z=Mathf.Clamp(p.z,-75,205),x=-58-9*Mathf.Sin((z+30)*.024f)-5*Mathf.Sin(z*.047f);return new Vector2(p.x-x,p.z-z).magnitude;}
 static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
 static Vector3 Normal(Vector3 p){var q=p-terrain.transform.position;return data.GetInterpolatedNormal(q.x/data.size.x,q.z/data.size.z);}
 static void Idle(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||StaticOcclusionCulling.isRunning)throw new Exception("Editor busy.");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene; refusing to discard.");}
 static void Read()
 {
  layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Art/Environment/Circuit02/Circuit02Layout.json"));
  var ids=Enumerable.Range(0,layout.centerline.Length).Where(i=>i%6==0||i==layout.centerline.Length-1).ToArray();
  route=ids.Select(i=>layout.centerline[i]).ToArray();widths=ids.Select(i=>layout.roadWidths[i]).ToArray();lengths=new float[route.Length];
  for(int i=1;i<route.Length;i++)lengths[i]=lengths[i-1]+Vector3.Distance(route[i-1],route[i]);
  terrain=Object.FindObjectsByType<Terrain>().Single();data=terrain.terrainData;
 }
 static float Edge(Vector3 p)
 {
  float best=float.MaxValue,half=6;
  for(int i=0;i<route.Length-1;i++){var a=route[i];var d=route[i+1]-a;float t=Mathf.Clamp01(((p.x-a.x)*d.x+(p.z-a.z)*d.z)/Mathf.Max(.001f,d.x*d.x+d.z*d.z));float x=p.x-a.x-d.x*t,z=p.z-a.z-d.z*t,q=x*x+z*z;if(q<best){best=q;half=Mathf.Lerp(widths[i],widths[i+1],t)*.5f;}}
  return Mathf.Sqrt(best)-half;
 }
 static Vector3 Sample(float s,out Vector3 tangent)
 {
  s=Mathf.Repeat(s,lengths[lengths.Length-1]);int i=0;while(i<lengths.Length-2&&lengths[i+1]<s)i++;
  tangent=(route[i+1]-route[i]).normalized;return Vector3.Lerp(route[i],route[i+1],Mathf.InverseLerp(lengths[i],lengths[i+1],s));
 }
 static bool Safe(Vector3 p,float radius,float slope=32)
 {
  var q=p-terrain.transform.position;if(q.x<radius+3||q.z<radius+3||q.x>data.size.x-radius-3||q.z>data.size.z-radius-3)return false;
  if(Edge(p)<10+radius||Crowd(p)<16+radius||Creek(p)<7+radius||Vector3.Angle(Normal(p),Vector3.up)>slope)return false;
  foreach(var b in occupied){float x=Mathf.Max(b.min.x-p.x,0,p.x-b.max.x),z=Mathf.Max(b.min.z-p.z,0,p.z-b.max.z);if(x*x+z*z<(radius+1)*(radius+1))return false;}
  return true;
 }
 static string Protected(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&
  ((c is Collider&&!(c is TerrainCollider))||c is Rigidbody||c is ProBuilderMesh||c is MonoBehaviour||
  c.transform.root.name=="AI_Waypoints"||c.transform.root.name=="Starting Grid"||c.transform.root.name=="Race HUD"||c.transform.root.name=="Porsche 911 SC Rally"||c.transform.root.name.StartsWith("Bot_Car")||
  c.GetComponentsInParent<Transform>(true).Any(t=>t.name=="Circuit 02 - Crowds Outside Boundaries"||t.name=="Circuit 02 - Water Basins (visual only)")))
  .OrderBy(c=>c.GetEntityId().ToString()).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
 public static void Build()
 {
  Idle();if(File.Exists(Folder+"/SierraTerrainDetailed.asset"))throw new Exception("Already authored; do not duplicate.");
  var setup=EditorSceneManager.GetSceneManagerSetup();Directory.CreateDirectory(Log);Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
  string backup="Logs/SceneBackups/Circuit02Scenery_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(ScenePath,backup+"/Circuit_02.unity");File.WriteAllText(Log+"/baseline.txt",backup+"/Circuit_02.unity");
  report=new StringBuilder("Circuit_02 scenery upgrade\nBackup: "+backup+"\n");random=new System.Random(81026);plants.Clear();occupied.Clear();minimumMargin=10000;
  try {
   var scene=EditorSceneManager.OpenScene(ScenePath);Read();string before=Protected(scene);
   if(GameObject.Find(RootName)!=null)throw new Exception("Duplicate root.");
   if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(data),Folder+"/SierraTerrainDetailed.asset"))throw new Exception("Terrain copy failed.");
   data=Asset<TerrainData>(Folder+"/SierraTerrainDetailed.asset");terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
   root=new GameObject(RootName).transform;
   Step("relief",Sculpt);Step("textures",Paint);Step("existing rocks",RegroundOldRocks);Step("new rocks",Rocks);Step("shrub patches",Shrubs);Step("instanced grasses",Grass);Step("isolated trees",Trees);Step("lighting",Lighting);
   terrain.heightmapPixelError=7;terrain.basemapDistance=180; // local visual precision, no global quality changes
   terrain.Flush();EditorUtility.SetDirty(data);
   if(Protected(scene)!=before)throw new Exception("Protected physics/route/gameplay changed; refusing scene save.");
   if(root.GetComponentsInChildren<Collider>().Length!=0||root.GetComponentsInChildren<MonoBehaviour>().Length!=0)throw new Exception("Scenery added gameplay components.");
   if(!GameObject.Find("Porsche 911 SC Rally").activeInHierarchy)throw new Exception("Player no longer active.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed.");
   report.AppendLine("PASS: original road, collider, checkpoint, waypoint, cars, grid, puddle and crowd components unchanged. Minimum full scenery bounds margin from road: "+minimumMargin.ToString("F2")+" m.");
   report.AppendLine("BUILD COMPLETE: PASS");
  }catch(Exception e){report.AppendLine("FAILED: "+e);throw;}
  finally{File.WriteAllText(Log+"/build.txt",report.ToString());EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 static void Step(string name,Action action){report.AppendLine("Starting "+name);File.WriteAllText(Log+"/build.txt",report.ToString());action();File.WriteAllText(Log+"/build.txt",report.ToString());}
 static void Sculpt()
 {
  int n=data.heightmapResolution;var old=data.GetHeights(0,0,n,n);var h=(float[,])old.Clone();var origin=terrain.transform.position;int changed=0,protectedCount=0;float maxDelta=0;
  for(int z=0;z<n;z++)for(int x=0;x<n;x++){
   var p=origin+new Vector3(x*data.size.x/(n-1),0,z*data.size.z/(n-1));float edge=Edge(p),crowd=Crowd(p),creek=Creek(p);
   if(edge<=18||crowd<=14||creek<=9){protectedCount++;continue;}
   float fade=Smooth(18,55,edge)*Smooth(14,35,crowd)*Smooth(9,38,creek);
   float warp=14*(Mathf.PerlinNoise(p.x*.008f+47,p.z*.008f+25)-.5f);
   float n1=Mathf.PerlinNoise((p.x+warp)*.016f+18,p.z*.012f+35),n2=Mathf.PerlinNoise(p.x*.034f+5,p.z*.027f+2);
   float ridge=1-Mathf.Abs(2*Mathf.PerlinNoise((p.x+warp)*.022f+4,p.z*.016f+9)-1);
   float detail=(n1-.46f)*13+(n2-.5f)*4+ridge*ridge*5;
   float distant=Smooth(45,145,edge)*ridge*ridge*13;
   float delta=fade*(detail+distant);h[z,x]=Mathf.Clamp01(old[z,x]+delta/data.size.y);maxDelta=Mathf.Max(maxDelta,Mathf.Abs(delta));changed++;
  }
  data.SetHeights(0,0,h);terrain.Flush();var actual=data.GetHeights(0,0,n,n);
  for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(h[z,x]==old[z,x]&&actual[z,x]!=old[z,x])throw new Exception("Protected Terrain heights changed.");
  report.AppendLine($"Terrain: {changed} outer samples shaped, max delta {maxDelta:F2}m; {protectedCount} road/runoff/crowd/stream samples bit-identical. Multi-scale foothills; original TerrainData untouched.");
 }
 static void Paint()
 {
  var source=data.terrainLayers;var layers=new TerrainLayer[4];float[] tiles={6.8f,12.7f,11.5f,19.7f};
  for(int i=0;i<4;i++){layers[i]=Object.Instantiate(source[i==3?0:i]);layers[i].name="Sierra detailed "+i;layers[i].tileSize=Vector2.one*tiles[i];layers[i].tileOffset=new Vector2(i*3.7f,i*5.3f);layers[i].normalScale=i==2?1.05f:.8f;layers[i].smoothness=.06f;AssetDatabase.CreateAsset(layers[i],Folder+"/Layer_"+i+".terrainlayer");}
  layers[0].diffuseRemapMax=new Vector4(.93f,.95f,.91f,1);layers[1].diffuseRemapMax=new Vector4(.86f,.91f,.74f,1);layers[2].diffuseRemapMax=new Vector4(.9f,.92f,.94f,1);layers[3].diffuseRemapMax=new Vector4(.78f,.83f,.76f,1);
  data.terrainLayers=layers;int n=data.alphamapWidth;var map=new float[n,n,4];
  for(int z=0;z<n;z++)for(int x=0;x<n;x++){
   float u=x/(float)(n-1),v=z/(float)(n-1);var p=terrain.transform.position+new Vector3(u*data.size.x,0,v*data.size.z);float edge=Edge(p),slope=data.GetSteepness(u,v);
   float macro=Mathf.PerlinNoise(p.x*.014f+8,p.z*.018f+7),small=Mathf.PerlinNoise(p.x*.075f+23,p.z*.069f+11);
   float rock=Smooth(12,35,slope)*(.48f+.4f*small)*Smooth(9,24,edge);
   float grass=Mathf.Lerp(.15f,.78f,Smooth(.27f,.72f,macro))*(1-rock)*Smooth(2.5f,20,edge);
   float gravel=(1-rock-grass)*(.18f+.6f*Mathf.PerlinNoise(p.x*.033f+9,p.z*.041f+33))*Smooth(1,17,edge);
   map[z,x,0]=1-rock-grass-gravel;map[z,x,1]=grass;map[z,x,2]=rock;map[z,x,3]=gravel;
  }data.SetAlphamaps(0,0,map);report.AppendLine("Four blended private PBR layers: rocky soil at two scales, scrub grass/rock and exposed cliff; shared 2K textures/normals, slope and multi-scale patch weighting.");
 }
 static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 static void RegroundOldRocks()
 {
  var old=GameObject.Find("Rocky outcrops - beyond containment");int count=0;
  foreach(Transform t in old.transform){var filters=t.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>().enabled).ToArray();var b=BoundsOf(t.gameObject);float y=Ground(b.center)-b.min.y-Mathf.Min(.9f,b.size.y*.17f);t.position+=Vector3.up*y;Embed(t.gameObject);occupied.Add(BoundsOf(t.gameObject));count++;}
  report.AppendLine("Regrounded "+count+" pre-existing rock placements on the new hills.");
 }
 static void Embed(GameObject go)
 {
  var distances=new List<float>();
  foreach(var f in go.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>().enabled)){
   var vertices=f.sharedMesh.vertices;float bottom=f.sharedMesh.bounds.min.y+f.sharedMesh.bounds.size.y*.16f;
   for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/600))if(vertices[i].y<=bottom){var p=f.transform.TransformPoint(vertices[i]);distances.Add(p.y-Ground(p));}
  }
  if(distances.Count>0){distances.Sort();go.transform.position-=Vector3.up*(distances[(int)(distances.Count*.65f)]+.08f);}
 }
 static void RendererSetup(Renderer r,bool shadow=false){r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.OccludeeStatic);}
 static void Rocks()
 {
  var mat=new Material(Asset<Material>(Source+"/Sierra weathered stone.mat")){name="Sierra granite - warm neutral",enableInstancing=true};mat.SetColor("_BaseColor",new Color(.74f,.77f,.73f));mat.SetFloat("_Smoothness",.09f);AssetDatabase.CreateAsset(mat,Folder+"/Materials/Granite.mat");
  int count=0,largeCount=0;
  for(int attempt=0;attempt<25000&&count<230;attempt++){
   bool large=count<26;float size=large?R(6,14):R(.65f,4.5f);float s=R(0,lengths.Last());var p=Sample(s,out var tangent);var right=Vector3.Cross(Vector3.up,tangent).normalized;
   p+=right*(random.Next(2)==0?-1:1)*R(large?32:20,large?105:66);float radius=size*.8f;
   if(!Safe(p,radius,large?32:26))continue;
   var mesh=Asset<Mesh>("Assets/Art/Environment/Circuit01Dressing/Meshes/Rock_"+(count%4)+".asset");
   var go=new GameObject((large?"Granite ridge ":"Scattered stone ")+count.ToString("000"),typeof(MeshFilter),typeof(MeshRenderer),typeof(LODGroup));go.transform.SetParent(root,false);
   go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
   p.y=Ground(p);go.transform.SetPositionAndRotation(p,Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,Normal(p),.7f))*Quaternion.Euler(0,R(0,360),0));go.transform.localScale=Vector3.one*(size/2.6f);Embed(go);
   var b=BoundsOf(go);float margin=Edge(b.center)-new Vector2(b.extents.x,b.extents.z).magnitude;if(margin<9.5f){Object.DestroyImmediate(go);continue;}
   minimumMargin=Mathf.Min(minimumMargin,margin);RendererSetup(go.GetComponent<Renderer>(),size>2.5f);
   if(large)GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.OccludeeStatic|StaticEditorFlags.OccluderStatic);
   var lod=go.GetComponent<LODGroup>();lod.SetLODs(new[]{new LOD(large?.006f:.012f,new[]{go.GetComponent<Renderer>()})});lod.RecalculateBounds();occupied.Add(b);count++;if(large)largeCount++;
  }
  if(count<180)throw new Exception("Insufficient safe rock placements: "+count);report.AppendLine(count+" new shared-mesh rocks, including "+largeCount+" ridge outcrops; slope-aligned and embedded, LOD and instanced material.");
 }
 static Renderer Combined(string name,List<CombineInstance> instances,Material material,Transform parent)
 {
  var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances.ToArray(),true,true);mesh.RecalculateBounds();MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Medium);AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+name+".asset");
  var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;RendererSetup(go.GetComponent<Renderer>());return go.GetComponent<Renderer>();
 }
 static void Shrubs()
 {
  int count=0,patches=0;
  for(float s=5;s<lengths.Last();s+=24)foreach(int side in new[]{-1,1}){
   var p=Sample(s,out var tangent);var center=p+Vector3.Cross(Vector3.up,tangent).normalized*side*R(23,49);center.y=Ground(center);
   var group=new GameObject("Scrub patch "+patches.ToString("000"));group.transform.SetParent(root,false);group.transform.position=center;
   var renderHigh=new List<Renderer>();var renderLow=new List<Renderer>();int patchCount=0;
   for(int kind=0;kind<2;kind++){
    var prefab=Asset<GameObject>(Source+"/"+(kind==0?"wild_rooibos_bush_upright":"searsia_lucida_optimized")+" Sierra.prefab");var source=prefab.GetComponent<MeshFilter>().sharedMesh;var mat=prefab.GetComponent<Renderer>().sharedMaterial;var high=new List<CombineInstance>();var low=new List<CombineInstance>();int added=0;
    // Most silhouettes use the 1.4k-vertex shrub; the denser leafy mesh is an accent.
    for(int trial=0;trial<90&&added<(kind==0?11:1);trial++){
     var q=center+new Vector3(R(-10,10),0,R(-10,10));float scale=kind==0?R(.85f,1.65f):R(.9f,1.5f);float radius=kind==0?scale*.8f:scale*.45f;
     if(!Safe(q,radius)||plants.Any(v=>Flat(v,q)<1.2f))continue;q.y=Ground(q)-.06f;
     var rotation=Quaternion.FromToRotation(Vector3.up,Vector3.Slerp(Vector3.up,Normal(q),.75f))*Quaternion.Euler(0,R(0,360),0);var matrix=group.transform.worldToLocalMatrix*Matrix4x4.TRS(q,rotation,Vector3.one*scale);
     for(int sub=0;sub<source.subMeshCount;sub++){var item=new CombineInstance{mesh=source,subMeshIndex=sub,transform=matrix};high.Add(item);if(added%3==0)low.Add(item);}
     minimumMargin=Mathf.Min(minimumMargin,Edge(q)-radius);plants.Add(q);added++;patchCount++;count++;
    }
    if(high.Count>0){renderHigh.Add(Combined("Scrub_"+patches+"_"+kind+"_Near",high,mat,group.transform));renderLow.Add(Combined("Scrub_"+patches+"_"+kind+"_Far",low,mat,group.transform));}
   }
   if(patchCount==0){Object.DestroyImmediate(group);continue;}
   var lod=group.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.07f,renderHigh.ToArray()),new LOD(.014f,renderLow.ToArray())});lod.RecalculateBounds();patches++;
  }
  if(count<650)throw new Exception("Not enough safely positioned shrubs: "+count);report.AppendLine(count+" shrubs in "+patches+" combined patches (two LODs, distant one-third density); no GameObject per shrub.");
 }
 static void Grass()
 {
  int n=data.detailWidth;var map=data.GetDetailLayer(0,0,n,n,0);int count=0;
  for(int attempt=0;attempt<250000&&count<9000;attempt++){
   var p=Sample(R(0,lengths.Last()),out var tangent);p+=Vector3.Cross(Vector3.up,tangent).normalized*(random.Next(2)==0?-1:1)*R(17,105);
   int x=Mathf.FloorToInt((p.x-terrain.transform.position.x)/data.size.x*n),z=Mathf.FloorToInt((p.z-terrain.transform.position.z)/data.size.z*n);if(x<0||z<0||x>=n||z>=n||map[z,x]>0)continue;
   p=terrain.transform.position+new Vector3((x+.5f)*data.size.x/n,0,(z+.5f)*data.size.z/n);
   if(!Safe(p,2.3f,35)||Mathf.PerlinNoise(p.x*.06f+39,p.z*.053f+19)<.38f)continue;map[z,x]=1;count++;
  }
  data.SetDetailLayer(0,0,0,map);var prototypes=data.detailPrototypes;prototypes[0].minWidth=1.1f;prototypes[0].maxWidth=2;prototypes[0].minHeight=.7f;prototypes[0].maxHeight=1.25f;data.detailPrototypes=prototypes;
  foreach(var p in data.detailPrototypes)if(!p.Validate(out string error))throw new Exception("Invalid detail prototype: "+error);
  report.AppendLine(count+" additional instanced grass cells; three valid single-material Terrain Detail prototypes. Distance/density preserved.");
 }
 static void Trees()
 {
  var prefab=Asset<GameObject>("Assets/Art/Forest/Prefabs/PineA.prefab");var materialCopies=new Dictionary<Material,Material>();int count=0;
  for(int attempt=0;attempt<2000&&count<12;attempt++){
   var p=Sample(R(0,lengths.Last()),out var tangent);p+=Vector3.Cross(Vector3.up,tangent).normalized*(random.Next(2)==0?-1:1)*R(32,85);
   if(!Safe(p,5,20))continue;var go=Object.Instantiate(prefab,root);go.name="Isolated mountain pine "+count.ToString("00");foreach(var c in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
   var b=BoundsOf(go);float scale=R(5.5f,8)/b.size.y;go.transform.localScale*=scale;p.y=Ground(p);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,R(0,360),0));b=BoundsOf(go);go.transform.position+=Vector3.up*(Ground(b.center)-b.min.y-.15f);
   foreach(var r in go.GetComponentsInChildren<Renderer>()){
    r.sharedMaterials=r.sharedMaterials.Select(m=>{if(!materialCopies.TryGetValue(m,out var copy)){copy=new Material(m){name="Sierra pine "+materialCopies.Count,enableInstancing=true};copy.SetColor("_BaseColor",m.GetColor("_BaseColor")*new Color(.9f,.92f,.8f));AssetDatabase.CreateAsset(copy,Folder+"/Materials/Pine_"+materialCopies.Count+".mat");materialCopies.Add(m,copy);}return copy;}).ToArray();RendererSetup(r,true);
   }
   var lod=go.GetComponent<LODGroup>();if(lod==null)lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.012f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();occupied.Add(BoundsOf(go));count++;
  }report.AppendLine(count+" isolated existing Kenney pines, private muted materials; no imported/new assets.");
 }
 static void Lighting()
 {
  var sky=new Material(RenderSettings.skybox){name="Sierra warm afternoon"};sky.SetColor("_SkyTint",new Color(.51f,.57f,.63f));sky.SetColor("_GroundColor",new Color(.37f,.36f,.31f));sky.SetFloat("_AtmosphereThickness",1.05f);sky.SetFloat("_Exposure",1.05f);AssetDatabase.CreateAsset(sky,Folder+"/Materials/Sky.mat");RenderSettings.skybox=sky;
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.63f,.71f);RenderSettings.ambientEquatorColor=new Color(.43f,.44f,.39f);RenderSettings.ambientGroundColor=new Color(.24f,.23f,.19f);
  RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.64f,.69f,.72f);RenderSettings.fogDensity=.0018f;
  var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.transform.rotation=Quaternion.Euler(36,-42,0);sun.color=new Color(1,.91f,.76f);sun.intensity=1.24f;sun.shadows=LightShadows.Soft;
  report.AppendLine("Warm 36-degree afternoon sun, cool sky fill and subtle distant haze; major rocks/trees cast shadows. No extra lights, post effects or global quality changes.");
 }
 public static object Audit()
 {
  var scene=EditorSceneManager.OpenPreviewScene(ScenePath);
  try {
   var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
   var environment=all.Single(t=>t.name==RootName);var t=all.Select(x=>x.GetComponent<Terrain>()).First(x=>x!=null);var d=t.terrainData;
   var renderers=environment.GetComponentsInChildren<MeshRenderer>();
   foreach(var r in renderers){if(r.GetComponent<MeshFilter>().sharedMesh==null||r.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported))throw new Exception("Missing/unsupported scenery asset: "+r.name);}
   if(environment.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Unexpected scenery collider.");
   int points=all.Single(x=>x.name=="AI_Waypoints").childCount;if(points!=132)throw new Exception("Waypoints changed.");
   int[] detailCounts=new int[d.detailPrototypes.Length];
   for(int l=0;l<detailCounts.Length;l++){
    if(!d.detailPrototypes[l].Validate(out var error))throw new Exception(error);
    for(int z=0;z<d.detailPatchCount;z++)for(int x=0;x<d.detailPatchCount;x++)detailCounts[l]+=d.ComputeDetailInstanceTransforms(x,z,l,1,out var b).Length;
   }
   foreach(var layer in d.terrainLayers){if(layer.diffuseTexture==null||layer.normalMapTexture==null)throw new Exception("Missing PBR map.");if(layer.diffuseTexture.width>2048||layer.normalMapTexture.width>2048)throw new Exception("Texture over budget.");}
   var result=new{waypoints=points,terrain=AssetDatabase.GetAssetPath(d),layers=d.terrainLayers.Length,detailInstances=detailCounts,renderers=renderers.Length,lodGroups=environment.GetComponentsInChildren<LODGroup>().Length,sceneryColliders=0,playerActive=all.Single(x=>x.name=="Porsche 911 SC Rally").gameObject.activeSelf};
   File.WriteAllText(Log+"/audit.txt","COMPLETE: PASS\n"+JsonUtility.ToJson(new AuditCounts{waypoints=points,detailInstances=detailCounts,renderers=renderers.Length},true));return result;
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
 [Serializable] class AuditCounts{public int waypoints,renderers;public int[] detailInstances;}
 public static void Compress()
 {
  Idle();int count=0;
  foreach(string path in Directory.GetFiles(Folder+"/Meshes","*.asset")){
   var mesh=Asset<Mesh>(path.Replace('\\','/'));MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Medium);EditorUtility.SetDirty(mesh);count++;
  }AssetDatabase.SaveAssets();File.WriteAllText(Log+"/compression.txt",count+" scenery-only combined meshes set to medium storage compression. No physics meshes modified.\nCOMPLETE: PASS\n");
 }
 public static void PineColors()
 {
  Idle();var originals=Asset<GameObject>("Assets/Art/Forest/Prefabs/PineA.prefab").GetComponent<Renderer>().sharedMaterials;
  for(int i=0;i<originals.Length;i++){var material=Asset<Material>(Folder+"/Materials/Pine_"+i+".mat");material.SetColor("_BaseColor",originals[i].GetColor("_BaseColor")*new Color(.9f,.92f,.8f));EditorUtility.SetDirty(material);}AssetDatabase.SaveAssets();
 }
 public static void ArchiveFailedAttempt()
 {
  Idle();string baseline=File.ReadAllText(Log+"/baseline.txt").Trim();
  if(!File.ReadAllText(Log+"/build.txt").Contains("FAILED:")||!File.ReadAllBytes(ScenePath).SequenceEqual(File.ReadAllBytes(baseline)))throw new Exception("Not an untouched failed build; refusing archive.");
  string destination=Path.GetDirectoryName(baseline)+"/UnreferencedGeneratedAssets";
  if(Directory.Exists(destination))throw new Exception("Archive already exists.");
  FileUtil.MoveFileOrDirectory(Folder,destination);if(File.Exists(Folder+".meta"))FileUtil.MoveFileOrDirectory(Folder+".meta",destination+".meta");AssetDatabase.Refresh();
 }
}
