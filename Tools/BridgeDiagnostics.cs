using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
public static class BridgeDiagnostics
{
 public static void Run()
 {
  var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Circuit03RiverBridge")).First(x=>x!=null);
  var flags=BindingFlags.Static|BindingFlags.NonPublic;
  float station=(float)t.GetField("station",flags).GetValue(null);
  var method=t.GetMethod("NewPoint",flags);
  Func<float,Vector3> point=d=>(Vector3)method.Invoke(null,new object[]{station+d});
  var lines=new System.Collections.Generic.List<string>();
  for(float s=-55;s<=55;s+=.5f) {
   var p=point(s);var r=Vector3.Cross(Vector3.up,(point(s+.2f)-point(s-.2f)).normalized);
   foreach(float lane in new[]{-3f,0,3f}) {
    var hits=Physics.RaycastAll(p+r*lane+Vector3.up*5,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
    var h=hits.Where(x=>x.collider.name=="Circuit03_Road"||x.collider.name=="Continuous smooth deck collider").OrderBy(x=>x.distance).ToArray();
    if(h.Length==0||Mathf.Abs(h[0].point.y-p.y)>.09f) lines.Add($"s={s} lane={lane} expected={p.y} hits="+string.Join(";",hits.Select(x=>x.collider.name+":"+x.point.y)));
   }
  }
  System.IO.File.WriteAllLines("Logs/bridge-diagnostics.txt",lines);
 }
}
