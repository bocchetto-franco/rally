using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Circuit02SierrasInspect
{
 public static object Details()
 {
  var s=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_02.unity");
  try {
   var a=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
   var terrain=a.Select(x=>x.GetComponent<Terrain>()).First(x=>x!=null);var samples=new System.Collections.Generic.List<string>();
   int px=Mathf.FloorToInt((0-terrain.transform.position.x)/terrain.terrainData.size.x*terrain.terrainData.detailPatchCount),pz=Mathf.FloorToInt((42-terrain.transform.position.z)/terrain.terrainData.size.z*terrain.terrainData.detailPatchCount);
   samples.Add("patches="+terrain.terrainData.detailPatchCount+", resolution="+terrain.terrainData.detailResolution+", nearby="+px+","+pz);
   for(int l=0;l<3;l++){var p=terrain.terrainData.detailPrototypes[l];bool valid=p.Validate(out var error);samples.Add("layer "+l+" painted="+terrain.terrainData.GetDetailLayer(0,0,512,512,l).Cast<int>().Sum()+" valid="+valid+" "+error);}
   for(int z=Mathf.Max(0,pz-2);z<=Mathf.Min(terrain.terrainData.detailPatchCount-1,pz+2);z++)for(int x=Mathf.Max(0,px-2);x<=Mathf.Min(terrain.terrainData.detailPatchCount-1,px+2);x++)for(int layer=0;layer<3;layer++){
    var instances=terrain.terrainData.ComputeDetailInstanceTransforms(x,z,layer,1,out var bounds);
    if(instances.Length>0)samples.Add(x+","+z+" layer="+layer+" count="+instances.Length+" first="+instances[0].posX+","+instances[0].posY+","+instances[0].posZ);
   }
   return new {
    detailInstances=samples.ToArray(),
    rocks=a.Where(t=>t.name.StartsWith("Granite outcrop")).Take(5).Select(t=>new {t.name,pos=t.position.ToString(),scale=t.lossyScale.ToString(),bounds=t.GetComponentsInChildren<Renderer>().Select(r=>r.bounds.ToString()).ToArray(),lod=t.GetComponent<LODGroup>().size}).ToArray(),
    terrains=a.Select(t=>t.GetComponent<Terrain>()).Where(t=>t!=null).Select(t=>new{t.drawTreesAndFoliage,t.detailObjectDistance,t.detailObjectDensity,scatter=t.terrainData.detailScatterMode.ToString(),protos=t.terrainData.detailPrototypes.Select(p=>new{p.prototype.name,p.useInstancing,p.density,p.targetCoverage,mode=p.renderMode.ToString(),bounds=p.prototype.GetComponent<MeshFilter>().sharedMesh.bounds.ToString(),scale=p.prototype.transform.localScale.ToString()}).ToArray()}).ToArray()
   };
  }finally{EditorSceneManager.ClosePreviewScene(s);}
 }
 public static object Run()
 {
  var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Circuit_02.unity");
  try {
   var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
   var t=all.Select(x=>x.GetComponent<Terrain>()).First(x=>x!=null);
   return new {
    roots=scene.GetRootGameObjects().Select(g=>new {g.name,count=g.transform.childCount}).ToArray(),
    terrain=new {path=AssetDatabase.GetAssetPath(t.terrainData),position=t.transform.position.ToString(),size=t.terrainData.size.ToString(),t.terrainData.heightmapResolution,
      details=t.terrainData.detailPrototypes.Select(p=>new {name=p.prototype?.name,path=AssetDatabase.GetAssetPath(p.prototype),p.minWidth,p.maxWidth,p.minHeight,p.maxHeight,p.useInstancing}).ToArray()},
    groups=all.Where(x=>x.name.Contains("Crowd")||x.name=="Circuit 02 - Trees and Rocks").Select(x=>new{x.name,position=x.position.ToString(),count=x.childCount}).ToArray(),
    rocks=all.Where(x=>x.name.StartsWith("Rocks_001")).Select(x=>new {x.name,position=x.position.ToString(),meshes=x.GetComponentsInChildren<MeshFilter>().Select(m=>new{m.name,vertices=m.sharedMesh.vertexCount,mesh=AssetDatabase.GetAssetPath(m.sharedMesh),material=AssetDatabase.GetAssetPath(m.GetComponent<Renderer>().sharedMaterial)}).ToArray()}).ToArray(),
    water=all.Select(x=>x.GetComponent<RallyPuddleSlowZone>()).Where(p=>p!=null).Select(p=>new{p.name,material=AssetDatabase.GetAssetPath(p.GetComponent<Renderer>().sharedMaterial)}).ToArray()
   };
  } finally {EditorSceneManager.ClosePreviewScene(scene);}
 }
}
