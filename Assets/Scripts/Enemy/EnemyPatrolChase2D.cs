using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public class EnemyPatrolChase2D : MonoBehaviour
{
    [Header("Route")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField, Min(0.05f)] private float patrolSpeed = 1.5f;
    [SerializeField, Min(0.05f)] private float chaseSpeed = 2.25f;
    [SerializeField, Min(0.01f)] private float arriveDistance = 0.05f;

    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float detectionRange = 5f;
    [SerializeField, Min(0.1f)] private float loseTargetRange = 7f;
    [SerializeField, Min(0f)] private float stoppingDistance = 0.9f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkBoolName = "Walk";

    [Header("Fallback Route")]
    [SerializeField, Min(0.1f)] private float fallbackHalfDistance = 2f;

    private EnemyController _enemyController;
    private EnemyDamageDealer _damageDealer;
    private Rigidbody2D _rigidbody2D;
    private Transform _currentTarget;
    private Vector2 _patrolPointA;
    private Vector2 _patrolPointB;
    private bool _moveToB = true;
    private int _walkHash;
    private bool _canDriveWalkAnimation;

    private void Awake()
    {
        _enemyController = GetComponent<EnemyController>();
        _damageDealer = GetComponent<EnemyDamageDealer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        animator = animator != null ? animator : GetComponent<Animator>();

        _walkHash = Animator.StringToHash(walkBoolName);
        _canDriveWalkAnimation = animator != null && HasBoolParameter(animator, walkBoolName);

        InitializePatrolPoints();
    }

    private void FixedUpdate()
    {
        if (_enemyController != null && _enemyController.IsDead)
        {
            SetWalkAnimation(false);
            return;
        }

        RefreshTarget();

        var currentPosition = GetPosition();
        var targetPosition = GetDesiredPosition(currentPosition, out var moveSpeed, out var isPatrolling);
        var nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.fixedDeltaTime);
        var delta = nextPosition - currentPosition;
        var isMoving = delta.sqrMagnitude > 0.000001f;

        MoveTo(nextPosition);
        SetWalkAnimation(isMoving);

        if (Mathf.Abs(delta.x) > 0.0001f)
            FaceDirection(delta.x);
        else if (_currentTarget != null)
            FaceDirection(_currentTarget.position.x - transform.position.x);

        if (isPatrolling && Vector2.Distance(nextPosition, targetPosition) <= arriveDistance)
            _moveToB = !_moveToB;
    }

    private Vector2 GetPosition()
    {
        return _rigidbody2D != null ? _rigidbody2D.position : (Vector2)transform.position;
    }

    private void MoveTo(Vector2 nextPosition)
    {
        if (_rigidbody2D != null)
        {
            _rigidbody2D.MovePosition(nextPosition);
            return;
        }

        transform.position = nextPosition;
    }

    private Vector2 GetDesiredPosition(Vector2 currentPosition, out float moveSpeed, out bool isPatrolling)
    {
        if (_currentTarget != null)
        {
            isPatrolling = false;
            moveSpeed = chaseSpeed;

            var targetX = _currentTarget.position.x;
            var stopDistance = GetStopDistance();
            if (Mathf.Abs(targetX - currentPosition.x) <= stopDistance)
                return currentPosition;

            return new Vector2(targetX, currentPosition.y);
        }

        isPatrolling = true;
        moveSpeed = patrolSpeed;
        return _moveToB ? GetPointBPosition() : GetPointAPosition();
    }

    private void RefreshTarget()
    {
        if (_currentTarget != null)
        {
            var distanceToCurrent = Vector2.Distance(transform.position, _currentTarget.position);
            var currentHealth = _currentTarget.GetComponent<PlayerHealth>() ?? _currentTarget.GetComponentInParent<PlayerHealth>();
            if (currentHealth != null && !currentHealth.IsDead && distanceToCurrent <= loseTargetRange)
                return;

            _currentTarget = null;
        }

        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        var closestDistance = detectionRange;

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null || player.IsDead)
                continue;

            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > closestDistance)
                continue;

            closestDistance = distance;
            _currentTarget = player.transform;
        }
    }

    private Vector2 GetPointAPosition()
    {
        return _patrolPointA;
    }

    private Vector2 GetPointBPosition()
    {
        return _patrolPointB;
    }

    private void InitializePatrolPoints()
    {
        if (pointA != null && pointB != null)
        {
            _patrolPointA = pointA.position;
            _patrolPointB = pointB.position;
            return;
        }

        var start = (Vector2)transform.position;
        _patrolPointA = start + Vector2.left * fallbackHalfDistance;
        _patrolPointB = start + Vector2.right * fallbackHalfDistance;
    }

    private void FaceDirection(float directionX)
    {
        if (Mathf.Abs(directionX) <= 0.0001f || _enemyController == null)
            return;

        _enemyController.FaceTowards(transform.position + Vector3.right * directionX);
    }

    private float GetStopDistance()
    {
        if (_damageDealer == null)
            return stoppingDistance;

        return Mathf.Max(stoppingDistance, _damageDealer.AttackRange * 0.85f);
    }

    private void SetWalkAnimation(bool isWalking)
    {
        if (!_canDriveWalkAnimation)
            return;

        animator.SetBool(_walkHash, isWalking);
    }

    private static bool HasBoolParameter(Animator targetAnimator, string paramName)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(paramName))
            return false;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == paramName)
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        var a = pointA != null ? (Vector2)pointA.position : (Vector2)transform.position + Vector2.left * fallbackHalfDistance;
        var b = pointB != null ? (Vector2)pointB.position : (Vector2)transform.position + Vector2.right * fallbackHalfDistance;

        Gizmos.DrawSphere(a, 0.08f);
        Gizmos.DrawSphere(b, 0.08f);
        Gizmos.DrawLine(a, b);

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, loseTargetRange);
    }
}