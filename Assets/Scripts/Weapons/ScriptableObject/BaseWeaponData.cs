using UnityEngine;

public class BaseWeaponData : ScriptableObject
{
     [SerializeField] private float damage;
     [SerializeField] private float cooldown;
     [SerializeField] private float attackDuration;
     
     public float Damage => damage;
     public float Cooldown => cooldown;
     public float AttackDuration => attackDuration;
}
