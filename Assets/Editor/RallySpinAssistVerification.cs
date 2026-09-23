using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RallySpinAssistVerification
{
    private const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    private const string RequestPath = "Logs/spin-assist-verify-request.txt";

    static RallySpinAssistVerification()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;

        File.Move(RequestPath, RequestPath + ".consumed-" + DateTime.Now.Ticks);
        try
        {
            Verify();
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/spin-assist-verify-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Rally/Verify Spin Stability Assist")]
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RallyVehicleDynamics dynamics = Object.FindAnyObjectByType<RallyVehicleDynamics>();
        if (dynamics == null)
            throw new InvalidOperationException("RallyVehicleDynamics is missing from Circuit_01.");

        var serialized = new SerializedObject(dynamics);
        AssertFloat(serialized, "spinAssistYawRateThreshold", 1.75f);
        AssertFloat(serialized, "spinAssistCorrectionStrength", 4.5f);

        // Guard the established tune: this feature must not alter it.
        AssertFloat(serialized, "downforceCoefficient", 3.2f);
        AssertFloat(serialized, "maximumDownforce", 4500f);
        AssertFloat(serialized, "frontServiceBrakeTorque", 4200f);
        AssertFloat(serialized, "rearServiceBrakeTorque", 1800f);
        AssertFloat(serialized, "rearSideExtremumValue", 0.405f);
        AssertFloat(serialized, "rearSideAsymptoteValue", 0.135f);
        AssertFloat(serialized, "rearSideAsymptoteSlip", 0.72f);

        MethodInfo calculate = typeof(RallyVehicleDynamics).GetMethod(
            "CalculateSpinAssistAngularAcceleration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (calculate == null)
            throw new MissingMethodException("Spin assistance calculation method was not found.");

        float Below(float yaw) => (float)calculate.Invoke(dynamics, new object[] { yaw });
        float inactive = Below(1.70f);
        float onset = Below(2.0f);
        float stronger = Below(2.75f);
        float cappedPositive = Below(4.0f);
        float cappedNegative = Below(-4.0f);

        if (!Mathf.Approximately(inactive, 0f))
            throw new InvalidOperationException("Assist activates during controlled yaw below the threshold.");
        if (!(onset < 0f && Mathf.Abs(onset) < Mathf.Abs(stronger)))
            throw new InvalidOperationException("Positive-yaw correction is not opposite and progressive.");
        if (!(stronger < 0f && Mathf.Abs(stronger) < 4.5f))
            throw new InvalidOperationException("Progressive correction reaches full strength too early.");
        if (!Mathf.Approximately(cappedPositive, -4.5f) || !Mathf.Approximately(cappedNegative, 4.5f))
            throw new InvalidOperationException("Correction is not capped or does not oppose both yaw directions.");

        File.WriteAllText(
            "Logs/spin-assist-verification.txt",
            "PASS: assist is inactive below 1.75 rad/s, ramps progressively, opposes both yaw directions, caps at 4.5 rad/s^2, and existing drift/brake/downforce values remain unchanged.\n" +
            DateTime.Now.ToString("O"));
        if (File.Exists("Logs/spin-assist-verify-error.txt"))
            File.Delete("Logs/spin-assist-verify-error.txt");
    }

    private static void AssertFloat(SerializedObject serialized, string propertyName, float expected)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !Mathf.Approximately(property.floatValue, expected))
            throw new InvalidOperationException(
                propertyName + " expected " + expected + " but was " +
                (property == null ? "missing" : property.floatValue.ToString("0.###")) + ".");
    }
}
