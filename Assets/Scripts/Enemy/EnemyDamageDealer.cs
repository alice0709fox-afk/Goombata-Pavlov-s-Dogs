using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDamageDealer : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float damageCooldown = 0.45f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.35f;
    [SerializeField] private Vector2 attackOffset = new Vector2(0.9f, 0.15f);
    [SerializeField, Min(0.05f)] private float attackRadius = 0.45f;
    [SerializeField, Min(0f)] private float attackWindup = 0.1f;
    [SerializeField, Min(0f)] private float attackAnimationLockDuration = 0f;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string[] attackStateNames = System.Array.Empty<string>();
    [SerializeField] private bool pickAttackStateByDistance = true;
    [SerializeField, Range(0f, 1f)] private float aggressiveDistanceRatio = 0.65f;
    [SerializeField] private bool avoidImmediateAttackRepeat = true;

    public float AttackRange => attackRange;
    public bool IsAttacking => _isAttacking;

    private float _nextDamageTime;
    private EnemyController _enemyController;
    private bool _isAttacking;
    private bool _hasAttackTrigger;
    private int _attackTriggerHash;
    private int _lastAttackStateIndex = -1;
    private string[] _resolvedAttackStates = System.Array.Empty<string>();
    private int[] _resolvedAttackStateHashes = System.Array.Empty<int>();
    private EnemyRangedShooter2D _rangedShooter;

    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        _enemyController = GetComponent<EnemyController>();

        _attackTriggerHash = Animator.StringToHash(attackTriggerName);
        _hasAttackTrigger = HasTriggerParameter(animator, attackTriggerName);
        ResolveAttackStates();
        _rangedShooter = GetComponent<EnemyRangedShooter2D>();
    }

    private void Update()
    {
        if (_enemyController != null && _enemyController.IsDead)
            return;

        if (_enemyController != null && !_enemyController.CanDealDamage)
            return;

        if (_rangedShooter != null && _rangedShooter.IsShooting)
            return;

        var playerHealth = FindNearestPlayerInRange();
        if (playerHealth == null)
            return;

        FaceTarget(playerHealth.transform);

        if (_isAttacking || Time.time < _nextDamageTime)
            return;

        StartCoroutine(AttackRoutine(playerHealth));
    }

    private PlayerHealth FindNearestPlayerInRange()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        PlayerHealth nearestPlayer = null;
        var nearestDistance = attackRange;

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null || player.IsDead)
                continue;

            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearestPlayer = player;
        }

        return nearestPlayer;
    }

    private void FaceTarget(Transform targetTransform)
    {
        if (targetTransform == null || _enemyController == null)
            return;

        _enemyController.FaceTowards(targetTransform.position);
    }

    private void TriggerAttackAnimation(PlayerHealth targetPlayer)
    {
        if (animator == null)
            return;

        if (_resolvedAttackStateHashes.Length > 0)
        {
            var nextStateIndex = PickAttackStateIndex(targetPlayer);
            var stateHash = _resolvedAttackStateHashes[nextStateIndex];
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
            return;
        }

        if (!_hasAttackTrigger || string.IsNullOrWhiteSpace(attackTriggerName))
            return;

        animator.ResetTrigger(_attackTriggerHash);
        animator.SetTrigger(_attackTriggerHash);
    }

    private IEnumerator AttackRoutine(PlayerHealth targetPlayer)
    {
        _isAttacking = true;
        _nextDamageTime = Time.time + damageCooldown;

        TriggerAttackAnimation(targetPlayer);

        if (attackWindup > 0f)
            yield return new WaitForSeconds(attackWindup);

        if (_enemyController != null && (_enemyController.IsDead || !_enemyController.CanDealDamage))
        {
            _isAttacking = false;
            yield break;
        }

        DealDamageInHitArea();

        if (attackAnimationLockDuration > 0f)
            yield return new WaitForSeconds(attackAnimationLockDuration);

        _isAttacking = false;
    }

    private int PickAttackStateIndex(PlayerHealth targetPlayer)
    {
        var attackCount = _resolvedAttackStateHashes.Length;
        if (attackCount <= 1)
            return 0;

        var minIndex = 0;
        if (pickAttackStateByDistance && targetPlayer != null)
        {
            var distance = Vector2.Distance(transform.position, targetPlayer.transform.position);
            var aggressiveDistance = Mathf.Max(0.05f, attackRange * Mathf.Clamp01(aggressiveDistanceRatio));
            if (distance <= aggressiveDistance)
                minIndex = attackCount / 2;
        }

        if (minIndex >= attackCount)
            minIndex = attackCount - 1;

        var nextIndex = Random.Range(minIndex, attackCount);
        var selectableCount = attackCount - minIndex;
        if (avoidImmediateAttackRepeat && selectableCount > 1 && nextIndex == _lastAttackStateIndex)
            nextIndex = minIndex + ((nextIndex - minIndex + 1) % selectableCount);

        _lastAttackStateIndex = nextIndex;
        return nextIndex;
    }

    private void ResolveAttackStates()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        var resolved = new System.Collections.Generic.List<string>();

        for (int i = 0; i < attackStateNames.Length; i++)
        {
            var candidate = attackStateNames[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (!resolved.Contains(candidate))
                resolved.Add(candidate);
        }

        if (resolved.Count == 0)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null)
                    continue;

                if (clip.name.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!resolved.Contains(clip.name))
                    resolved.Add(clip.name);
            }
        }

        _resolvedAttackStates = resolved.ToArray();
        _resolvedAttackStateHashes = new int[_resolvedAttackStates.Length];
        for (int i = 0; i < _resolvedAttackStates.Length; i++)
            _resolvedAttackStateHashes[i] = Animator.StringToHash(_resolvedAttackStates[i]);
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

    private void DealDamageInHitArea()
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

            playerHealth.TakeDamage(damage);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(GetAttackPoint(), attackRadius);
    }
}