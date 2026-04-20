using System;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private WeaponHandlerBase weapon;
    [SerializeField] private GroundChecker groundChecker;
    [SerializeField] private RuntimeAnimatorController deathController;

    private Vector3 _startScale;
    private bool _isWeaponSubscribed;
    private bool _isDead;
    private RuntimeAnimatorController _defaultController;
    
    private const string NameAnimationWalk = "Walk";
    private const string NameAnimationRun = "Run";
    private const string NameAnimationAttack = "Attack";
    private const string NameAnimationJump = "Jump";
    private const string NameAnimationHurt = "Hurt";
    private const string NameAnimationDead = "Dead";
    private const string DeadStatePath = "Base Layer.Gangster_2 Dead";

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

        if (animator != null)
        {
            _defaultController = animator.runtimeAnimatorController;
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
        if (_isDead)
            return;

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
        if (animator == null || _isDead) return;

        animator.SetBool(NameAnimationAttack, isAttacking);
    }

    public void PlayHurt()
    {
        if (animator == null || _isDead)
            return;

        animator.ResetTrigger(NameAnimationHurt);
        animator.SetTrigger(NameAnimationHurt);
    }

    public void PlayDeath()
    {
        if (animator == null || _isDead)
            return;

        _isDead = true;

        animator.SetBool(NameAnimationWalk, false);
        animator.SetBool(NameAnimationRun, false);
        animator.SetBool(NameAnimationJump, false);
        animator.SetBool(NameAnimationAttack, false);
        animator.ResetTrigger(NameAnimationHurt);

        animator.speed = 1f;

        if (deathController != null)
        {
            animator.runtimeAnimatorController = deathController;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(DeadStatePath, 0, 0f);
            return;
        }

        animator.SetBool(NameAnimationDead, true);
        animator.CrossFadeInFixedTime(DeadStatePath, 0f, 0, 0f);
    }
}