using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopNpcInteraction : MonoBehaviour
{
    [SerializeField] private string panelObjectName = "CanvasShop";
    [SerializeField] private string exitButtonName = "Exit";
    [SerializeField] private string alternateExitButtonName = "ButtonExit";
    [SerializeField] private float interactEnterDelay = 0.1f;
    [SerializeField] private float interactionTriggerRadius = 2f;
    [SerializeField] private string approvalAnimationStateName = "NPsShop_Approval";
    [SerializeField] private string idleAnimationStateName = "NPsShop_Idle";
    [SerializeField] private float approvalReturnDelay = 1.1f;

    private static bool _bootstrapInitialized;

    private bool _playerInside;
    private float _playerEnteredTime;
    private bool _isPanelOpen;
    private bool _waitingForDialogueEnd;
    private GameObject _shopPanel;
    private Button _exitButton;
    private Dialogue _dialogue;
    private Animator _animator;
    private Coroutine _openAfterDialogueRoutine;
    private Coroutine _approvalRoutine;
    private readonly List<PlayerInputHandler> _disabledInputHandlers = new List<PlayerInputHandler>();
    private ShopLevelUpgradePanel _levelUpgradePanel;
    private PlayerSouls _playerSouls;
    private PlayerHealth _playerHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!_bootstrapInitialized)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _bootstrapInitialized = true;
        }

        AttachToShopNpcsInScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToShopNpcsInScene(scene);
    }

    private static void AttachToShopNpcsInScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (var t = 0; t < transforms.Length; t++)
            {
                var candidate = transforms[t];
                if (candidate == null || !candidate.name.Equals("NPsShop"))
                {
                    continue;
                }

                var interaction = candidate.GetComponent<ShopNpcInteraction>();
                if (interaction == null)
                {
                    interaction = candidate.gameObject.AddComponent<ShopNpcInteraction>();
                }

                interaction.EnsureTriggerCollider();
            }
        }
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        _dialogue = GetComponent<Dialogue>() ?? GetComponentInChildren<Dialogue>(true) ?? GetComponentInParent<Dialogue>();
        _animator = GetComponent<Animator>();
        _playerSouls = FindFirstObjectByType<PlayerSouls>();
        _playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (_dialogue != null)
        {
            _dialogue.DialogueEnded -= HandleDialogueEnded;
            _dialogue.DialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnEnable()
    {
        _playerInside = false;
        _isPanelOpen = false;
        _waitingForDialogueEnd = false;
    }

    private void OnDisable()
    {
        if (_dialogue != null)
        {
            _dialogue.DialogueEnded -= HandleDialogueEnded;
        }

        if (_openAfterDialogueRoutine != null)
        {
            StopCoroutine(_openAfterDialogueRoutine);
            _openAfterDialogueRoutine = null;
        }

        if (_approvalRoutine != null)
        {
            StopCoroutine(_approvalRoutine);
            _approvalRoutine = null;
        }

        CloseShopPanel();
    }

    private void Update()
    {
        if (_waitingForDialogueEnd || _openAfterDialogueRoutine != null)
        {
            return;
        }

        if (!_playerInside)
        {
            return;
        }

        if (Time.unscaledTime < _playerEnteredTime + interactEnterDelay)
        {
            return;
        }

        if (_isPanelOpen)
        {
            if (_levelUpgradePanel != null)
            {
                _levelUpgradePanel.HandleSelectionInput();
            }

            if (WasCloseShopPressedThisFrame())
            {
                CloseShopPanel();
                return;
            }

            if (WasInteractPressedThisFrame() && _levelUpgradePanel != null)
            {
                _levelUpgradePanel.TryPurchaseSelectedUpgrade();
            }

            return;
        }

        if (!WasInteractPressedThisFrame())
        {
            return;
        }

        _openAfterDialogueRoutine = StartCoroutine(OpenShopAfterDialogueRoutine());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        _playerInside = true;
        _playerEnteredTime = Time.unscaledTime;
        if (_dialogue != null)
        {
            _dialogue.ToggleIndicator(true);
        }
        PlayApprovalGreeting();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        _playerInside = false;
        _waitingForDialogueEnd = false;

        if (_dialogue != null)
        {
            _dialogue.ToggleIndicator(false);
        }

        if (_openAfterDialogueRoutine != null)
        {
            StopCoroutine(_openAfterDialogueRoutine);
            _openAfterDialogueRoutine = null;
        }

        if (_isPanelOpen)
        {
            CloseShopPanel();
        }
    }

    private void EnsureTriggerCollider()
    {
        var collider2D = GetComponent<Collider2D>();
        if (collider2D == null)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = Mathf.Max(0.1f, interactionTriggerRadius);
            circle.isTrigger = true;
            return;
        }

        collider2D.isTrigger = true;
        if (collider2D is CircleCollider2D existingCircle)
        {
            existingCircle.radius = Mathf.Max(0.1f, interactionTriggerRadius);
        }
    }

    private IEnumerator OpenShopAfterDialogueRoutine()
    {
        _waitingForDialogueEnd = true;

        if (_dialogue != null && !_dialogue.IsRunning)
        {
            _dialogue.StartDialogue();
        }

        const float dialogueStartGraceSeconds = 0.35f;
        var startedAnyDialogue = IsAnyDialogueRunning();
        var elapsed = 0f;

        while (!startedAnyDialogue && elapsed < dialogueStartGraceSeconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            startedAnyDialogue = IsAnyDialogueRunning();
        }

        if (startedAnyDialogue)
        {
            while (IsAnyDialogueRunning())
            {
                yield return null;
            }
        }

        if (_playerInside && !_isPanelOpen)
        {
            OpenShopPanel();
        }

        _waitingForDialogueEnd = false;
        _openAfterDialogueRoutine = null;
    }

    private void HandleDialogueEnded()
    {
        _waitingForDialogueEnd = false;

        if (_openAfterDialogueRoutine == null && _playerInside && !_isPanelOpen)
        {
            OpenShopPanel();
        }
    }

    private static bool IsAnyDialogueRunning()
    {
        var dialogues = FindObjectsByType<Dialogue>(FindObjectsSortMode.None);
        for (var i = 0; i < dialogues.Length; i++)
        {
            var dialogue = dialogues[i];
            if (dialogue != null && dialogue.IsRunning)
            {
                return true;
            }
        }

        return false;
    }

    private void OpenShopPanel()
    {
        _shopPanel = FindObjectInActiveScene(panelObjectName);
        if (_shopPanel == null)
        {
            Debug.LogWarning($"ShopNpcInteraction: Could not find panel '{panelObjectName}' in active scene.", this);
            return;
        }

        BindExitButton();
        _shopPanel.SetActive(true);
        _isPanelOpen = true;

        _levelUpgradePanel = _shopPanel.GetComponent<ShopLevelUpgradePanel>()
                             ?? _shopPanel.GetComponentInChildren<ShopLevelUpgradePanel>(true);
        if (_levelUpgradePanel == null)
        {
            _levelUpgradePanel = _shopPanel.AddComponent<ShopLevelUpgradePanel>();
        }

        if (_levelUpgradePanel != null)
        {
            _levelUpgradePanel.Initialize(_playerSouls, _playerHealth);
        }

        DisableGameplayInput();
        Time.timeScale = 0f;
    }

    private void CloseShopPanel()
    {
        if (_shopPanel != null)
        {
            _shopPanel.SetActive(false);
        }

        _isPanelOpen = false;
        _levelUpgradePanel = null;
        Time.timeScale = 1f;
        RestoreGameplayInput();
    }

    private void BindExitButton()
    {
        if (_shopPanel == null)
        {
            return;
        }

        var buttons = _shopPanel.GetComponentsInChildren<Button>(true);
        Button resolvedExit = null;

        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (button.name.Equals(exitButtonName, System.StringComparison.OrdinalIgnoreCase)
                || button.name.Equals(alternateExitButtonName, System.StringComparison.OrdinalIgnoreCase))
            {
                resolvedExit = button;
                break;
            }

            if (resolvedExit == null && IsExitButtonByNameOrText(button))
            {
                resolvedExit = button;
            }
        }

        if (resolvedExit == null)
        {
            return;
        }

        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveListener(CloseShopPanel);
        }

        _exitButton = resolvedExit;
        _exitButton.onClick.RemoveListener(CloseShopPanel);
        _exitButton.onClick.AddListener(CloseShopPanel);
    }

    private static bool IsExitButtonByNameOrText(Button button)
    {
        var buttonName = button.name.ToLowerInvariant();
        if (buttonName.Contains("exit") || buttonName.Contains("close") || buttonName.Contains("back"))
        {
            return true;
        }

        var tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            var value = tmpText.text.ToLowerInvariant();
            if (value.Contains("exit") || value.Contains("close") || value.Contains("назад") || value.Contains("выход"))
            {
                return true;
            }
        }

        var uiText = button.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            var value = uiText.text.ToLowerInvariant();
            if (value.Contains("exit") || value.Contains("close") || value.Contains("назад") || value.Contains("выход"))
            {
                return true;
            }
        }

        return false;
    }

    private void DisableGameplayInput()
    {
        _disabledInputHandlers.Clear();

        var handlers = FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            if (handler == null || !handler.enabled)
            {
                continue;
            }

            handler.enabled = false;
            _disabledInputHandlers.Add(handler);
        }
    }

    private void RestoreGameplayInput()
    {
        for (var i = 0; i < _disabledInputHandlers.Count; i++)
        {
            var handler = _disabledInputHandlers[i];
            if (handler != null)
            {
                handler.enabled = true;
            }
        }

        _disabledInputHandlers.Clear();
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

    private static bool WasCloseShopPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        return GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) && gamepad.buttonEast.wasPressedThisFrame;
    }

    private void PlayApprovalGreeting()
    {
        if (_animator == null || string.IsNullOrWhiteSpace(approvalAnimationStateName))
        {
            return;
        }

        if (_approvalRoutine != null)
        {
            StopCoroutine(_approvalRoutine);
        }

        _approvalRoutine = StartCoroutine(PlayApprovalSequence());
    }

    private IEnumerator PlayApprovalSequence()
    {
        _animator.Play(approvalAnimationStateName, 0, 0f);

        if (approvalReturnDelay > 0f)
        {
            yield return new WaitForSeconds(approvalReturnDelay);
        }

        if (_animator != null && !string.IsNullOrWhiteSpace(idleAnimationStateName))
        {
            _animator.Play(idleAnimationStateName, 0, 0f);
        }

        _approvalRoutine = null;
    }

    private static GameObject FindObjectInActiveScene(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return null;
        }

        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var result = FindByNameRecursive(roots[i].transform, objectName);
            if (result != null)
            {
                return result.gameObject;
            }
        }

        return null;
    }

    private static Transform FindByNameRecursive(Transform root, string objectName)
    {
        if (root.name.Equals(objectName))
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var nested = FindByNameRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
