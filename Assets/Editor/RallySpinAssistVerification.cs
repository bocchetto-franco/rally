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
        AssertFloat(serialized, "maximumSteerAngle", 38f);
        AssertFloat(serialized, "steeringResponse", 8.5f);
        AssertFloat(serialized, "spinAssistYawRateThreshold", 1.25f);
        AssertFloat(serialized, "spinAssistCorrectionStrength", 7f);
        AssertFloat(serialized, "rearHandbrakeTorque", 3500f);
        AssertFloat(serialized, "handbrakeRecoveryTorqueMultiplier", 0.55f);

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
        float inactive = Below(1.20f);
        float onset = Below(1.5f);
        float stronger = Below(2.0f);
        float cappedPositive = Below(3.0f);
        float cappedNegative = Below(-3.0f);

        if (!Mathf.Approximately(inactive, 0f))
            throw new InvalidOperationException("Assist activates during controlled yaw below the threshold.");
        if (!(onset < 0f && Mathf.Abs(onset) < Mathf.Abs(stronger)))
            throw new InvalidOperationException("Positive-yaw correction is not opposite and progressive.");
        if (!(stronger < 0f && Mathf.Abs(stronger) < 7f))
            throw new InvalidOperationException("Progressive correction reaches full strength too early.");
        if (!Mathf.Approximately(cappedPositive, -7f) || !Mathf.Approximately(cappedNegative, 7f))
            throw new InvalidOperationException("Correction is not capped or does not oppose both yaw directions.");

        MethodInfo handbrake = typeof(RallyVehicleDynamics).GetMethod(
            "CalculateHandbrakeTorque", BindingFlags.Instance | BindingFlags.NonPublic);
        if (handbrake == null)
            throw new MissingMethodException("Handbrake yaw recovery calculation was not found.");
        float BrakeAt(float yaw) => (float)handbrake.Invoke(dynamics, new object[] { yaw });
        if (!Mathf.Approximately(BrakeAt(0f), 3500f) ||
            !(BrakeAt(1.8f) < 3500f && BrakeAt(1.8f) > 1925f) ||
            !Mathf.Approximately(BrakeAt(3f), 1925f) ||
            !Mathf.Approximately(BrakeAt(-3f), 1925f))
            throw new InvalidOperationException("Handbrake does not progressively release excessive yaw.");

        File.WriteAllText(
            "Logs/spin-assist-verification.txt",
            "PASS: assist is inactive below 1.25 rad/s, ramps progressively, opposes both yaw directions, caps at 7 rad/s^2; handbrake torque falls from 3500 to 1925 at severe yaw; existing drift/downforce values remain unchanged.\n" +
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
