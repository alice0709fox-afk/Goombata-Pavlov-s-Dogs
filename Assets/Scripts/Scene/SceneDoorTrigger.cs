using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class SceneDoorTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        Enter,
        Exit
    }

    [SerializeField] private string targetSceneName = "Level_1";
    [SerializeField] private string targetSpawnPointName = "TargetSpawnPointName";
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Exit;
    [SerializeField] private float activationDelay = 0.75f;
    [SerializeField] private bool requireInteract;
    [SerializeField] private float interactEnterDelay = 0.1f;

    private bool _isTriggered;
    private bool _playerInside;
    private float _canTriggerAfterTime;
    private float _playerEnteredTime;
    private Collider2D _triggerCollider;
    private Coroutine _initialOverlapRoutine;
    private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

    private void OnEnable()
    {
        _isTriggered = false;
        _playerInside = false;
        _canTriggerAfterTime = Time.timeSinceLevelLoad + Mathf.Max(0f, activationDelay);

        EnsureTriggerCollider();
        StartInitialOverlapCheck();
    }

    private void OnDisable()
    {
        if (_initialOverlapRoutine != null)
        {
            StopCoroutine(_initialOverlapRoutine);
            _initialOverlapRoutine = null;
        }
    }

    private void Update()
    {
        if (!requireInteract || !_playerInside || _isTriggered)
        {
            return;
        }

        if (Time.time < _playerEnteredTime + interactEnterDelay)
        {
            return;
        }

        if (!WasInteractPressedThisFrame())
        {
            return;
        }

        LoadTargetScene();
    }

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        collider2D.isTrigger = true;
    }

    private void EnsureTriggerCollider()
    {
        if (_triggerCollider == null)
        {
            _triggerCollider = GetComponent<Collider2D>();
        }
    }

    private void StartInitialOverlapCheck()
    {
        if (triggerMode != TriggerMode.Enter)
        {
            return;
        }

        if (_initialOverlapRoutine != null)
        {
            StopCoroutine(_initialOverlapRoutine);
        }

        _initialOverlapRoutine = StartCoroutine(CheckInitialOverlapAfterDelay());
    }

    private IEnumerator CheckInitialOverlapAfterDelay()
    {
        while (!_isTriggered && Time.timeSinceLevelLoad < _canTriggerAfterTime)
        {
            yield return null;
        }

        if (_isTriggered || _triggerCollider == null)
        {
            _initialOverlapRoutine = null;
            yield break;
        }

        var hitCount = _triggerCollider.Overlap(ContactFilter2D.noFilter, _overlapBuffer);
        for (var i = 0; i < hitCount; i++)
        {
            var other = _overlapBuffer[i];
            if (!IsPlayerCollider(other))
            {
                continue;
            }

            if (requireInteract)
            {
                _playerInside = true;
                break;
            }

            TryLoadScene(other);
            if (_isTriggered)
            {
                break;
            }
        }

        _initialOverlapRoutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        _playerInside = true;
        _playerEnteredTime = Time.time;

        if (requireInteract)
        {
            return;
        }

        if (triggerMode == TriggerMode.Enter)
        {
            TryLoadScene(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        _playerInside = false;

        if (requireInteract)
        {
            return;
        }

        if (triggerMode == TriggerMode.Exit)
        {
            TryLoadScene(other);
        }
    }

    private void TryLoadScene(Collider2D other)
    {
        if (_isTriggered)
        {
            return;
        }

        if (Time.timeSinceLevelLoad < _canTriggerAfterTime)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (_isTriggered)
        {
            return;
        }

        _isTriggered = true;
        SceneTransitionManager.Instance.LoadScene(targetSceneName, targetSpawnPointName);
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        return other != null && other.GetComponentInParent<PlayerMovement>() != null;
    }

    private static bool WasInteractPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) && gamepad.buttonWest.wasPressedThisFrame;
    }
}
