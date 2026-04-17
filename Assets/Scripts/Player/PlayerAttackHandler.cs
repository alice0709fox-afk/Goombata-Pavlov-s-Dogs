using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerAttackHandler : MonoBehaviour
{
    [SerializeField] private WeaponHandlerBase weapon;
    
    private PlayerInputHandler _input;
    private bool _missingWeaponLogged;

    private void Start()
    {
        _input = GetComponent<PlayerInputHandler>();
        if (_input == null)
        {
            Debug.LogError("PlayerAttackHandler: PlayerInputHandler component is missing.", this);
            return;
        }

        _input.OnAttackPressed += SetStateAttack;
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnAttackPressed -= SetStateAttack;
        }
    }

    private void SetStateAttack(bool isAttacking)
    {
        if (weapon == null)
        {
            if (!_missingWeaponLogged)
            {
                Debug.LogError("PlayerAttackHandler: Weapon handler is not assigned.", this);
                _missingWeaponLogged = true;
            }
            return;
        }

        weapon.SetStateAttack(isAttacking);
    }
}
