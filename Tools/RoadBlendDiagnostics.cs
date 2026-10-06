using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
public static class RoadBlendDiagnostics
{
 public static object Run()
 {
  var setup=EditorSceneManager.GetSceneManagerSetup();
  try {
   EditorSceneManager.OpenScene("Assets/Scenes/Circuit_03.unity");
   var road=GameObject.Find("Circuit03_Road").GetComponent<ProBuilderMesh>();
   var shoulder=GameObject.Find("Circuit03_Runoff").GetComponent<ProBuilderMesh>();
   var rp=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
   var sp=shoulder.positions.Select(p=>shoulder.transform.TransformPoint(p)).ToArray();
   var face=shoulder.faces.OrderBy(f=>(sp[f.distinctIndexes.Min()]-new Vector3(320.14f,5.32f,90.21f)).sqrMagnitude).First();
   return string.Join("\n",face.distinctIndexes.OrderBy(i=>i).Select(i=>{var p=sp[i];var nearest=rp.OrderBy(q=>(new Vector2(q.x-p.x,q.z-p.z)).sqrMagnitude).First();return i+" shoulder="+p.ToString("F4")+" nearest="+nearest.ToString("F4")+" delta="+(p-nearest).ToString("F4");}));
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
}
