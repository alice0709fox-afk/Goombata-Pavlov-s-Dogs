using UnityEngine;

public class NpcPatrol2D : MonoBehaviour
{
    [Header("Route")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField, Min(0.05f)] private float speed = 1.5f;
    [SerializeField, Min(0.001f)] private float arriveDistance = 0.05f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkBoolName = "Walk";

    [Header("Facing")]
    [SerializeField] private bool flipByLocalScale = true;
    [SerializeField] private bool invertFacing = false;

    [Header("Fallback Route")]
    [SerializeField, Min(0.1f)] private float fallbackHalfDistance = 1.5f;

    private Rigidbody2D _rb;
    private Vector2 _runtimePointA;
    private Vector2 _runtimePointB;
    private bool _moveToB = true;

    private int _walkHash;
    private bool _canDriveWalkAnimation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        _walkHash = Animator.StringToHash(walkBoolName);
        _canDriveWalkAnimation = animator != null && HasBoolParameter(animator, walkBoolName);

        InitializeRoute();
    }

    private void FixedUpdate()
    {
        var current = GetPosition();
        var target = _moveToB ? _runtimePointB : _runtimePointA;

        var next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);
        MoveTo(next);

        var delta = next - current;
        var isMoving = delta.sqrMagnitude > 0.000001f;

        if (_canDriveWalkAnimation)
        {
            animator.SetBool(_walkHash, isMoving);
        }

        if (Mathf.Abs(delta.x) > 0.0001f)
        {
            ApplyFacing(Mathf.Sign(delta.x));
        }

        if (Vector2.Distance(next, target) <= arriveDistance)
        {
            _moveToB = !_moveToB;
        }
    }

    private Vector2 GetPosition()
    {
        return _rb != null ? _rb.position : (Vector2)transform.position;
    }

    private void MoveTo(Vector2 next)
    {
        if (_rb != null)
        {
            _rb.MovePosition(next);
            return;
        }

        transform.position = next;
    }

    private void InitializeRoute()
    {
        if (pointA != null && pointB != null)
        {
            _runtimePointA = pointA.position;
            _runtimePointB = pointB.position;
            return;
        }

        var start = (Vector2)transform.position;
        _runtimePointA = start + Vector2.left * fallbackHalfDistance;
        _runtimePointB = start + Vector2.right * fallbackHalfDistance;
    }

    private void ApplyFacing(float xDirection)
    {
        if (!flipByLocalScale)
        {
            return;
        }

        var sign = xDirection >= 0f ? 1f : -1f;
        if (invertFacing)
        {
            sign *= -1f;
        }

        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * sign;
        transform.localScale = scale;
    }

    private static bool HasBoolParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(paramName))
        {
            return false;
        }

        foreach (var param in anim.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool && param.name == paramName)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        var a = pointA != null ? (Vector2)pointA.position : (Vector2)transform.position + Vector2.left * fallbackHalfDistance;
        var b = pointB != null ? (Vector2)pointB.position : (Vector2)transform.position + Vector2.right * fallbackHalfDistance;

        Gizmos.DrawSphere(a, 0.08f);
        Gizmos.DrawSphere(b, 0.08f);
        Gizmos.DrawLine(a, b);
    }
}
