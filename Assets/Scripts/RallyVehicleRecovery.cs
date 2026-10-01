using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Rights the player locally, without changing checkpoint or lap state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(JrsVehicleController))]
public sealed class RallyVehicleRecovery : MonoBehaviour
{
    Rigidbody body;
    JrsVehicleController controller;
    bool keyboardWasHeld;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        controller = GetComponent<JrsVehicleController>();
    }

    void Update()
    {
        bool keyboardHeld = Input.GetKey(KeyCode.T) ||
            (Keyboard.current != null && Keyboard.current.tKey.isPressed);
        bool keyboardPressed = keyboardHeld && !keyboardWasHeld;
        keyboardWasHeld = keyboardHeld;
        bool gamepadPressed = RallyGamepadInput.RecoverPressedThisFrame;
        if (keyboardPressed || gamepadPressed)
            RightVehicle();
    }

    public void RightVehicle()
    {
        if (Time.timeScale <= 0f || body.isKinematic || !controller.isActiveAndEnabled ||
            GetComponentInParent<RallyBotController>() != null)
            return;
        // Leave normal driving alone; enable recovery once tilted at least 60 degrees.
        if (Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > 0.5f)
            return;

        Vector3 heading = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.001f)
            heading = Vector3.Cross(body.rotation * Vector3.right, Vector3.up);
        Quaternion upright = Quaternion.LookRotation(heading.normalized, Vector3.up);
        Vector3 position = body.position;

        // Keep X/Z exactly. Only raise if upright tyres would intersect the ground;
        // raycasts ignore the car itself, triggers and movable props/other vehicles.
        float height = position.y;
        CheckWheelClearance(controller.frontLeftWheel, upright, position, ref height);
        CheckWheelClearance(controller.frontRightWheel, upright, position, ref height);
        CheckWheelClearance(controller.rearLeftWheel, upright, position, ref height);
        CheckWheelClearance(controller.rearRightWheel, upright, position, ref height);
        position.y = height;
        body.rotation = upright;
        body.position = position;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        Physics.SyncTransforms();
    }

    void CheckWheelClearance(WheelCollider wheel, Quaternion upright, Vector3 position, ref float height)
    {
        if (wheel == null) return;
        Vector3 offset = Quaternion.Inverse(body.rotation) *
            (wheel.transform.TransformPoint(wheel.center) - body.position);
        offset = upright * offset;
        Vector3 origin = position + offset + Vector3.up * 2f;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody != null || Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
                continue;
            float radius = wheel.radius * Mathf.Abs(wheel.transform.lossyScale.y);
            height = Mathf.Max(height, hit.point.y + radius - offset.y + 0.03f);
        }
    }
}
