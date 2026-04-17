using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/MeleeWeaponData", fileName = "MeleeWeaponData")]
public class MeleeWeaponData : BaseWeaponData
{
    [SerializeField] private float radiusDamage;
    
    public float RadiusDamage { get => radiusDamage; set => radiusDamage = value; }
}
