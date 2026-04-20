using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDamageDealer : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float damageCooldown = 0.35f;
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "Attack";

    private float _nextDamageTime;
    private EnemyController _enemyController;

    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        _enemyController = GetComponent<EnemyController>();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
            return;

        TryDealDamage(collision.collider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider2D target)
    {
        if (target == null)
            return;

        if (_enemyController != null && _enemyController.IsDead)
            return;

        var playerHealth = target.GetComponent<PlayerHealth>() ?? target.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        FaceTarget(playerHealth.transform);

        if (Time.time < _nextDamageTime)
            return;

        playerHealth.TakeDamage(damage);
        TriggerAttackAnimation();
        _nextDamageTime = Time.time + damageCooldown;
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
}