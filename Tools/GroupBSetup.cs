using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class GroupBSetup
{
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string name in new[]{"LanciaS4","Peugeot205T16"})
        {
            string folder="Assets/Art/Vehicles/"+name;
            var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/Body.png");
            importer.sRGBTexture=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Compressed;
            importer.mipmapEnabled=true;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            string materialPath=folder+"/Body.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Body.png"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.30f);
            material.SetFloat("_Metallic",.05f);material.enableInstancing=true;
            EditorUtility.SetDirty(material);
            RallyClassicMiniSetup.BuildVariant(folder,name,_=>material);
            string path="Assets/Resources/Vehicles/"+name+".prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                prefab.GetComponent<RallyVehicleVisual>().useAuthoredScale=true;
                PrefabUtility.SaveAsPrefabAsset(prefab,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        AssetDatabase.SaveAssets();
    }
}
