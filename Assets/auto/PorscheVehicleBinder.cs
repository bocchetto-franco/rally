using System;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
public sealed class PorscheVehicleBinder : MonoBehaviour
{
    private const string FrontLeftVisualName = "wheel_FL";
    private const string FrontRightVisualName = "wheel_FR";
    private const string RearLeftVisualName = "wheel_BL";
    private const string RearRightVisualName = "wheel_BR";

    private void Awake()
    {
        ConfigureVehicle();
    }

    private void OnEnable()
    {
        ConfigureVehicle();
    }

    private void ConfigureVehicle()
    {
        Transform vehicleRoot = transform.parent;
        if (vehicleRoot == null)
            return;

        JrsVehicleController controller = vehicleRoot.GetComponent<JrsVehicleController>();
        if (controller == null)
            return;

        Transform frontLeftVisual = FindVisualWheel(vehicleRoot, FrontLeftVisualName);
        Transform frontRightVisual = FindVisualWheel(vehicleRoot, FrontRightVisualName);
        Transform rearLeftVisual = FindVisualWheel(vehicleRoot, RearLeftVisualName);
        Transform rearRightVisual = FindVisualWheel(vehicleRoot, RearRightVisualName);
        if (frontLeftVisual == null || frontRightVisual == null ||
            rearLeftVisual == null || rearRightVisual == null)
            return;

        WheelCollider frontLeft = FindWheelCollider(vehicleRoot, "WheelCollider_FL");
        WheelCollider frontRight = FindWheelCollider(vehicleRoot, "WheelCollider_FR");
        WheelCollider rearLeft = FindWheelCollider(vehicleRoot, "WheelCollider_BL");
        WheelCollider rearRight = FindWheelCollider(vehicleRoot, "WheelCollider_BR");
        if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            return;

        ConfigureWheel(vehicleRoot, frontLeftVisual, frontLeft);
        ConfigureWheel(vehicleRoot, frontRightVisual, frontRight);
        ConfigureWheel(vehicleRoot, rearLeftVisual, rearLeft);
        ConfigureWheel(vehicleRoot, rearRightVisual, rearRight);

        controller.frontLeftWheel = frontLeft;
        controller.frontRightWheel = frontRight;
        controller.rearLeftWheel = rearLeft;
        controller.rearRightWheel = rearRight;
        controller.frontLeftWheelTransform = frontLeftVisual;
        controller.frontRightWheelTransform = frontRightVisual;
        controller.rearLeftWheelTransform = rearLeftVisual;
        controller.rearRightWheelTransform = rearRightVisual;
        controller.wheelCollidersBrake = new[] { frontLeft, frontRight, rearLeft, rearRight };
        controller.enabled = true;

        Transform modelRoot = FindModelRoot(vehicleRoot, frontLeftVisual);
        DisablePolicePresentation(vehicleRoot, modelRoot, controller);
        FitBodyCollider(vehicleRoot, modelRoot, new[]
        {
            frontLeftVisual,
            frontRightVisual,
            rearLeftVisual,
            rearRightVisual
        }, controller);
    }

