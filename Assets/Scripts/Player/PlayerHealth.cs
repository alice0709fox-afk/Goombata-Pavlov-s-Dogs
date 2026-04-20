using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

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

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerAttackHandler = GetComponent<PlayerAttackHandler>();
        _playerInputHandler = GetComponent<PlayerInputHandler>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _colliders = GetComponents<Collider2D>();
        CurrentHealth = Mathf.Max(1f, maxHealth);
        NotifyHealthChanged();
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentHealth <= 0f || _isDead)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        NotifyHealthChanged();
        AudioController.Instance?.PlayPlayerHurt();

        if (CurrentHealth > 0f && _playerAnimator != null)
            _playerAnimator.PlayHurt();

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

    private void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

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

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

}