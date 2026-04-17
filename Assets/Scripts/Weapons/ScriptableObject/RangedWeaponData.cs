using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/RangedWeaponData", fileName = "RangedWeaponData")]
public class RangedWeaponData : BaseWeaponData
{
    [Header("Bullet")] 
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private float speedBullet;
    [SerializeField] private float lifeTimeBullet;
    
    [Header("Magazine")] 
    [SerializeField] private int magazineSize;
    [SerializeField] private float reloadTime;


    public Bullet BulletPrefab => bulletPrefab;
    public float SpeedBullet => speedBullet;
    public float LifeTimeBullet => lifeTimeBullet;

    public int MagazineSize => magazineSize;
    public float ReloadTime => reloadTime;
}