using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float damageInvulnerabilityDuration = 0.2f;
    [Header("Hurt Feedback")]
    [SerializeField] private Color hurtFlashHighlightColor = Color.white;
    [SerializeField, Min(0f)] private float hurtFlashHighlightDuration = 0.04f;
    [SerializeField] private Color hurtFlashColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField, Min(0.01f)] private float hurtFlashDuration = 0.09f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => _isDead;

    public event System.Action Died;
    public event System.Action<float, float> HealthChanged;

    private PlayerMovement _playerMovement;
    private PlayerAttackHandler _playerAttackHandler;
    private PlayerInputHandler _playerInputHandler;
    private PlayerAnimator _playerAnimator;
    private Rigidbody2D _rigidbody2D;
    private Collider2D[] _colliders;
    private bool _isDead;
    private float _nextAllowedDamageTime;
    private SpriteRenderer[] _flashSpriteRenderers = System.Array.Empty<SpriteRenderer>();
    private Color[] _defaultFlashColors = System.Array.Empty<Color>();
    private Coroutine _hurtFlashRoutine;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerAttackHandler = GetComponent<PlayerAttackHandler>();
        _playerInputHandler = GetComponent<PlayerInputHandler>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _colliders = GetComponents<Collider2D>();
        CacheFlashRenderers();
        CurrentHealth = Mathf.Max(1f, maxHealth);
        NotifyHealthChanged();
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentHealth <= 0f || _isDead)
            return;

        if (Time.time < _nextAllowedDamageTime)
            return;

        _nextAllowedDamageTime = Time.time + damageInvulnerabilityDuration;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        NotifyHealthChanged();
        AudioController.Instance?.PlayPlayerHurt();

        if (CurrentHealth > 0f && _playerAnimator != null)
            _playerAnimator.PlayHurt();

        TriggerHurtFlash();

        if (CurrentHealth <= 0f)
            Die();
    }

    public void Heal(float value)
    {
        if (value <= 0f || CurrentHealth <= 0f)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + value);
        NotifyHealthChanged();
    }

    public void IncreaseMaxHealth(float value, bool healToFull = true)
    {
        if (value <= 0f)
            return;

        maxHealth += value;

        if (healToFull)
            CurrentHealth = maxHealth;
        else
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);

        NotifyHealthChanged();
    }

    public void KillInstantly()
    {
        if (_isDead)
            return;

        CurrentHealth = 0f;
        NotifyHealthChanged();
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

        RestoreFlashColors();

        if (_playerMovement != null)
            _playerMovement.enabled = false;

        if (_playerAttackHandler != null)
            _playerAttackHandler.enabled = false;

        if (_playerInputHandler != null)
            _playerInputHandler.enabled = false;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
            _rigidbody2D.simulated = false;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = false;
        }

        if (_playerAnimator != null)
            _playerAnimator.PlayDeath();

        Died?.Invoke();
    }

    private void TriggerHurtFlash()
    {
        if (_isDead)
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

    private void OnDisable()
    {
        if (_hurtFlashRoutine != null)
        {
            StopCoroutine(_hurtFlashRoutine);
            _hurtFlashRoutine = null;
        }

        RestoreFlashColors();
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

}