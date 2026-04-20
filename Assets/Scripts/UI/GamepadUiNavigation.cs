using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class GamepadUiNavigation
{
    private const float NavigationThreshold = 0.5f;
    private const float NavigationRepeatDelay = 0.18f;

    private static float _nextNavigationTime;
    private static readonly InputAction NavigateAction = CreateNavigateAction();
    private static readonly InputAction SubmitAction = CreateButtonAction("GamepadSubmit", "<Gamepad>/buttonSouth", "<Gamepad>/startButton");
    private static readonly InputAction CancelAction = CreateButtonAction("GamepadCancel", "<Gamepad>/buttonEast", "<Gamepad>/selectButton");

    static GamepadUiNavigation()
    {
        NavigateAction.Enable();
        SubmitAction.Enable();
        CancelAction.Enable();
    }

    public static bool TryGetActiveGamepad(out Gamepad gamepad)
    {
        var players = UnityEngine.Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            var player = players[playerIndex];
            if (player == null || !player.inputIsActive)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(player.currentControlScheme) &&
                player.currentControlScheme.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var pairedDevices = player.devices;
            for (var deviceIndex = 0; deviceIndex < pairedDevices.Count; deviceIndex++)
            {
                if (pairedDevices[deviceIndex] is Gamepad pairedGamepad && pairedGamepad.added)
                {
                    gamepad = pairedGamepad;
                    return true;
                }
            }
        }

        if (Gamepad.current != null && Gamepad.current.added)
        {
            gamepad = Gamepad.current;
            return true;
        }

        var gamepads = Gamepad.all;
        for (var index = 0; index < gamepads.Count; index++)
        {
            var candidate = gamepads[index];
            if (candidate != null && candidate.added)
            {
                gamepad = candidate;
                return true;
            }
        }

        gamepad = null;
        return false;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    public static void EnsureSelection(Selectable fallbackSelection)
    {
        EnsureEventSystem();

        if (EventSystem.current == null || fallbackSelection == null)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            return;
        }

        if (!HasAnyNavigationInput())
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(fallbackSelection.gameObject);
    }

    public static void HandleNavigation()
    {
        if (EventSystem.current == null || !TryGetActiveGamepad(out _))
        {
            return;
        }

        var selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
        {
            return;
        }

        var currentSelectable = selectedObject.GetComponent<Selectable>() ?? selectedObject.GetComponentInParent<Selectable>();
        if (currentSelectable == null)
        {
            return;
        }

        if (Time.unscaledTime < _nextNavigationTime)
        {
            return;
        }

        var nextSelectable = GetNextSelectable(currentSelectable);
        if (nextSelectable != null && nextSelectable.gameObject != EventSystem.current.currentSelectedGameObject)
        {
            EventSystem.current.SetSelectedGameObject(nextSelectable.gameObject);
            _nextNavigationTime = Time.unscaledTime + NavigationRepeatDelay;
        }
    }

    public static bool TryGetNavigationMove(out Vector2Int move)
    {
        move = Vector2Int.zero;

        if (Time.unscaledTime < _nextNavigationTime)
        {
            return false;
        }

        var navigate = NavigateAction.ReadValue<Vector2>();
        if (navigate.y <= -NavigationThreshold)
        {
            move = Vector2Int.down;
        }
        else if (navigate.y >= NavigationThreshold)
        {
            move = Vector2Int.up;
        }
        else if (navigate.x <= -NavigationThreshold)
        {
            move = Vector2Int.left;
        }
        else if (navigate.x >= NavigationThreshold)
        {
            move = Vector2Int.right;
        }

        if (move == Vector2Int.zero)
        {
            return false;
        }

        _nextNavigationTime = Time.unscaledTime + NavigationRepeatDelay;
        return true;
    }

    public static void HandleSubmit()
    {
        if (!WasSubmitPressedThisFrame() || EventSystem.current == null)
        {
            return;
        }

        var selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
        {
            return;
        }

        var button = selectedObject.GetComponent<Button>() ?? selectedObject.GetComponentInParent<Button>();
        button?.onClick.Invoke();
    }

    public static bool WasCancelPressedThisFrame()
    {
        return CancelAction.WasPressedThisFrame();
    }

    public static bool WasSubmitPressedThisFrame()
    {
        return SubmitAction.WasPressedThisFrame();
    }

    private static bool HasAnyNavigationInput()
    {
        var navigate = NavigateAction.ReadValue<Vector2>();

        return Mathf.Abs(navigate.x) >= NavigationThreshold ||
            Mathf.Abs(navigate.y) >= NavigationThreshold ||
            WasSubmitPressedThisFrame();
    }

    private static Selectable GetNextSelectable(Selectable currentSelectable)
    {
        var navigate = NavigateAction.ReadValue<Vector2>();

        if (navigate.y <= -NavigationThreshold)
        {
            return currentSelectable.FindSelectableOnDown() ?? currentSelectable;
        }

        if (navigate.y >= NavigationThreshold)
        {
            return currentSelectable.FindSelectableOnUp() ?? currentSelectable;
        }

        if (navigate.x <= -NavigationThreshold)
        {
            return currentSelectable.FindSelectableOnLeft() ?? currentSelectable;
        }

        if (navigate.x >= NavigationThreshold)
        {
            return currentSelectable.FindSelectableOnRight() ?? currentSelectable;
        }

        return null;
    }

    private static InputAction CreateNavigateAction()
    {
        var action = new InputAction("GamepadNavigate", InputActionType.Value);
        action.AddBinding("<Gamepad>/dpad");
        action.AddBinding("<Gamepad>/leftStick");
        return action;
    }

    private static InputAction CreateButtonAction(string actionName, string primaryBinding, string secondaryBinding)
    {
        var action = new InputAction(actionName, InputActionType.Button);
        action.AddBinding(primaryBinding);
        action.AddBinding(secondaryBinding);
        return action;
    }

}