    private static Transform FindVisualWheel(Transform vehicleRoot, string exactName)
    {
        return vehicleRoot.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => string.Equals(candidate.name, exactName, StringComparison.Ordinal));
    }

    private static WheelCollider FindWheelCollider(Transform vehicleRoot, string exactName)
    {
        return vehicleRoot.GetComponentsInChildren<WheelCollider>(true)
            .FirstOrDefault(candidate => string.Equals(candidate.gameObject.name, exactName, StringComparison.Ordinal));
    }

    private static void ConfigureWheel(Transform vehicleRoot, Transform visualWheel, WheelCollider collider)
    {
        Renderer[] renderers = visualWheel.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        collider.transform.localPosition = vehicleRoot.InverseTransformPoint(bounds.center);
        float[] dimensions = { Mathf.Abs(bounds.size.x), Mathf.Abs(bounds.size.y), Mathf.Abs(bounds.size.z) };
        Array.Sort(dimensions);
        float worldRadius = (dimensions[1] + dimensions[2]) * 0.25f;
        Vector3 scale = vehicleRoot.lossyScale;
        float averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
        collider.radius = worldRadius / Mathf.Max(averageScale, 0.0001f);
        collider.center = Vector3.zero;
        collider.enabled = true;
    }

    private static Transform FindModelRoot(Transform vehicleRoot, Transform visualWheel)
    {
        Transform modelRoot = visualWheel;
        while (modelRoot.parent != null && modelRoot.parent != vehicleRoot)
            modelRoot = modelRoot.parent;
        return modelRoot;
    }

    private void DisablePolicePresentation(
        Transform vehicleRoot,
        Transform modelRoot,
        JrsVehicleController controller)
    {
        foreach (Renderer renderer in vehicleRoot.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = renderer.transform == modelRoot || renderer.transform.IsChildOf(modelRoot);

        foreach (Light light in vehicleRoot.GetComponentsInChildren<Light>(true))
            light.enabled = light.transform == modelRoot || light.transform.IsChildOf(modelRoot);

        foreach (MonoBehaviour behaviour in vehicleRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != controller && behaviour != this)
                behaviour.enabled = false;
        }

        foreach (Collider collider in vehicleRoot.GetComponentsInChildren<Collider>(true))
        {
            bool isPorscheCollider = collider.gameObject.name.StartsWith("WheelCollider_", StringComparison.Ordinal) ||
                                     collider.gameObject.name == "PorscheBodyCollider";
            collider.enabled = isPorscheCollider;
        }

        ParticleSystem[] frontDustSystems =
        {
            controller.frontLeftDustParticleSystem,
            controller.frontRightDustParticleSystem
        };
        foreach (ParticleSystem dust in frontDustSystems.Where(system => system != null))
        {
            ParticleSystemRenderer dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            if (dustRenderer != null)
                dustRenderer.enabled = false;
        }

        ParticleSystem[] rearDustSystems =
        {
            controller.rearLeftDustParticleSystem,
            controller.rearRightDustParticleSystem
        };
        foreach (ParticleSystem dust in rearDustSystems.Where(system => system != null))
        {
            ParticleSystemRenderer dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            if (dustRenderer != null)
                dustRenderer.enabled = true;
        }
    }

    private static void FitBodyCollider(
        Transform vehicleRoot,
        Transform modelRoot,
        Transform[] wheels,
        JrsVehicleController controller)
    {
        BoxCollider bodyCollider = vehicleRoot.GetComponentsInChildren<BoxCollider>(true)
            .FirstOrDefault(candidate => candidate.gameObject.name == "PorscheBodyCollider");
        if (bodyCollider == null)
            return;

        Renderer[] bodyRenderers = modelRoot.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => wheels.All(wheel => !renderer.transform.IsChildOf(wheel)))
            .ToArray();
        if (bodyRenderers.Length == 0)
            return;

        Bounds worldBounds = bodyRenderers[0].bounds;
        for (int index = 1; index < bodyRenderers.Length; index++)
            worldBounds.Encapsulate(bodyRenderers[index].bounds);

        Bounds localBounds = ConvertBoundsToLocal(vehicleRoot, worldBounds);
        bodyCollider.center = localBounds.center;
        bodyCollider.size = new Vector3(
            localBounds.size.x * 0.92f,
            localBounds.size.y * 0.78f,
            localBounds.size.z * 0.90f);

        if (controller.centerOfMassObject != null)
        {
            Vector3 centerOfMass = localBounds.center;
            centerOfMass.y = localBounds.min.y + localBounds.size.y * 0.35f;
            controller.centerOfMassObject.transform.localPosition = centerOfMass;
        }
    }

    private static Bounds ConvertBoundsToLocal(Transform localSpace, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3 first = localSpace.InverseTransformPoint(min);
        Bounds localBounds = new Bounds(first, Vector3.zero);

        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 corner = new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z);
            localBounds.Encapsulate(localSpace.InverseTransformPoint(corner));
        }

        return localBounds;
    }
}
