using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class BmwReplacementSetup
{
 const string Folder="Assets/Art/Vehicles/BmwE30";
 public static void Build()
 {
  AssetDatabase.Refresh();
  foreach(string path in Directory.GetFiles(Folder+"/Textures","*.png")) {
   var i=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
   bool normal=path.ToLowerInvariant().Contains("normal");
   i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
   i.sRGBTexture=!normal&&!path.Contains("Metallic")&&!path.Contains("Roughness")&&!path.Contains("AO");
   i.maxTextureSize=2048;i.textureCompression=TextureImporterCompression.Compressed;i.mipmapEnabled=true;i.SaveAndReimport();
  }
  RallyClassicMiniSetup.BuildVariant(Folder,"BmwE30",Convert);
  var prefab=PrefabUtility.LoadPrefabContents("Assets/Resources/Vehicles/BmwE30.prefab");
  try {
   prefab.GetComponent<RallyVehicleVisual>().useAuthoredScale=true;
   PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Resources/Vehicles/BmwE30.prefab");
  } finally { PrefabUtility.UnloadPrefabContents(prefab); }
 }
 static Texture2D Tex(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+name+".png");
 static Material Convert(Material original)
 {
  string name=original.name,path=Folder+"/Materials/"+name+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  Color tint=Color.white;float metal=0,smooth=.35f;string color=null,normal=null,ao=null;
  switch(name) {
   case "BMW_E30_M3_PAINT":tint=new Color(.52f,.022f,.015f);metal=.25f;smooth=.72f;break;
   case "BMW_E30_M3_PLASTIC":tint=new Color(.035f,.035f,.035f);smooth=.25f;break;
   case "BMW_E30_M3_BLACKOUT":tint=new Color(.012f,.012f,.012f);smooth=.1f;break;
   case "BMW_E30_M3_CHROME":tint=new Color(.65f,.65f,.65f);metal=.85f;smooth=.8f;break;
   case "BMW_E30_M3_SIDE_MIRROR":metal=1;smooth=.9f;break;
   case "BMW_E30_M3_WINDOWS":tint=new Color(.07f,.105f,.13f);metal=.35f;smooth=.9f;break;
   case "BMW_E30_M3_EMBLEMS":color="BMW_E30_M3_EMBLEMS_COLOR";metal=.3f;break;
   case "BMW_E30_M3_LENS":color="BMW_E30_M3_LENS_COLOR";normal="BMW_E30_M3_LENS_NORMAL";smooth=.72f;break;
   case "BMW_E30_M3_HEADLIGHT_REFLECTOR":color="BMW_E30_M3_HEADLIGHT_REFLECTOR_EMISSION";metal=.4f;smooth=.7f;break;
   case "BMW_E30_M3_TAILLIGHT_REFLECTOR":color="BMW_E30_M3_TAILLIGHT_REFLECTOR_EMISSION_BRAKE_LIGHTS";smooth=.65f;break;
   case "BMW_E30_M3_RIM":color=name+"_Base_color";normal=name+"_Normal_OpenGL";ao=name+"_Mixed_AO";metal=.65f;smooth=.45f;break;
   case "BMW_E30_M3_TIRE":color=name+"_Base_color";normal=name+"_Normal_OpenGL";ao=name+"_Mixed_AO";smooth=.12f;break;
   case "Brake_Disc":color="Brake_Disc_Base_color";normal="Brake_Disc_Normal_OpenGL";metal=.8f;break;
   case "Brembo_Calipers":color="Caliper_Bake_Base_color_Red";normal="Caliper_Bake_Normal_OpenGL";metal=.2f;break;
   case "Logo_Plane":color="Brembo_Logo_Alpha";m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.5f);m.EnableKeyword("_ALPHATEST_ON");break;
  }
  m.SetColor("_BaseColor",tint);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
  if(color!=null)m.SetTexture("_BaseMap",Tex(color));
  if(normal!=null){m.SetTexture("_BumpMap",Tex(normal));m.EnableKeyword("_NORMALMAP");}
  if(ao!=null){m.SetTexture("_OcclusionMap",Tex(ao));m.EnableKeyword("_OCCLUSIONMAP");}
  m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
 }
}
