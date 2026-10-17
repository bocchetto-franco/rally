using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BridgeObstructionAudit
{
 [Serializable] class Layout {public Vector3[] centerline=Array.Empty<Vector3>(); public float[] distances=Array.Empty<float>(),roadWidths=Array.Empty<float>();}
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Leave Play first");
  var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_03.unity");
  try {
   var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
   var bridge=all.Single(t=>t.name=="Circuit 03 - Timber River Crossing");
   var lines=new System.Collections.Generic.List<string>();
   lines.Add("Bridge position="+bridge.position+" forward="+bridge.forward);
   foreach(var t in all.Where(t=>t.name.Contains("Crossing")))lines.Add(t.name+" world="+t.position+" local="+bridge.InverseTransformPoint(t.position));
   foreach(var c in all.SelectMany(t=>t.GetComponents<Collider>()).Where(c=>c.enabled&&c.gameObject.activeInHierarchy)) {
    var local=bridge.InverseTransformPoint(c.bounds.center);
    if(Mathf.Abs(local.z)>85||Mathf.Abs(local.x)>24||Mathf.Abs(local.y)>10)continue;
    string path=c.name;for(var p=c.transform.parent;p!=null;p=p.parent)path=p.name+"/"+path;
    lines.Add(path+" id="+GlobalObjectId.GetGlobalObjectIdSlow(c)+" type="+c.GetType().Name+" local="+local+" size="+c.bounds.size+" trigger="+c.isTrigger+" material="+(c.sharedMaterial==null?"none":c.sharedMaterial.name));
   }
   var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Art/Forest/Circuit03/Circuit03Layout.json"));
   foreach(var t in all.Where(t=>t.GetComponent<Collider>()!=null&& (t.name.Contains("Road")||t.name.Contains("Runoff")||t.name.Contains("Terrain"))))lines.Add("SURFACE "+t.name+" "+t.GetComponent<Collider>().GetType().Name);
   for(int i=0;i<layout.centerline.Length-1;i++) {
    if(Vector3.Distance(layout.centerline[i],bridge.position)>70)continue;
    for(float a=0;a<1;a+=.25f) {
     var p=Vector3.Lerp(layout.centerline[i],layout.centerline[i+1],a);var hits=new System.Collections.Generic.List<string>();
     foreach(var c in all.SelectMany(t=>t.GetComponents<Collider>()).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.attachedRigidbody==null))
      if(c.Raycast(new Ray(p+Vector3.up*4,Vector3.down),out var hit,8))hits.Add(c.name+" dy="+(hit.point.y-p.y).ToString("F3")+" normal="+hit.normal);
     lines.Add("SUPPORT "+Mathf.Lerp(layout.distances[i],layout.distances[i+1],a).ToString("F2")+" "+string.Join(";",hits));
    }
   }
   var probe=new GameObject("audit probe").AddComponent<BoxCollider>();probe.size=new Vector3(1.8f,.8f,4.3f);
   try {
    foreach(var c in all.SelectMany(t=>t.GetComponents<Collider>()).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&!(c is WheelCollider)&&c.attachedRigidbody==null)) {
     float worst=0;string at="";
     for(int i=0;i<layout.centerline.Length-1;i++) {
      if(Vector3.Distance(layout.centerline[i],bridge.position)>80)continue;
      var f=(layout.centerline[i+1]-layout.centerline[i]).normalized;var r=Vector3.Cross(Vector3.up,f).normalized;
      for(float a=0;a<1;a+=.25f)foreach(float lane in new[]{-1f,0,1f}) {
       float lateral=lane*(Mathf.Lerp(layout.roadWidths[i],layout.roadWidths[i+1],a)*.5f-.95f);
       var p=Vector3.Lerp(layout.centerline[i],layout.centerline[i+1],a)+Vector3.up*.7f+r*lateral;
       if(!c.bounds.Intersects(new Bounds(p,new Vector3(6,3,6))))continue;
       if(Physics.ComputePenetration(probe,p,Quaternion.LookRotation(f),c,c.transform.position,c.transform.rotation,out var dir,out float depth)&&depth>worst){worst=depth;at="station="+Mathf.Lerp(layout.distances[i],layout.distances[i+1],a)+" lane="+lane+" direction="+dir;}
      }
     }
     if(worst>0)lines.Add("INTRUSION "+c.name+" depth="+worst+" "+at+" id="+GlobalObjectId.GetGlobalObjectIdSlow(c));
    }
   }finally{UnityEngine.Object.DestroyImmediate(probe.gameObject);}
   File.WriteAllLines("Logs/bridge-obstruction-audit.txt",lines);
  }finally {EditorSceneManager.ClosePreviewScene(scene);}
 }
}
