using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerInput input;
    
    public event Action<Vector2> OnMove;
    public event Action OnJumpPressed;
    public event Action<bool> OnRunningPressed;
    public event Action<bool> OnAttackPressed;
    
    private InputAction MoveAction => input?.actions?["Move"];
    private InputAction JumpAction => input?.actions?["Jump"];
    private InputAction RunningAction => input?.actions?["Running"];
    private InputAction AttackAction => input?.actions?["Attack"];

    private void OnEnable()
    {
       if (!ValidateInputSetup()) return;

       MoveAction.performed += MovePerformed;
       MoveAction.canceled += MoveCanceled;

       JumpAction.performed += JumpPerformed;

       RunningAction.performed += RunningPerformed;
       RunningAction.canceled += RunningCanceled;

       AttackAction.performed += AttackPerformed;
       AttackAction.canceled += AttackCanceled;
    }

    private void OnDisable()
    {
        if (MoveAction != null)
        {
            MoveAction.performed -= MovePerformed;
            MoveAction.canceled -= MoveCanceled;
        }

        if (JumpAction != null)
        {
            JumpAction.performed -= JumpPerformed;
        }

        if (RunningAction != null)
        {
            RunningAction.performed -= RunningPerformed;
            RunningAction.canceled -= RunningCanceled;
        }

        if (AttackAction != null)
        {
            AttackAction.performed -= AttackPerformed;
            AttackAction.canceled -= AttackCanceled;
        }
    }

    private bool ValidateInputSetup()
    {
        if (input == null)
        {
            Debug.LogError("PlayerInputHandler: PlayerInput reference is not assigned.", this);
            return false;
        }

        if (MoveAction == null || JumpAction == null || RunningAction == null || AttackAction == null)
        {
            Debug.LogError("PlayerInputHandler: One or more actions are missing in PlayerInput actions asset (Move/Jump/Running/Attack).", this);
            return false;
        }

        return true;
    }

    private void MovePerformed(InputAction.CallbackContext ctx)
    {
        try
        {
            OnMove?.Invoke(ctx.ReadValue<Vector2>());
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void MoveCanceled(InputAction.CallbackContext ctx)
    {
        try
        {
            OnMove?.Invoke(Vector2.zero);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void JumpPerformed(InputAction.CallbackContext ctx)
    {
        try
        {
            OnJumpPressed?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void RunningPerformed(InputAction.CallbackContext ctx)
    {
        try
        {
            OnRunningPressed?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void RunningCanceled(InputAction.CallbackContext ctx)
    {
        try
        {
            OnRunningPressed?.Invoke(false);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void AttackPerformed(InputAction.CallbackContext ctx)
    {
        try
        {
            OnAttackPressed?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void AttackCanceled(InputAction.CallbackContext ctx)
    {
        try
        {
            OnAttackPressed?.Invoke(false);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }
}
