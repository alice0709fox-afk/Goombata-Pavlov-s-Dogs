using UnityEngine;

[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int soulReward = 10;

    [Header("Health Bar")]
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(1.2f, 0.18f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color healthBarFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    [SerializeField] private int healthBarSortingOrder = 50;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string hurtTriggerName = "Hurt";
    [SerializeField] private string deadBoolName = "Dead";
    [SerializeField] private string deadStateName = "EnemyMen_Dead";
    [SerializeField] private float deadAnimationSpeed = 0.35f;
    [SerializeField] private float destroyDelay = 0.1f;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => _isDead;

    private float _currentHealth;
    private Transform _healthBarRoot;
    private SpriteRenderer _healthBarBackground;
    private SpriteRenderer _healthBarFill;
    private static Sprite _whiteSprite;
    private bool _isDead;
    private Collider2D _collider2D;
    private EnemyDamageDealer _damageDealer;
    private float _defaultAnimatorSpeed = 1f;

    private void Awake()
    {
        animator = animator != null ? animator : GetComponent<Animator>();
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        _collider2D = GetComponent<Collider2D>();
        _damageDealer = GetComponent<EnemyDamageDealer>();
        if (animator != null)
            _defaultAnimatorSpeed = animator.speed;
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

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        Debug.Log($"Enemy '{name}' took {damage} damage. HP: {_currentHealth}/{maxHealth}", this);
        TriggerHurtAnimation();
        UpdateHealthBar();

        if (_currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

        if (_damageDealer != null)
            _damageDealer.enabled = false;

        if (_collider2D != null)
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

        Destroy(gameObject, GetDeathAnimationDuration() + Mathf.Max(0f, destroyDelay));
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
}