using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        GameObject playerDynamics = GameObject.Find("Porsche Rally Dynamics");
        RallyVehicleDynamics dynamics = playerDynamics != null
            ? playerDynamics.GetComponent<RallyVehicleDynamics>() : null;
        if (dynamics == null)
            throw new InvalidOperationException("RallyVehicleDynamics is missing from Circuit_01.");

        var serialized = new SerializedObject(dynamics);
        AssertFloat(serialized, "maximumSteerAngle", 40f);
        AssertFloat(serialized, "steeringResponse", 22f);
        AssertFloat(serialized, "spinAssistYawRateThreshold", 1.25f);
        AssertFloat(serialized, "spinAssistCorrectionStrength", 7f);
        AssertFloat(serialized, "rearHandbrakeTorque", 3500f);
        AssertFloat(serialized, "handbrakeRecoveryTorqueMultiplier", 0.55f);

        // Guard the established downforce and manual-braking tune.
        AssertFloat(serialized, "downforceCoefficient", 3.2f);
        AssertFloat(serialized, "maximumDownforce", 4500f);
        AssertFloat(serialized, "frontServiceBrakeTorque", 4200f);
        AssertFloat(serialized, "rearServiceBrakeTorque", 1800f);
        AssertFloat(serialized, "frontSideExtremumValue", 1.55f);
        AssertFloat(serialized, "frontSideAsymptoteValue", 1.25f);
        AssertFloat(serialized, "rearSideExtremumValue", 1.55f);
        AssertFloat(serialized, "rearSideAsymptoteValue", 1.25f);
        AssertFloat(serialized, "rearSideAsymptoteSlip", 0.72f);
        AssertFloat(serialized, "lateralVelocityCorrection", 5f);
        AssertFloat(serialized, "automaticBrakeMaximumFraction", 0.55f);

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

        MethodInfo cornerBrake = typeof(RallyVehicleDynamics).GetMethod(
            "CalculateAutomaticBrakeAmount", BindingFlags.Instance | BindingFlags.NonPublic);
        if (cornerBrake == null)
            throw new MissingMethodException("Automatic corner braking calculation was not found.");
        float CornerBrake(float steering, float speed) =>
            (float)cornerBrake.Invoke(dynamics, new object[] { steering, speed });
        if (!Mathf.Approximately(CornerBrake(0f, 160f), 0f) ||
            !Mathf.Approximately(CornerBrake(1f, 40f), 0f) ||
            !(CornerBrake(0.5f, 130f) > 0f && CornerBrake(0.5f, 130f) < CornerBrake(1f, 130f)) ||
            !Mathf.Approximately(CornerBrake(1f, 130f), 0.55f))
            throw new InvalidOperationException("Automatic braking is not proportional to steering and excessive speed.");

        File.WriteAllText(
            "Logs/spin-assist-verification.txt",
            "PASS: anti-spin is progressive, four-wheel lateral grip is high, corner braking scales with steering and speed, handbrake recovery works, and downforce/manual brakes remain unchanged.\n" +
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
