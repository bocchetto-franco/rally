using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Imports downloaded CC0 assets into reusable URP materials and upright prefabs.
public static class Circuit03ForestAssets
{
    const string Root="Assets/Art/Forest/";
    static T Load<T>(string p) where T:Object {var a=AssetDatabase.LoadAssetAtPath<T>(p);if(a==null)throw new Exception("Missing forest asset: "+p);return a;}
    public static void Build()
    {
        if(File.Exists(Root+"Prefabs/PineA.prefab"))return;
        foreach(string dir in new[]{"Materials","Terrain","Prefabs","Meshes"})Directory.CreateDirectory(Root+dir);
        AssetDatabase.Refresh();
        foreach(string path in Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".jpg")||p.EndsWith(".png")||p.EndsWith(".hdr")))
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;importer.mipmapEnabled=true;
            if(path.Contains("nor_gl")){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;}
            importer.SaveAndReimport();
        }
        foreach(string id in new[]{"mud_forest","forest_floor","mossy_rock"})
        {
            var layer=new TerrainLayer{name=id,diffuseTexture=Texture(id,"diff"),normalMapTexture=Texture(id,"nor_gl"),tileSize=Vector2.one*(id=="mossy_rock"?7:3),normalScale=.65f,smoothness=id=="mud_forest"?.25f:.12f,metallic=0};
            AssetDatabase.CreateAsset(layer,Root+"Terrain/"+id+".terrainlayer");
        }
        GroundMaterial("Wet forest road","mud_forest",.34f);
        GroundMaterial("Forest floor","forest_floor",.16f);
        var shoulder=Load<Material>(Root+"Materials/Forest floor.mat");shoulder.SetColor("_BaseColor",new Color(.58f,.65f,.55f));EditorUtility.SetDirty(shoulder);
        var terrain=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(terrain,Root+"Materials/Forest Terrain.mat");
        var sky=new Material(Shader.Find("Skybox/Panoramic"));sky.SetTexture("_MainTex",Load<Texture>(Root+"PolyHaven/kloofendal_overcast/kloofendal_overcast_2k.hdr"));sky.SetFloat("_Exposure",.9f);sky.SetFloat("_Rotation",65);AssetDatabase.CreateAsset(sky,Root+"Materials/Overcast sky.mat");
        Prefab("PineA","tree_pineTallA_detailed",12);Prefab("PineB","tree_pineTallB_detailed",10);Prefab("PineC","tree_pineRoundC",8);
        Prefab("Bush","plant_bushDetailed",1.4f);Prefab("Fern","plant_flatShort",.75f);Prefab("Rock","rock_largeA",1.3f);
        AssetDatabase.SaveAssets();
    }
    static Texture2D Texture(string id,string kind)=>Load<Texture2D>(Root+"PolyHaven/"+id+"/"+id+"_"+kind+"_2k.jpg");
    static void GroundMaterial(string name,string id,float smoothness)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetTexture("_BaseMap",Texture(id,"diff"));m.SetTexture("_BumpMap",Texture(id,"nor_gl"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.65f);m.SetFloat("_Smoothness",smoothness);m.SetTextureScale("_BaseMap",Vector2.one*2);m.SetTextureScale("_BumpMap",Vector2.one*2);AssetDatabase.CreateAsset(m,Root+"Materials/"+name+".mat");
    }
    static void Prefab(string name,string file,float height)
    {
        string path=Root+"KenneyNature/"+file+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();
        var instance=Object.Instantiate(Load<GameObject>(path));instance.transform.position=Vector3.zero;
        // Keep FBX axis correction while baking. Do not cancel its root rotation.
        var combines=new List<CombineInstance>();var materials=new List<Material>();
        foreach(var f in instance.GetComponentsInChildren<MeshFilter>())for(int i=0;i<f.sharedMesh.subMeshCount;i++)
        {
            combines.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=f.transform.localToWorldMatrix});
            var original=f.GetComponent<Renderer>().sharedMaterials[i];string matPath=Root+"Materials/Kenney_"+original.name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=original.name,enableInstancing=true};
                string lower=original.name.ToLowerInvariant();
                var color=lower.Contains("leaf")||lower.Contains("grass")?new Color(.21f,.38f,.19f):lower.Contains("wood")?new Color(.27f,.18f,.12f):new Color(.4f,.43f,.39f);
                material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(material,matPath);
            }
            materials.Add(material);
        }
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines.ToArray(),false,true);mesh.RecalculateBounds();var bounds=mesh.bounds;float scale=height/bounds.size.y;
        var offset=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);mesh.vertices=mesh.vertices.Select(v=>(v-offset)*scale).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Root+"Meshes/"+name+".asset");Object.DestroyImmediate(instance);
        var go=new GameObject(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/"+name+".prefab");Object.DestroyImmediate(go);
        Debug.Log("Forest prefab "+name+": "+mesh.vertexCount+" vertices, bounds "+mesh.bounds);
    }
}
