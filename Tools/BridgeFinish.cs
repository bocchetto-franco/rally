using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
public static class BridgeFinish
{
 public static void Run()
 {
  var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Circuit03RiverBridge")).First(x=>x!=null);
  var flags=BindingFlags.Static|BindingFlags.NonPublic;
  float station=(float)t.GetField("station",flags).GetValue(null);
  var method=t.GetMethod("NewPoint",flags);
  Func<float,Vector3> point=d=>(Vector3)method.Invoke(null,new object[]{d});
  var root=GameObject.Find("Circuit 03 - Timber River Crossing").transform;
  var samples=Enumerable.Range(0,1201).Select(i=>point(station-60+i*.1f)).ToArray();
  Func<Vector3,(float y,float gap,float station)> nearest=p=>{
   float best=float.MaxValue,y=0,s=0;
   for(int i=0;i<samples.Length-1;i++) {
    var a=samples[i];var b=samples[i+1];var d=new Vector2(b.x-a.x,b.z-a.z);var q=new Vector2(p.x-a.x,p.z-a.z);
    float u=Mathf.Clamp01(Vector2.Dot(q,d)/Mathf.Max(.00001f,d.sqrMagnitude));float sq=(q-d*u).sqrMagnitude;
    if(sq<best){best=sq;y=Mathf.Lerp(a.y,b.y,u);s=station-60+(i+u)*.1f;}
   }return(y,Mathf.Sqrt(best),s);
  };
  foreach(string name in new[]{"Circuit03_Road","Circuit03_Runoff"}) {
   var mesh=GameObject.Find(name).GetComponent<ProBuilderMesh>();
   var positions=mesh.positions.ToArray();
   for(int i=0;i<positions.Length;i++) {
    var p=mesh.transform.TransformPoint(positions[i]);var n=nearest(p);
    float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(52,57,Mathf.Abs(n.station-station)));
    if(n.gap<18&&w>0){p.y=Mathf.Lerp(p.y,n.y+(name.Contains("Runoff")?-.035f:0),w);positions[i]=mesh.transform.InverseTransformPoint(p);}
   }
   mesh.positions=positions;mesh.ToMesh();mesh.Refresh();mesh.GetComponent<MeshCollider>().sharedMesh=mesh.GetComponent<MeshFilter>().sharedMesh;
  }
  var terrain=UnityEngine.Object.FindAnyObjectByType<Terrain>();var data=terrain.terrainData;
  int res=data.heightmapResolution;var h=data.GetHeights(0,0,res,res);var origin=terrain.transform.position;var size=data.size;
  for(int z=0;z<res;z++)for(int x=0;x<res;x++){
   var p=origin+new Vector3(x*size.x/(res-1),h[z,x]*size.y,z*size.z/(res-1));var local=root.InverseTransformPoint(p);
   if(Mathf.Abs(local.x)>25||Mathf.Abs(local.z)>58)continue;
   var n=nearest(p);float a=Mathf.Abs(n.station-station);
   if(a<=18||a>=57||n.gap>=16)continue;
   float w=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,16,n.gap)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(52,57,a)));
   h[z,x]=(Mathf.Lerp(p.y,n.y-.15f,w)-origin.y)/size.y;
  }
  data.SetHeights(0,0,h);EditorUtility.SetDirty(data);Physics.SyncTransforms();
  t.GetMethod("ValidateDrivingSurface",flags).Invoke(null,null);
  var layout=t.GetField("layout",flags).GetValue(null);var lt=layout.GetType();
  var distances=(float[])lt.GetField("distances").GetValue(layout);
  var centers=(Vector3[])lt.GetField("centerline").GetValue(layout);
  var widths=(float[])lt.GetField("roadWidths").GetValue(layout);
  var updated=distances.Select(point).ToArray();
  for(int i=0;i<centers.Length;i++){centers[i]=updated[i];widths[i]=Mathf.Lerp(widths[i],10.4f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,52,Mathf.Abs(distances[i]-station))));}
  lt.GetField("notes").SetValue(layout,(string)lt.GetField("notes").GetValue(layout)+" Timber river bridge near 900m; preserve RiverBridge TerrainData and localized crossing.");
  File.WriteAllText("Assets/Art/Forest/Circuit03/Circuit03Layout.json",JsonUtility.ToJson(layout,true));
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  var capture=t.GetMethod("Capture",flags);var center=root.position;
  capture.Invoke(null,new object[]{center-root.forward*34+Vector3.up*3,center+root.forward*10,"Logs/Circuit03_Bridge_Driving.png"});
  capture.Invoke(null,new object[]{center+root.right*37+Vector3.up*19-root.forward*29,center-Vector3.up,"Logs/Circuit03_Bridge_Overview.png"});
  File.AppendAllText("Logs/circuit03-river-bridge.txt","Approach cross sections levelled; terrain matched to actual new route. Scene saved.\n");
  SessionState.SetString("Rally.RiverBridge.BakeTicks",DateTime.UtcNow.Ticks.ToString());
  if(!StaticOcclusionCulling.Compute())throw new Exception("Bake failed to start.");
  SessionState.SetBool("Rally.RiverBridge.Bake",true);
 }
}
