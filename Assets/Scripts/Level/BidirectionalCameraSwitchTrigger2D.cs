using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class BidirectionalCameraSwitchTrigger2D : MonoBehaviour
{
    private enum SwitchAxis
    {
        Horizontal,
        Vertical
    }

    [Header("Cameras")]
    [SerializeField] private CinemachineVirtualCameraBase negativeSideCamera;
    [SerializeField] private CinemachineVirtualCameraBase positiveSideCamera;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    [Header("Direction")]
    [SerializeField] private SwitchAxis axis = SwitchAxis.Horizontal;
    [SerializeField, Min(0f)] private float minVelocityForDirection = 0.05f;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float switchCooldown = 0.15f;

    private Collider2D _trigger;
    private float _lastSwitchTime = -999f;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        _trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (Time.time - _lastSwitchTime < switchCooldown)
        {
            return;
        }

        if (negativeSideCamera == null || positiveSideCamera == null)
        {
            return;
        }

        var axisVector = axis == SwitchAxis.Horizontal ? Vector2.right : Vector2.up;
        var playerPosition = other.bounds.center;
        var triggerPosition = _trigger.bounds.center;

        var velocity = Vector2.zero;
        var body = other.attachedRigidbody;
        if (body != null)
        {
            velocity = body.linearVelocity;
        }

        var velocityAlongAxis = Vector2.Dot(velocity, axisVector);
        bool movingToPositive;

        if (Mathf.Abs(velocityAlongAxis) >= minVelocityForDirection)
        {
            movingToPositive = velocityAlongAxis > 0f;
        }
        else
        {
            var relativeAlongAxis = Vector2.Dot((Vector2)playerPosition - (Vector2)triggerPosition, axisVector);
            movingToPositive = relativeAlongAxis < 0f;
        }

        ApplySwitch(movingToPositive ? positiveSideCamera : negativeSideCamera);
        _lastSwitchTime = Time.time;
    }

    private void ApplySwitch(CinemachineVirtualCameraBase activeCamera)
    {
        if (activeCamera == positiveSideCamera)
        {
            positiveSideCamera.Priority = activePriority;
            negativeSideCamera.Priority = inactivePriority;
            return;
        }

        negativeSideCamera.Priority = activePriority;
        positiveSideCamera.Priority = inactivePriority;
    }
}
