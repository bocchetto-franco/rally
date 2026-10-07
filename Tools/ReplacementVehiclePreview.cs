using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public static class ReplacementVehiclePreview
{
 public static void Run()
 {
  var player=GameObject.Find("Porsche 911 SC Rally");var controller=player.GetComponent<JrsVehicleController>();
  string wheels=string.Join(";",new[]{controller.frontLeftWheel,controller.frontRightWheel,controller.rearLeftWheel,controller.rearRightWheel}.Select(w=>player.transform.InverseTransformPoint(w.transform.position)+" radius="+w.radius));
  var root=new GameObject("Temporary vehicle preview");var copy=new GameObject("Porsche reference");copy.transform.SetParent(root.transform);copy.transform.position=new Vector3(0,0,0);
  var bounds=new Bounds();bool first=true;
  foreach(var r in player.GetComponentsInChildren<MeshRenderer>()){
   if(!r.enabled)continue;var mesh=r.GetComponent<MeshFilter>();if(mesh==null)continue;
   var go=new GameObject(r.name);go.transform.SetParent(copy.transform);
   go.transform.localPosition=player.transform.InverseTransformPoint(r.transform.position);
   go.transform.localRotation=Quaternion.Inverse(player.transform.rotation)*r.transform.rotation;
   go.transform.localScale=r.transform.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
  }
  foreach(var r in copy.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
  copy.transform.position+=Vector3.up*(-bounds.min.y);
  var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/BmwE30.prefab"),root.transform);
  model.transform.position=new Vector3(3.4f,.001f,0);
  var audi=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vehicles/AudiQuattro.prefab"),root.transform);audi.transform.position=new Vector3(-3.4f,0,0);
  var visual=model.GetComponent<RallyVehicleVisual>();
  File.WriteAllText("Logs/replacement-vehicle-preview.txt","Porsche visible size: "+bounds.size+" wheels: "+wheels+"\nBMW body: "+visual.bodyBounds+" radii: "+string.Join(",",visual.radii));
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(22,.1f,20);
  var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.2f,.2f,.2f));floor.GetComponent<Renderer>().sharedMaterial=mat;
  foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
  var goCam=new GameObject("Vehicle preview camera");goCam.transform.SetParent(root.transform);var cam=goCam.AddComponent<Camera>();cam.cullingMask=1<<31;cam.fieldOfView=40;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.14f,.17f,.21f);cam.farClipPlane=40;
  cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  goCam.transform.SetPositionAndRotation(new Vector3(9,5.5f,13),Quaternion.LookRotation(new Vector3(0,.5f,0)-new Vector3(9,5.5f,13)));
  var rt=new RenderTexture(1400,900,24);var tex=new Texture2D(1400,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
  try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,900),0,0);tex.Apply();File.WriteAllBytes("Logs/ReplacementVehicles_Preview.png",tex.EncodeToPNG());}
  finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mat);}
 }
}
