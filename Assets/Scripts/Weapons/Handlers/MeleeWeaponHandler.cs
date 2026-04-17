using System;
using System.Collections;
using UnityEngine;

public class MeleeWeaponHandler : WeaponHandlerBase
{
    [SerializeField] private MeleeWeaponData weapon;
    [SerializeField] private Transform pivotRadius;
    [SerializeField] private float radiusDamage;
    [SerializeField] private LayerMask layerDamage;

    public bool IsAttacking { get; private set; }

    private Coroutine _attackRoutine;
    private bool _missingReferencesLogged;
    
    public override void SetStateAttack(bool active)
    {
        if (active)
        {
            if (!HasRequiredReferences()) return;
            if(_attackRoutine != null) return;
            _attackRoutine = StartCoroutine(AttackLoop());
        }
        else
        {
            if(_attackRoutine == null) return;
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            SetState(false);
        }
    }

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            if (weapon == null)
            {
                Debug.LogError("MeleeWeaponHandler: Weapon data is not assigned. Attack loop stopped.", this);
                _attackRoutine = null;
                yield break;
            }

            SetState(true);
            PerformAttack();
            
            yield return new WaitForSeconds(weapon.AttackDuration);
            
            SetState(false);
            
            yield return new WaitForSeconds(weapon.Cooldown);
        }
    }

    private void SetState(bool active)
    {
        IsAttacking = active;
        try
        {
            OnAttacking?.Invoke(IsAttacking);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void PerformAttack()
    {
        if (pivotRadius == null)
        {
            if (!_missingReferencesLogged)
            {
                Debug.LogError("MeleeWeaponHandler: Pivot Radius is not assigned.", this);
                _missingReferencesLogged = true;
            }
            return;
        }

        var targets = Physics2D.OverlapCircle(pivotRadius.position, radiusDamage, layerDamage);

        if (targets != null)
        {
            // if(targets.TryGetComponent())
            // {
            //     //DAMAGE
            // }
        }
        
        Debug.Log("Damage");
    }

    private bool HasRequiredReferences()
    {
        if (weapon == null)
        {
            Debug.LogError("MeleeWeaponHandler: Weapon data is not assigned.", this);
            return false;
        }

        if (pivotRadius == null)
        {
            Debug.LogError("MeleeWeaponHandler: Pivot Radius is not assigned.", this);
            return false;
        }

        _missingReferencesLogged = false;
        return true;
    }
    
    private void OnDrawGizmos()
    {
        if(pivotRadius ==null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pivotRadius.position, radiusDamage);
    }
}