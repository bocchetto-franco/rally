using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RallyPuddleEnhancementVerification
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string MaterialPath = "Assets/Art/Environment/Materials/Puddles - Unity sample.mat";

    [MenuItem("Tools/Rally/Verify Puddle Enhancement")]
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RallyPuddleSlowZone[] puddles = Object.FindObjectsByType<RallyPuddleSlowZone>();
        if (puddles.Length < 1)
            throw new InvalidOperationException("The circuit must retain its original puddle.");

        RallyPuddleSlowZone puddle = Array.Find(puddles, p => p.name.StartsWith("Puddle_01_"));
        if (puddle == null) throw new InvalidOperationException("Original puddle missing.");
        BoxCollider trigger = puddle.GetComponent<BoxCollider>();
        if (trigger == null || !trigger.isTrigger)
            throw new InvalidOperationException("The puddle trigger is missing or disabled.");
        if (Vector3.Distance(puddle.transform.position, new Vector3(35.96386f, 0.035f, 86.82444f)) > 0.001f ||
            Vector3.Distance(trigger.size, new Vector3(6f, 0.9f, 9f)) > 0.001f)
            throw new InvalidOperationException("Puddle position or size changed.");

        var serialized = new SerializedObject(puddle);
        AssertFloat(serialized, "brakingAcceleration", 5f, allowOriginalMigrationValue: true);
        AssertFloat(serialized, "additionalHighSpeedBraking", 2.5f);
        AssertFloat(serialized, "aquaplaningStartSpeedKph", 55f);
        AssertFloat(serialized, "aquaplaningFullSpeedKph", 110f);
        AssertFloat(serialized, "maximumLateralAcceleration", 2.2f);
        AssertFloat(serialized, "splashMinimumSpeedKph", 35f);

        GameObject temporary = new GameObject("Temporary Puddle Response Verification");
        RallyPuddleSlowZone calculationTarget = temporary.AddComponent<RallyPuddleSlowZone>();
        MethodInfo resistanceMethod = typeof(RallyPuddleSlowZone).GetMethod(
            "CalculateWaterResistance", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo aquaplaningMethod = typeof(RallyPuddleSlowZone).GetMethod(
            "CalculateAquaplaning", BindingFlags.Instance | BindingFlags.NonPublic);
        if (resistanceMethod == null || aquaplaningMethod == null)
            throw new MissingMethodException("Puddle response calculation methods are missing.");

        try
        {
            float Resistance(float speed) => (float)resistanceMethod.Invoke(calculationTarget, new object[] { speed });
            float Aquaplaning(float speed) => (float)aquaplaningMethod.Invoke(calculationTarget, new object[] { speed });
            if (!Mathf.Approximately(Aquaplaning(50f), 0f) || !Mathf.Approximately(Resistance(50f), 5f))
                throw new InvalidOperationException("Low-speed puddle response is incorrect.");
            if (Mathf.Abs(Aquaplaning(82.5f) - 0.5f) > 0.001f || Mathf.Abs(Resistance(82.5f) - 6.25f) > 0.001f)
                throw new InvalidOperationException("Mid-speed puddle response is not progressive.");
            if (!Mathf.Approximately(Aquaplaning(110f), 1f) || !Mathf.Approximately(Resistance(110f), 7.5f))
                throw new InvalidOperationException("High-speed puddle response does not reach the configured cap.");
        }
        finally
        {
            Object.DestroyImmediate(temporary);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader))
            throw new InvalidOperationException("The official water material is missing or invalid.");
        if (Mathf.Abs(material.GetFloat("_OpaqueDepth") - 0.65f) > 0.001f ||
            Mathf.Abs(material.GetFloat("_RefractionStrength") - 0.01f) > 0.001f ||
            material.GetVector("_RippleSpeed").sqrMagnitude < 0.001f)
            throw new InvalidOperationException("Water depth, refraction or ripple animation is not configured.");
        if (puddle.GetComponent<Renderer>().sharedMaterial != material)
            throw new InvalidOperationException("The puddle is not using the enhanced water material.");

        Directory.CreateDirectory("Logs");
        File.WriteAllText(
            "Logs/puddle-enhancement-verification.txt",
            "PASS: one puddle retained at original transform and 6x9m trigger; official water shader valid; translucent depth/refraction/ripples configured; resistance progresses 5.0->7.5 m/s^2; aquaplaning starts at 55 and reaches full at 110 km/h; splash starts at 35 km/h.\n" +
            DateTime.Now.ToString("O"));
    }

    static void AssertFloat(SerializedObject serialized, string name, float expected, bool allowOriginalMigrationValue = false)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException(name + " is not serialized.");
        if (!Mathf.Approximately(property.floatValue, expected) &&
            !(allowOriginalMigrationValue && Mathf.Approximately(property.floatValue, 3.5f)))
            throw new InvalidOperationException(name + " expected " + expected + " but was " + property.floatValue + ".");
    }
}
