using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerConsumables : MonoBehaviour
{
    [SerializeField] private int maxKolaCount = 2;
    [SerializeField] private int startKolaCount;
    [SerializeField] private bool kolaHealsToFull = true;
    [SerializeField] private float kolaHealAmount = 25f;

    public event System.Action<int, int> KolaCountChanged;

    public int CurrentKolaCount { get; private set; }
    public int MaxKolaCount => Mathf.Max(1, maxKolaCount);

    private PlayerHealth _playerHealth;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();
        CurrentKolaCount = Mathf.Clamp(startKolaCount, 0, MaxKolaCount);
        NotifyKolaChanged();
    }

    private void Update()
    {
        if (_playerHealth == null || _playerHealth.IsDead || Mathf.Approximately(Time.timeScale, 0f))
        {
            return;
        }

        if (WasUseKolaPressedThisFrame())
        {
            TryUseKola();
        }
    }

    public bool TryAddKola(int amount = 1)
    {
        if (amount <= 0 || CurrentKolaCount >= MaxKolaCount)
        {
            return false;
        }

        CurrentKolaCount = Mathf.Clamp(CurrentKolaCount + amount, 0, MaxKolaCount);
        NotifyKolaChanged();
        return true;
    }

    public bool TryUseKola()
    {
        if (CurrentKolaCount <= 0 || _playerHealth == null || _playerHealth.CurrentHealth <= 0f)
        {
            return false;
        }

        if (_playerHealth.CurrentHealth >= _playerHealth.MaxHealth)
        {
            return false;
        }

        CurrentKolaCount--;
        if (kolaHealsToFull)
            _playerHealth.Heal(_playerHealth.MaxHealth);
        else
            _playerHealth.Heal(kolaHealAmount);

        AudioController.Instance?.PlayKolaUse();
        NotifyKolaChanged();
        return true;
    }

    private bool WasUseKolaPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            GameSettings.PreferredControlScheme = "Keyboard";
            return true;
        }

        if (GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) && gamepad.buttonNorth.wasPressedThisFrame)
        {
            GameSettings.PreferredControlScheme = "Gamepad";
            return true;
        }

        return false;
    }

    private void NotifyKolaChanged()
    {
        KolaCountChanged?.Invoke(CurrentKolaCount, MaxKolaCount);
    }
}