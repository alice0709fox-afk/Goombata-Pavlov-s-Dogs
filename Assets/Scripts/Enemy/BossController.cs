using System.Collections;
using UnityEngine;

/// <summary>
/// AI босса: патруль по точкам, агр и погоня за игроком, прыжки в сторону цели.
/// Требует Rigidbody2D и EnemyController.
/// Компонент включается через BossRoomTrigger после кат-сцены.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyController))]
public class BossController : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] platformPoints;
    [SerializeField, Min(0.1f)] private float walkSpeed = 2f;
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.2f;

    [Header("Chase")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float detectionRange = 9f;
    [SerializeField, Min(0.1f)] private float loseTargetRange = 12f;
    [SerializeField, Min(0f)] private float stoppingDistance = 1.2f;

    [Header("Jump")]
    [SerializeField, Min(1f)] private float jumpForceX = 4f;
    [SerializeField, Min(1f)] private float jumpForceY = 8f;
    [SerializeField, Min(0.1f)] private float jumpCooldown = 1.1f;
    [SerializeField, Min(0f)] private float minJumpXDistance = 1.5f;
    [SerializeField, Min(0f)] private float chaseJumpDistance = 4f;
    [SerializeField, Min(0f)] private float minJumpTargetHeight = 0.35f;
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 0.15f;
    [SerializeField, Min(0f)] private float groundSnapDistance = 0.2f;
    [SerializeField, Min(0f)] private float maxGroundSnapLift = 0.08f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkBoolName = "Walk";
    [SerializeField] private string jumpBoolName = "Jump";
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Built-in Attack Fallback")]
    [SerializeField] private bool enableBuiltInAttack = true;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField] private Vector2 attackOffset = new Vector2(0.9f, 0.2f);
    [SerializeField, Min(0.05f)] private float attackRadius = 0.5f;
    [SerializeField, Min(0f)] private float attackWindup = 0.1f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.9f;
    [SerializeField, Min(1f)] private float attackDamage = 12f;

    private Rigidbody2D _rb;
    private Collider2D _collider2D;
    private EnemyController _enemyController;
    private EnemyDamageDealer _damageDealer;
    private Transform _currentTarget;
    private int _patrolTargetIndex;
    private float _nextJumpTime;
    private float _nextAttackTime;
    private bool _isAttacking;
    private int _walkHash;
    private int _jumpHash;
    private int _attackTriggerHash;
    private bool _canAnimate;
    private bool _canDriveWalkAnimation;
    private bool _canDriveJumpAnimation;
    private bool _canDriveAttackAnimation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        if (_rb.bodyType != RigidbodyType2D.Dynamic)
            _rb.bodyType = RigidbodyType2D.Dynamic;

        _enemyController = GetComponent<EnemyController>();
        _damageDealer = GetComponent<EnemyDamageDealer>();
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();

        walkBoolName = ResolveBoolParameterName(animator, walkBoolName, "Walk", "Run", "Move", "IsWalking");
        jumpBoolName = ResolveBoolParameterName(animator, jumpBoolName, "Jump", "IsJump", "InAir", "Airborne");
        attackTriggerName = ResolveTriggerParameterName(animator, attackTriggerName, "Attack", "Hit", "Strike");

        _walkHash = Animator.StringToHash(walkBoolName);
        _jumpHash = Animator.StringToHash(jumpBoolName);
        _attackTriggerHash = Animator.StringToHash(attackTriggerName);
        _canAnimate = animator != null;
        _canDriveWalkAnimation = _canAnimate && HasBoolParameter(animator, walkBoolName);
        _canDriveJumpAnimation = _canAnimate && HasBoolParameter(animator, jumpBoolName);
        _canDriveAttackAnimation = _canAnimate && HasTriggerParameter(animator, attackTriggerName);

        if (platformPoints == null || platformPoints.Length == 0)
            Debug.LogWarning("[BossController] platformPoints не назначены. Босс не будет двигаться.");
    }

    private void OnEnable()
    {
        _patrolTargetIndex = 0;
        _currentTarget = null;
        _nextJumpTime = Time.time + 0.25f;
        _nextAttackTime = Time.time + 0.2f;
        _isAttacking = false;
    }

    private void FixedUpdate()
    {
        if (_enemyController != null && _enemyController.IsDead)
        {
            SetHorizontalVelocity(0f);
            _isAttacking = false;
            SetWalk(false);
            return;
        }

        RefreshTarget();

        var grounded = IsGrounded();
        TrySnapUpFromGround(grounded);
        SetJumpAnimation(!grounded);

        if (IsInAttackAnimation())
        {
            SetHorizontalVelocity(0f);
            SetWalk(false);
            return;
        }

        var position = _rb.position;
        var targetPosition = GetDesiredTargetPosition(position);
        if (targetPosition == null)
        {
            SetHorizontalVelocity(0f);
            SetWalk(false);
            return;
        }

        var delta = targetPosition.Value - position;
        var absX = Mathf.Abs(delta.x);
        var direction = absX > 0.01f ? Mathf.Sign(delta.x) : 0f;

        var moveSpeed = _currentTarget != null ? chaseSpeed : walkSpeed;
        var minStopDistance = _currentTarget != null ? Mathf.Min(GetStopDistance(), 0.2f) : arriveDistance;
        var shouldMove = absX > minStopDistance;

        if (ShouldStartBuiltInAttack())
        {
            SetHorizontalVelocity(0f);
            SetWalk(false);

            if (!_isAttacking)
                StartCoroutine(BuiltInAttackRoutine());

            return;
        }

        var horizontalSpeed = shouldMove ? direction * moveSpeed : 0f;
        SetHorizontalVelocity(horizontalSpeed);

        if (direction != 0f)
            _enemyController.FaceTowards(transform.position + Vector3.right * direction);

        SetWalk(Mathf.Abs(horizontalSpeed) > 0.01f);

        if (grounded && Time.time >= _nextJumpTime && ShouldJump(delta, absX))
            Jump(direction);
    }

    private bool IsGrounded()
    {
        // Если слой указан неверно, используем fallback-проверку по всем слоям.
        var bounds = _collider2D != null ? _collider2D.bounds : default;
        var hasBounds = _collider2D != null;
        var feetY = hasBounds ? bounds.min.y + 0.02f : _rb.position.y;
        var origin = new Vector2(_rb.position.x, feetY);
        var distance = groundCheckDistance + 0.2f;

        int configuredMask = groundLayer.value;
        if (configuredMask != 0)
        {
            var configuredHit = Physics2D.Raycast(origin, Vector2.down, distance, configuredMask);
            if (configuredHit.collider != null && configuredHit.collider.gameObject != gameObject)
                return true;
        }

        var fallbackHit = Physics2D.Raycast(origin, Vector2.down, distance, Physics2D.AllLayers);
        return fallbackHit.collider != null && fallbackHit.collider.gameObject != gameObject;
    }

    private void TrySnapUpFromGround(bool grounded)
    {
        if (!grounded || _collider2D == null || maxGroundSnapLift <= 0f)
            return;

        var bounds = _collider2D.bounds;
        var origin = new Vector2(_rb.position.x, bounds.min.y + Mathf.Max(0.02f, groundSnapDistance));
        var castDistance = Mathf.Max(0.04f, groundSnapDistance * 2f);
        var mask = groundLayer.value != 0 ? groundLayer.value : Physics2D.AllLayers;
        var hit = Physics2D.Raycast(origin, Vector2.down, castDistance, mask);

        if (hit.collider == null || hit.collider.gameObject == gameObject)
            return;

        var desiredFeetY = hit.point.y + 0.01f;
        var lift = desiredFeetY - bounds.min.y;
        if (lift <= 0.001f || lift > maxGroundSnapLift)
            return;

        _rb.MovePosition(new Vector2(_rb.position.x, _rb.position.y + lift));
    }

    private void RefreshTarget()
    {
        if (_currentTarget != null)
        {
            var currentHealth = _currentTarget.GetComponent<PlayerHealth>() ?? _currentTarget.GetComponentInParent<PlayerHealth>();
            var currentDistance = Vector2.Distance(transform.position, _currentTarget.position);
            if (currentHealth != null && !currentHealth.IsDead && currentDistance <= loseTargetRange)
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

    private Vector2? GetDesiredTargetPosition(Vector2 currentPosition)
    {
        if (_currentTarget != null)
            return _currentTarget.position;

        return GetPatrolTarget(currentPosition);
    }

    private Vector2? GetPatrolTarget(Vector2 currentPosition)
    {
        if (platformPoints == null || platformPoints.Length == 0)
            return null;

        var safeIndex = Mathf.Clamp(_patrolTargetIndex, 0, platformPoints.Length - 1);
        if (platformPoints[safeIndex] == null)
        {
            safeIndex = GetNearestValidPatrolIndex(currentPosition);
            if (safeIndex < 0)
                return null;
            _patrolTargetIndex = safeIndex;
        }

        var target = platformPoints[_patrolTargetIndex];
        if (target == null)
            return null;

        if (Mathf.Abs(target.position.x - currentPosition.x) <= arriveDistance)
            _patrolTargetIndex = GetNextValidPatrolIndex(_patrolTargetIndex);

        target = platformPoints[_patrolTargetIndex];
        return target != null ? (Vector2)target.position : null;
    }

    private int GetNearestValidPatrolIndex(Vector2 currentPosition)
    {
        if (platformPoints == null || platformPoints.Length == 0)
            return -1;

        var nearestIndex = -1;
        var nearestDistance = float.MaxValue;

        for (int i = 0; i < platformPoints.Length; i++)
        {
            var point = platformPoints[i];
            if (point == null)
                continue;

            var distance = Mathf.Abs(point.position.x - currentPosition.x);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestIndex = i;
        }

        return nearestIndex;
    }

    private int GetNextValidPatrolIndex(int fromIndex)
    {
        if (platformPoints == null || platformPoints.Length == 0)
            return 0;

        for (int offset = 1; offset <= platformPoints.Length; offset++)
        {
            var index = (fromIndex + offset) % platformPoints.Length;
            if (platformPoints[index] != null)
                return index;
        }

        return fromIndex;
    }

    private bool ShouldJump(Vector2 delta, float absX)
    {
        if (absX < minJumpXDistance)
            return false;

        if (_currentTarget != null)
            return delta.y > minJumpTargetHeight || absX >= chaseJumpDistance;

        return Mathf.Abs(delta.y) > minJumpTargetHeight;
    }

    private void Jump(float direction)
    {
        if (direction == 0f)
            return;

        _nextJumpTime = Time.time + jumpCooldown;
        _rb.linearVelocity = new Vector2(direction * jumpForceX, jumpForceY);
        SetWalk(false);
    }

    private bool ShouldStartBuiltInAttack()
    {
        if (!enableBuiltInAttack || _damageDealer != null || _isAttacking || _currentTarget == null)
            return false;

        if (Time.time < _nextAttackTime)
            return false;

        var playerHealth = _currentTarget.GetComponent<PlayerHealth>() ?? _currentTarget.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
            return false;

        var distance = Vector2.Distance(transform.position, _currentTarget.position);
        return distance <= attackRange;
    }

    private IEnumerator BuiltInAttackRoutine()
    {
        _isAttacking = true;
        _nextAttackTime = Time.time + attackCooldown;
        TriggerAttackAnimation();

        if (attackWindup > 0f)
            yield return new WaitForSeconds(attackWindup);

        DealBuiltInAttackDamage();
        _isAttacking = false;
    }

    private void TriggerAttackAnimation()
    {
        if (!_canDriveAttackAnimation)
            return;

        animator.ResetTrigger(_attackTriggerHash);
        animator.SetTrigger(_attackTriggerHash);
    }

    private void DealBuiltInAttackDamage()
    {
        var colliders = Physics2D.OverlapCircleAll(GetAttackPoint(), attackRadius);
        for (int i = 0; i < colliders.Length; i++)
        {
            var hitCollider = colliders[i];
            if (hitCollider == null)
                continue;

            var playerHealth = hitCollider.GetComponent<PlayerHealth>() ?? hitCollider.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || playerHealth.IsDead)
                continue;

            playerHealth.TakeDamage(attackDamage);
            break;
        }
    }

    private Vector2 GetAttackPoint()
    {
        var facingSign = 1f;
        if (spriteRenderer != null && spriteRenderer.flipX)
            facingSign = -1f;

        return (Vector2)transform.position + new Vector2(attackOffset.x * facingSign, attackOffset.y);
    }

    private void SetHorizontalVelocity(float value)
    {
        if (_rb.bodyType == RigidbodyType2D.Dynamic)
        {
            var velocity = _rb.linearVelocity;
            velocity.x = value;
            _rb.linearVelocity = velocity;
            return;
        }

        var delta = value * Time.fixedDeltaTime;
        _rb.MovePosition(new Vector2(_rb.position.x + delta, _rb.position.y));
    }

    private float GetStopDistance()
    {
        if (_damageDealer == null)
            return stoppingDistance;

        return Mathf.Max(stoppingDistance, _damageDealer.AttackRange * 0.85f);
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

    private static bool HasTriggerParameter(Animator targetAnimator, string paramName)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(paramName))
            return false;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == paramName)
                return true;
        }

        return false;
    }

    private static string ResolveBoolParameterName(Animator targetAnimator, string preferredName, params string[] fallbacks)
    {
        if (HasBoolParameter(targetAnimator, preferredName))
            return preferredName;

        for (int i = 0; i < fallbacks.Length; i++)
        {
            if (HasBoolParameter(targetAnimator, fallbacks[i]))
                return fallbacks[i];
        }

        return preferredName;
    }

    private static string ResolveTriggerParameterName(Animator targetAnimator, string preferredName, params string[] fallbacks)
    {
        if (HasTriggerParameter(targetAnimator, preferredName))
            return preferredName;

        for (int i = 0; i < fallbacks.Length; i++)
        {
            if (HasTriggerParameter(targetAnimator, fallbacks[i]))
                return fallbacks[i];
        }

        return preferredName;
    }

    private void SetWalk(bool walking)
    {
        if (!_canAnimate || !_canDriveWalkAnimation)
            return;

        animator.SetBool(_walkHash, walking);
    }

    private void SetJumpAnimation(bool jumping)
    {
        if (!_canAnimate || !_canDriveJumpAnimation)
            return;

        animator.SetBool(_jumpHash, jumping);
    }

    private bool IsInAttackAnimation()
    {
        if (animator == null)
            return false;

        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Men3_Attack") || state.IsTag("Attack");
    }

    private void OnDrawGizmosSelected()
    {
        if (platformPoints == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < platformPoints.Length; i++)
        {
            if (platformPoints[i] == null)
                continue;
            Gizmos.DrawSphere(platformPoints[i].position, 0.15f);
            if (i > 0 && platformPoints[i - 1] != null)
                Gizmos.DrawLine(platformPoints[i - 1].position, platformPoints[i].position);
        }

        // Ground check ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);

        // Built-in attack area fallback
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(GetAttackPoint(), attackRadius);
    }
}
