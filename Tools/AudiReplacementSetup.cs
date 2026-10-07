using System.IO;
using UnityEditor;
using UnityEngine;
public static class AudiReplacementSetup
{
 const string Folder="Assets/Art/Vehicles/AudiQuattro";
 public static void Build()
 {
  AssetDatabase.Refresh();
  foreach(string raw in Directory.GetFiles(Folder+"/Textures")) {
   if(raw.EndsWith(".meta"))continue;string path=raw.Replace('\\','/');
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
   bool normal=path.Contains("normal");importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
   importer.sRGBTexture=path.Contains("albedo");importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
  }
  RallyClassicMiniSetup.BuildVariant(Folder,"AudiQuattro",Convert);
  var prefab=PrefabUtility.LoadPrefabContents("Assets/Resources/Vehicles/AudiQuattro.prefab");
  try{prefab.GetComponent<RallyVehicleVisual>().useAuthoredScale=true;PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Resources/Vehicles/AudiQuattro.prefab");}
  finally{PrefabUtility.UnloadPrefabContents(prefab);}
 }
 static Material Convert(Material original)
 {
  string path=Folder+"/Audi Rally PBR.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetTexture("_BaseMap",Tex("Audi_Quttor_S1_albedo.jpeg"));m.SetColor("_BaseColor",Color.white);
  m.SetTexture("_BumpMap",Tex("Audi_Quttor_S1_normal.png"));m.SetFloat("_BumpScale",1);m.EnableKeyword("_NORMALMAP");
  m.SetTexture("_MetallicGlossMap",Tex("MetallicSmoothness.png"));m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",.75f);m.EnableKeyword("_METALLICSPECGLOSSMAP");
  m.SetTexture("_OcclusionMap",Tex("Audi_Quttor_S1_AO.jpeg"));m.SetFloat("_OcclusionStrength",.7f);m.EnableKeyword("_OCCLUSIONMAP");
  m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
 }
 static Texture2D Tex(string file)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+file);
}
