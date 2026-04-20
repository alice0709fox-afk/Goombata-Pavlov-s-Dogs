using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Bullet : MonoBehaviour
{
    private float _damage;
    private float _speed;
    private float _lifeTime;
    private Vector2 _direction;
    private LayerMask _layerDamage;
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _collider;
    [SerializeField] private LayerMask passThroughLayers;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();

        // Bullet should use trigger interactions so it does not push or get stuck in world colliders.
        if (_collider != null)
            _collider.isTrigger = true;
    }
    
    public void Init(RangedWeaponData data, Vector2 direction, LayerMask layerDamage)
    {
        _damage = data.Damage;
        _speed = data.SpeedBullet;
        _lifeTime = data.LifeTimeBullet;
        _direction = direction.normalized;
        _layerDamage = layerDamage;
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();
        
        StartCoroutine(LifeTimer());
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        _rigidbody.linearVelocity = _direction * _speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other == null) return;

        if ((passThroughLayers.value & (1 << other.gameObject.layer)) != 0)
            return;
        
        if(_layerDamage.value != 0 && (_layerDamage.value & (1 << other.gameObject.layer)) == 0)
            return;
        
        if (other.TryGetComponent<EnemyController>(out var enemy))
            enemy.TakeDamage(_damage);
        else if (other.GetComponentInParent<EnemyController>() is { } parentEnemy)
            parentEnemy.TakeDamage(_damage);
        
        StopAllCoroutines();
        Kill();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return;

        if ((passThroughLayers.value & (1 << collision.collider.gameObject.layer)) != 0)
            return;

        StopAllCoroutines();
        Kill();
    }

    private IEnumerator LifeTimer()
    {
        yield return new WaitForSeconds(_lifeTime);
        Kill();
    }
    
    private void Kill() => Destroy(gameObject);
}