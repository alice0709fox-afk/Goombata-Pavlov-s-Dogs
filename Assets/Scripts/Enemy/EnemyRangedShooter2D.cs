using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public class EnemyRangedShooter2D : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField, Min(0.5f)] private float rangedAttackRange = 14f;
    [SerializeField, Min(0.1f)] private float meleeDistanceThreshold = 1.6f;

    [Header("Shooting")]
    [SerializeField, Min(0f)] private float projectileDamage = 8f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 13f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 2.5f;
    [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.2f;
    [SerializeField] private float projectileVisualAngleOffset = -90f;
    [SerializeField, Min(0.05f)] private float shootCooldown = 1.1f;
    [SerializeField, Min(0f)] private float shootWindup = 0.14f;
    [SerializeField, Min(0f)] private float shotAnimationLockDuration = 0.2f;
    [SerializeField] private bool lockMovementWhileShooting = true;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectileTemplate;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation")]
    [SerializeField] private string shotStateName = "Raider2_Shot";

    public bool IsShooting => _isShooting;
    public bool LockMovementWhileShooting => lockMovementWhileShooting && _isShooting;
    public float MeleeDistanceThreshold => Mathf.Max(0.1f, meleeDistanceThreshold);

    public void SetRuntimeProjectileDamage(float newDamage)
    {
        projectileDamage = Mathf.Max(0f, newDamage);
    }

    private EnemyController _enemyController;
    private EnemyDamageDealer _meleeDealer;
    private bool _isShooting;
    private float _nextShootTime;
    private int _shotStateHash;

    private void Awake()
    {
        _enemyController = GetComponent<EnemyController>();
        _meleeDealer = GetComponent<EnemyDamageDealer>();
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();

        if (firePoint == null)
        {
            var child = transform.Find("Bullet");
            if (child != null)
                firePoint = child;
        }

        if (projectileTemplate == null && firePoint != null)
            projectileTemplate = firePoint.gameObject;

        if (projectileTemplate != null)
            projectileTemplate.SetActive(false);

        _shotStateHash = string.IsNullOrWhiteSpace(shotStateName) ? 0 : Animator.StringToHash(shotStateName);
    }

    private void Update()
    {
        if (_enemyController != null && _enemyController.IsDead)
            return;

        if (_enemyController != null && !_enemyController.CanDealDamage)
            return;

        if (_isShooting || Time.time < _nextShootTime)
            return;

        if (_meleeDealer != null && _meleeDealer.IsAttacking)
            return;

        var target = FindNearestPlayerInRange(rangedAttackRange);
        if (target == null)
            return;

        var distance = Vector2.Distance(transform.position, target.transform.position);
        if (distance <= meleeDistanceThreshold)
            return;

        FaceTarget(target.transform.position);
        StartCoroutine(ShootRoutine(target.transform));
    }

    private PlayerHealth FindNearestPlayerInRange(float range)
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        PlayerHealth nearest = null;
        var nearestDistance = range;

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null || player.IsDead)
                continue;

            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = player;
        }

        return nearest;
    }

    private IEnumerator ShootRoutine(Transform target)
    {
        _isShooting = true;
        _nextShootTime = Time.time + shootCooldown;

        TriggerShotAnimation();

        if (shootWindup > 0f)
            yield return new WaitForSeconds(shootWindup);

        if (_enemyController == null || (!_enemyController.IsDead && _enemyController.CanDealDamage))
            ShootProjectile(target);

        if (shotAnimationLockDuration > 0f)
            yield return new WaitForSeconds(shotAnimationLockDuration);

        _isShooting = false;
    }

    private void ShootProjectile(Transform target)
    {
        if (firePoint == null)
            return;

        var direction = target != null
            ? ((Vector2)target.position - (Vector2)firePoint.position).normalized
            : GetFacingDirection();

        if (direction.sqrMagnitude <= 0.0001f)
            direction = GetFacingDirection();

        var projectileObject = CreateProjectileObject();
        if (projectileObject == null)
            return;

        var spawnPosition = (Vector2)firePoint.position + direction * projectileSpawnOffset;
        projectileObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        projectileObject.SetActive(true);

        var projectile = projectileObject.GetComponent<EnemyProjectile2D>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<EnemyProjectile2D>();

        projectile.Launch(direction, projectileDamage, projectileSpeed, projectileLifetime, gameObject);

        var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + projectileVisualAngleOffset;
        projectileObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private GameObject CreateProjectileObject()
    {
        if (projectileTemplate != null)
        {
            // Clone the visual template but strip player-bullet behavior.
            var templateClone = Instantiate(projectileTemplate);
            var playerBullet = templateClone.GetComponent<Bullet>();
            if (playerBullet != null)
            {
                playerBullet.enabled = false;
                Destroy(playerBullet);
            }

            if (templateClone.GetComponent<EnemyProjectile2D>() == null)
                templateClone.AddComponent<EnemyProjectile2D>();

            var trails = templateClone.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                Destroy(trails[i]);

            return templateClone;
        }

        // Fallback when template is missing: create a minimal visible projectile.
        var runtimeProjectile = new GameObject("EnemyProjectile");

        var spriteRenderer = runtimeProjectile.AddComponent<SpriteRenderer>();
        if (this.spriteRenderer != null)
        {
            spriteRenderer.sprite = this.spriteRenderer.sprite;
            spriteRenderer.material = this.spriteRenderer.sharedMaterial;
        }
        spriteRenderer.sortingLayerID = this.spriteRenderer != null ? this.spriteRenderer.sortingLayerID : 0;
        spriteRenderer.sortingOrder = (this.spriteRenderer != null ? this.spriteRenderer.sortingOrder : 0) + 1;
        runtimeProjectile.transform.localScale = Vector3.one * 0.2f;

        var collider = runtimeProjectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.15f;

        var body = runtimeProjectile.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        runtimeProjectile.AddComponent<EnemyProjectile2D>();
        return runtimeProjectile;
    }

    private void TriggerShotAnimation()
    {
        if (animator == null)
            return;

        if (_shotStateHash != 0)
        {
            animator.Play(_shotStateHash, 0, 0f);
            animator.Update(0f);
        }
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        if (_enemyController != null)
        {
            _enemyController.FaceTowards(targetPosition);
            return;
        }

        if (spriteRenderer == null)
            return;

        var deltaX = targetPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) > 0.01f)
            spriteRenderer.flipX = deltaX < 0f;
    }

    private Vector2 GetFacingDirection()
    {
        if (spriteRenderer != null)
            return spriteRenderer.flipX ? Vector2.left : Vector2.right;

        return transform.right;
    }
}
