using System;
using System.Collections;
using UnityEngine;

public class RangedWeaponHandler : WeaponHandlerBase
{
    [SerializeField] private RangedWeaponData weapon;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LayerMask layerDamage;

    private int _currentAmmo;
    private bool _isReloading;
    private PlayerCombatUpgrades _combatUpgrades;
    
    private Coroutine _shotRoutine;

    private void Awake()
    {
        _combatUpgrades = GetComponent<PlayerCombatUpgrades>() ?? GetComponentInParent<PlayerCombatUpgrades>();
    }

    private void Start()
    {
        _currentAmmo = weapon.MagazineSize;
    }

    public override void SetStateAttack(bool active)
    {
        if (active)
        {
            if(_shotRoutine != null) return;
            _shotRoutine = StartCoroutine(ShotLoop());
        }
        else
        {
            if(_shotRoutine == null) return;
            StopCoroutine(_shotRoutine);
            _shotRoutine = null;
            SetState(false); 
        }
    }

    private void Shot()
    {
        var bullet = Instantiate(weapon.BulletPrefab, firePoint.position, firePoint.rotation);
        var bonusDamage = _combatUpgrades != null ? _combatUpgrades.FlatDamageBonus : 0f;

        bullet.Init(weapon, firePoint.up, layerDamage, bonusDamage);
        AudioController.Instance?.PlayPlayerHit();
        
        _currentAmmo--;
    }

    private IEnumerator ShotLoop()
    {
        while (true)
        {
            if (_isReloading)
            {
                yield return null;
                continue;
            }

            if (_currentAmmo <= 0)
            {
                yield return StartCoroutine(Reload());
                continue;
            }

            Shot();
            SetState(true);
            yield return new WaitForSeconds(weapon.Cooldown);
            
            SetState(false);
            yield return new WaitForSeconds(weapon.AttackDuration);
        }
    }

    private void SetState(bool active)
    {
        OnAttacking?.Invoke(active);
    }
    
    private IEnumerator Reload()
    {
        _isReloading = true;
        
        yield return new WaitForSeconds(weapon.ReloadTime);
        
        _currentAmmo = weapon.MagazineSize;
        _isReloading = false;
    }
}
