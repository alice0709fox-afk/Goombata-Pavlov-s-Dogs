using System;
using UnityEngine;

public abstract class WeaponHandlerBase : MonoBehaviour
{
    public abstract void SetStateAttack(bool active);
    public Action<bool> OnAttacking;
}
