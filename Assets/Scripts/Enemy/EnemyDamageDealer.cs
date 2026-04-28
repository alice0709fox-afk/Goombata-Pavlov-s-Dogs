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
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string attackTriggerName = "Attack";

    public float AttackRange => attackRange;

    private float _nextDamageTime;
    private EnemyController _enemyController;
    private bool _isAttacking;

    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        _enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (_enemyController != null && _enemyController.IsDead)
            return;

        if (_enemyController != null && !_enemyController.CanDealDamage)
            return;

        var playerHealth = FindNearestPlayerInRange();
        if (playerHealth == null)
            return;

        FaceTarget(playerHealth.transform);

        if (_isAttacking || Time.time < _nextDamageTime)
            return;

        StartCoroutine(AttackRoutine());
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

    private void TriggerAttackAnimation()
    {
        if (animator == null || string.IsNullOrWhiteSpace(attackTriggerName))
            return;

        animator.ResetTrigger(attackTriggerName);
        animator.SetTrigger(attackTriggerName);
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _nextDamageTime = Time.time + damageCooldown;

        TriggerAttackAnimation();

        if (attackWindup > 0f)
            yield return new WaitForSeconds(attackWindup);

        if (_enemyController != null && (_enemyController.IsDead || !_enemyController.CanDealDamage))
        {
            _isAttacking = false;
            yield break;
        }

        DealDamageInHitArea();
        _isAttacking = false;
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