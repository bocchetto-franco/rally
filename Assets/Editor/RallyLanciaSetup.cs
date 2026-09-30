using System.IO;
using UnityEditor;
using UnityEngine;

public static class RallyLanciaSetup
{
    const string Folder = "Assets/Art/Vehicles/LanciaDelta";

    [MenuItem("Tools/Rally/Build Lancia Delta Visual")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(Folder + "/Textures", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            bool normal = path.Contains("_Normal");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        RallyClassicMiniSetup.BuildVariant(Folder, "LanciaDelta", ConvertMaterial);
    }

    static Material ConvertMaterial(Material original)
    {
        string path = Folder + "/Materials/" + original.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = original.name;
        Color tint = original.HasProperty("_Color") ? original.color : Color.gray;
        string color = null, normal = null;
        float metallic = 0f, smoothness = .4f;
        bool cutout = false;
        switch (original.name)
        {
            case "Paint": tint = new Color(.82f, .44f, .025f); smoothness = .6f; metallic = .15f; break;
            case "detail 1": color = "Details"; normal = "Details_Normal"; break;
            case "Chrome Detail": color = "Chrome"; metallic = .85f; smoothness = .72f; break;
            case "glass": color = "Glass"; smoothness = .8f; break;
            case "Light": color = "Lights"; smoothness = .65f; break;
            case "LightBump": color = "LightBump"; normal = "LightBump_Normal"; smoothness = .65f; break;
            case "Tire": color = "Tire"; normal = "Tire_Normal"; smoothness = .18f; break;
            case "Rim": color = "Rim"; normal = "Rim_Normal"; metallic = .5f; break;
            case "rim 2": color = "RimFront"; normal = "RimFront_Normal"; metallic = .5f; break;
            case "Logo": color = "Logo"; cutout = true; break;
            case "Number": color = "Plate"; cutout = true; break;
            case "Legend": color = "Legend"; cutout = true; break;
            case "Legion": color = "Legion"; cutout = true; break;
            case "Red Suns": color = "RedSuns"; cutout = true; break;
            case "Shield 2": color = "Shield"; cutout = true; break;
            case "Cube": color = "Cube"; break;
        }
        if (color != null)
        {
            material.SetTexture("_BaseMap", Load(color));
            tint = Color.white;
        }
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", Load(normal));
            material.EnableKeyword("_NORMALMAP");
        }
        if (cutout)
        {
            material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", .4f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = 2450;
        }
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + ".png");
    public static void BuildAndVerify() { Build(); RallyVehiclePhysicsParityTest.Run(); }
}
