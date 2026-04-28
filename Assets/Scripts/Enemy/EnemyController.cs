using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour
{
    public static event System.Action<EnemyController> AnyEnemyDied;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int soulReward = 10;

    [Header("Health Bar")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.2f, 0.18f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color healthBarFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    [SerializeField] private int healthBarSortingOrder = 50;
    [SerializeField] private bool autoRaiseHealthBar = true;
    [SerializeField, Min(0f)] private float healthBarTopPadding = 0.7f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string hurtTriggerName = "Hurt";
    [SerializeField] private string deadBoolName = "Dead";
    [SerializeField] private string deadStateName = "EnemyMen_Dead";
    [SerializeField] private float deadAnimationSpeed = 0.35f;
    [SerializeField] private float destroyDelay = 0.1f;

    [Header("Hurt Feedback")]
    [SerializeField, Min(0f)] private float hurtInvulnerabilityDuration = 0.12f;
    [SerializeField, Min(0f)] private float hurtAttackLockDuration = 0.18f;
    [SerializeField] private Color hurtFlashHighlightColor = Color.white;
    [SerializeField, Min(0f)] private float hurtFlashHighlightDuration = 0.04f;
    [SerializeField] private Color hurtFlashColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField, Min(0.01f)] private float hurtFlashDuration = 0.09f;

    [Header("Death Physics")]
    [SerializeField] private bool keepColliderOnDeath;
    [SerializeField] private bool freezeRigidbodyOnDeath = true;
    [SerializeField, Min(0.05f)] private float deathGroundSnapDistance = 2f;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => _isDead;
    public bool CanDealDamage => !_isDead && Time.time >= _hurtLockedUntilTime;
    public float DeathPresentationDuration => GetDeathAnimationDuration() + Mathf.Max(0f, destroyDelay);

    public event System.Action Died;

    private float _currentHealth;
    private Transform _healthBarRoot;
    private SpriteRenderer _healthBarBackground;
    private SpriteRenderer _healthBarFill;
    private static Sprite _whiteSprite;
    private bool _isDead;
    private Collider2D _collider2D;
    private Rigidbody2D _rigidbody2D;
    private EnemyDamageDealer _damageDealer;
    private EnemyPatrolChase2D _patrolChase;
    private float _defaultAnimatorSpeed = 1f;
    private Coroutine _deactivateRoutine;
    private Coroutine _hurtFlashRoutine;
    private float _nextAllowedDamageTime;
    private float _hurtLockedUntilTime;
    private Color _defaultSpriteColor = Color.white;
    private SpriteRenderer[] _flashSpriteRenderers = System.Array.Empty<SpriteRenderer>();
    private Color[] _defaultFlashColors = System.Array.Empty<Color>();

    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            _defaultSpriteColor = spriteRenderer.color;

        CacheFlashRenderers();

        _collider2D = GetComponent<Collider2D>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _damageDealer = GetComponent<EnemyDamageDealer>();
        _patrolChase = GetComponent<EnemyPatrolChase2D>();

        // Для босса важно не отключать коллайдер на смерти, иначе объект провалится в пол.
        if (!keepColliderOnDeath && GetComponent<BossController>() != null)
            keepColliderOnDeath = true;

        if (animator != null)
            _defaultAnimatorSpeed = animator.speed;

        if (autoRaiseHealthBar && _collider2D != null)
        {
            var desiredY = _collider2D.bounds.extents.y + healthBarTopPadding;
            if (healthBarOffset.y < desiredY)
                healthBarOffset.y = desiredY;
        }

        _currentHealth = Mathf.Max(1f, maxHealth);
        EnsureHealthBar();
        UpdateHealthBar();
    }

    public void FaceTowards(Vector3 targetPosition)
    {
        if (_isDead || spriteRenderer == null)
            return;

        var deltaX = targetPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            return;

        spriteRenderer.flipX = deltaX < 0f;
    }

    private void LateUpdate()
    {
        if (_healthBarRoot != null)
            _healthBarRoot.localPosition = healthBarOffset;
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || _currentHealth <= 0f || _isDead)
            return;

        if (Time.time < _nextAllowedDamageTime)
            return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        _nextAllowedDamageTime = Time.time + hurtInvulnerabilityDuration;
        _hurtLockedUntilTime = Mathf.Max(_hurtLockedUntilTime, Time.time + hurtAttackLockDuration);

        Debug.Log($"Enemy '{name}' took {damage} damage. HP: {_currentHealth}/{maxHealth}", this);
        TriggerHurtAnimation();
        TriggerHurtFlash();
        UpdateHealthBar();

        if (_currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

        if (_hurtFlashRoutine != null)
        {
            StopCoroutine(_hurtFlashRoutine);
            _hurtFlashRoutine = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = _defaultSpriteColor;

        RestoreFlashColors();

        if (_damageDealer != null)
            _damageDealer.enabled = false;

        if (_patrolChase != null)
            _patrolChase.enabled = false;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;

            SnapToGroundOnDeath();

            if (freezeRigidbodyOnDeath)
                _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (_collider2D != null && !keepColliderOnDeath)
            _collider2D.enabled = false;

        if (animator != null)
        {
            if (!string.IsNullOrWhiteSpace(hurtTriggerName))
                animator.ResetTrigger(hurtTriggerName);

            animator.speed = Mathf.Max(0.01f, deadAnimationSpeed);

            if (!string.IsNullOrWhiteSpace(deadBoolName))
                animator.SetBool(deadBoolName, true);

            if (!string.IsNullOrWhiteSpace(deadStateName))
            {
                animator.Play(deadStateName, 0, 0f);
                animator.Update(0f);
            }
        }

        var playerSouls = FindFirstObjectByType<PlayerSouls>();
        if (playerSouls != null)
            playerSouls.AddSouls(soulReward);

#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject)
            Selection.activeObject = null;
#endif

        if (_deactivateRoutine != null)
            StopCoroutine(_deactivateRoutine);

        Died?.Invoke();
        AnyEnemyDied?.Invoke(this);
        BossVictoryPanelManager.NotifyEnemyDied(this);

        _deactivateRoutine = StartCoroutine(DeactivateAfterDeath());
    }

    private System.Collections.IEnumerator DeactivateAfterDeath()
    {
        yield return new WaitForSeconds(GetDeathAnimationDuration() + Mathf.Max(0f, destroyDelay));

#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject)
            Selection.activeObject = null;
#endif

        if (_collider2D != null)
            _collider2D.enabled = false;

        gameObject.SetActive(false);
    }

    private void EnsureHealthBar()
    {
        var root = transform.Find("HealthBarRoot");
        if (root == null)
        {
            root = new GameObject("HealthBarRoot").transform;
            root.SetParent(transform, false);
        }

        _healthBarRoot = root;
        _healthBarRoot.localPosition = healthBarOffset;

        _healthBarBackground = GetOrCreateBarPart(_healthBarRoot, "Background", healthBarBackgroundColor, healthBarSortingOrder);
        _healthBarFill = GetOrCreateBarPart(_healthBarRoot, "Fill", healthBarFillColor, healthBarSortingOrder + 1);
    }

    private SpriteRenderer GetOrCreateBarPart(Transform parent, string objectName, Color color, int sortingOrder)
    {
        var target = parent.Find(objectName);
        if (target == null)
        {
            target = new GameObject(objectName).transform;
            target.SetParent(parent, false);
        }

        var spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = target.gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        return spriteRenderer;
    }

    private void UpdateHealthBar()
    {
        if (_healthBarBackground == null || _healthBarFill == null)
            return;

        var healthPercent = Mathf.Clamp01(_currentHealth / Mathf.Max(1f, maxHealth));

        _healthBarBackground.transform.localPosition = Vector3.zero;
        _healthBarBackground.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);

        _healthBarFill.transform.localScale = new Vector3(healthBarSize.x * healthPercent, healthBarSize.y, 1f);
        _healthBarFill.transform.localPosition = new Vector3((healthBarSize.x * (healthPercent - 1f)) * 0.5f, 0f, -0.01f);
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        _whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            Texture2D.whiteTexture.width);

        return _whiteSprite;
    }

    private void TriggerHurtAnimation()
    {
        if (_isDead || animator == null || string.IsNullOrWhiteSpace(hurtTriggerName))
            return;

        animator.ResetTrigger(hurtTriggerName);
        animator.SetTrigger(hurtTriggerName);
    }

    private void TriggerHurtFlash()
    {
        if (_isDead || spriteRenderer == null)
            return;

        if (_hurtFlashRoutine != null)
            StopCoroutine(_hurtFlashRoutine);

        _hurtFlashRoutine = StartCoroutine(HurtFlashRoutine());
    }

    private System.Collections.IEnumerator HurtFlashRoutine()
    {
        for (int i = 0; i < _flashSpriteRenderers.Length; i++)
        {
            var rendererRef = _flashSpriteRenderers[i];
            if (rendererRef == null)
                continue;

            rendererRef.color = hurtFlashHighlightColor;
        }

        if (hurtFlashHighlightDuration > 0f)
            yield return new WaitForSeconds(hurtFlashHighlightDuration);

        for (int i = 0; i < _flashSpriteRenderers.Length; i++)
        {
            var rendererRef = _flashSpriteRenderers[i];
            if (rendererRef == null)
                continue;

            rendererRef.color = hurtFlashColor;
        }

        yield return new WaitForSeconds(hurtFlashDuration);

        if (!_isDead)
            RestoreFlashColors();

        _hurtFlashRoutine = null;
    }

    private void OnDisable()
    {
        if (_hurtFlashRoutine != null)
        {
            StopCoroutine(_hurtFlashRoutine);
            _hurtFlashRoutine = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = _defaultSpriteColor;

        RestoreFlashColors();
    }

    private void CacheFlashRenderers()
    {
        _flashSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _defaultFlashColors = new Color[_flashSpriteRenderers.Length];

        for (int i = 0; i < _flashSpriteRenderers.Length; i++)
        {
            var rendererRef = _flashSpriteRenderers[i];
            _defaultFlashColors[i] = rendererRef != null ? rendererRef.color : Color.white;
        }
    }

    private void RestoreFlashColors()
    {
        if (_flashSpriteRenderers == null || _defaultFlashColors == null)
            return;

        var length = Mathf.Min(_flashSpriteRenderers.Length, _defaultFlashColors.Length);
        for (int i = 0; i < length; i++)
        {
            var rendererRef = _flashSpriteRenderers[i];
            if (rendererRef == null)
                continue;

            rendererRef.color = _defaultFlashColors[i];
        }
    }

    private float GetDeathAnimationDuration()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        var clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip != null && clip.name.ToLowerInvariant().Contains("dead"))
            {
                var playbackSpeed = _isDead ? Mathf.Max(0.01f, deadAnimationSpeed) : Mathf.Max(0.01f, _defaultAnimatorSpeed);
                return clip.length / playbackSpeed;
            }
        }

        return 0f;
    }

    private void SnapToGroundOnDeath()
    {
        if (_rigidbody2D == null || _collider2D == null)
            return;

        var bounds = _collider2D.bounds;
        var origin = new Vector2(bounds.center.x, bounds.max.y + 0.05f);
        var maxDistance = bounds.size.y + deathGroundSnapDistance;
        var hits = Physics2D.RaycastAll(origin, Vector2.down, maxDistance, Physics2D.AllLayers);
        if (hits == null || hits.Length == 0)
            return;

        RaycastHit2D bestHit = default;
        var hasBestHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var hitCollider = hit.collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.gameObject == gameObject || hitCollider.isTrigger)
                continue;

            // Игрок под боссом не должен считаться "землей" для снапа смерти.
            if (hitCollider.GetComponentInParent<PlayerHealth>() != null)
                continue;

            if (!hasBestHit || hit.distance < bestHit.distance)
            {
                bestHit = hit;
                hasBestHit = true;
            }
        }

        if (!hasBestHit)
            return;

        var targetFeetY = bestHit.point.y + 0.01f;
        var offsetY = targetFeetY - bounds.min.y;
        if (Mathf.Abs(offsetY) <= 0.001f)
            return;

        _rigidbody2D.position = new Vector2(_rigidbody2D.position.x, _rigidbody2D.position.y + offsetY);
    }
}