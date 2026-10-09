using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Install three desktop presets without renaming/removing the existing PC/Mobile levels.</summary>
public static class RallyOptionsSetup
{
    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Leave Play before installing quality presets.");
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        var settings = new SerializedObject(assets[0]);
        var levels = settings.FindProperty("m_QualitySettings");
        int pc = -1;
        for (int i = 0; i < levels.arraySize; i++)
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "PC") pc = i;
        if (pc < 0) throw new Exception("Missing original PC preset.");
        var source = levels.GetArrayElementAtIndex(pc);
        var pipeline = source.FindPropertyRelative("customRenderPipeline").objectReferenceValue;
        string[] names = { "Baja", "Media", "Alta" };
        float[] bias = { .30f, .40f, .50f };
        for (int n = 0; n < names.Length; n++)
        {
            int index = -1;
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == names[n]) index = i;
            if (index < 0)
            {
                // The last original level is PC; subsequent appended levels are its copies.
                // Append to preserve all existing level/default indices.
                index = levels.arraySize;
                levels.InsertArrayElementAtIndex(index);
            }
            var level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue = names[n];
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            level.FindPropertyRelative("lodBias").floatValue = bias[n];
            level.FindPropertyRelative("globalTextureMipmapLimit").intValue = n == 0 ? 1 : 0;
            level.FindPropertyRelative("anisotropicTextures").intValue = n == 0 ? 1 : 2;
            // New desktop profiles must remain available in Standalone builds.
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            for (int i = excluded.arraySize - 1; i >= 0; i--)
                if (excluded.GetArrayElementAtIndex(i).stringValue == "Standalone") excluded.DeleteArrayElementAtIndex(i);
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string result = "COMPLETE: PASS - quality presets Baja / Media / Alta installed; original PC/Mobile and default preserved.";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/options-setup.txt", result + "\n");
        return result;
    }

    public static string Refresh()
    {
        AssetDatabase.Refresh();
        return "Asset refresh requested; compiling=" + EditorApplication.isCompiling;
    }
}
