using System;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private WeaponHandlerBase weapon;
    [SerializeField] private GroundChecker groundChecker;

    private Vector3 _startScale;
    
    private const string NameAnimationWalk = "Walk";
    private const string NameAnimationRun = "Run";
    private const string NameAnimationAttack = "Attack";
    private const string NameAnimationJump = "Jump";
    private bool _isWeaponSubscribed;

    private void Start()
    {
        _startScale = transform.localScale;
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (movement == null)
            movement = GetComponentInChildren<PlayerMovement>();

        if (groundChecker == null)
            groundChecker = GetComponentInChildren<GroundChecker>();

        if (weapon == null)
            weapon = GetComponentInChildren<WeaponHandlerBase>();

        if (weapon != null)
        {
            weapon.OnAttacking += Attack;
            _isWeaponSubscribed = true;
        }
        else
        {
            Debug.LogError("PlayerAnimator: WeaponHandlerBase not found for attack animation subscription.", this);
        }

        if (groundChecker != null)
        {
            groundChecker.CheckGround();
        }
    }

    private void OnDestroy()
    {
        if (_isWeaponSubscribed && weapon != null)
        {
            weapon.OnAttacking -= Attack;
        }
    }

    private void FixedUpdate()
    {
        MoveAnimation();
        JumpAnimation();
        Flip();
    }

    private void Flip()
    {
        if(movement.MoveInput.x == 0) return;
        
        var scaleX = _startScale.x;
        if(movement.MoveInput.x < 0) scaleX *= -1;
        
        transform.localScale = new Vector3(scaleX, _startScale.y, _startScale.z);
    }

    private void MoveAnimation()
    {
        if (animator == null || movement == null) return;

        animator.SetBool(NameAnimationWalk, movement.MoveInput.x != 0);
        animator.SetBool(NameAnimationRun, movement.MoveInput.x != 0 && movement.RunningPressed);
    }


    private void JumpAnimation()
    {
        if (animator == null || groundChecker == null) return;

        animator.SetBool(NameAnimationJump, !groundChecker.IsGrounded);
    }

    private void Attack(bool isAttacking)
    {
        if (animator == null) return;

        animator.SetBool(NameAnimationAttack, isAttacking);
    }
}