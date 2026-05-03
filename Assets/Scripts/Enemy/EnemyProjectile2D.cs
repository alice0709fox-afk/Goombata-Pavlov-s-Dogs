using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float spawnCollisionGrace = 0.06f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Collider2D[] _ownerColliders = System.Array.Empty<Collider2D>();
    private float _damage;
    private float _speed;
    private Vector2 _direction;
    private float _dieAt;
    private float _ignoreCollisionsUntil;
    private bool _launched;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        if (_collider2D != null)
            _collider2D.isTrigger = true;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.simulated = true;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    public void Launch(Vector2 direction, float damage, float speed, float lifeTime, GameObject owner)
    {
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _damage = Mathf.Max(0f, damage);
        _speed = Mathf.Max(0.01f, speed);
        _dieAt = Time.time + Mathf.Max(0.05f, lifeTime);
        _ignoreCollisionsUntil = Time.time + spawnCollisionGrace;
        _launched = true;

        if (owner != null)
        {
            _ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);
            if (_collider2D != null)
            {
                for (int i = 0; i < _ownerColliders.Length; i++)
                {
                    var ownerCollider = _ownerColliders[i];
                    if (ownerCollider == null)
                        continue;

                    Physics2D.IgnoreCollision(_collider2D, ownerCollider, true);
                }
            }
        }

    }

    private void FixedUpdate()
    {
        if (!_launched)
            return;

        var step = _direction * (_speed * Time.fixedDeltaTime);

        if (_rigidbody2D != null && _rigidbody2D.simulated)
        {
            _rigidbody2D.MovePosition(_rigidbody2D.position + step);
            return;
        }

        transform.position += (Vector3)step;
    }

    private void Update()
    {
        if (_launched && Time.time >= _dieAt)
        {
            Destroy(gameObject);
            return;
        }

        // Extra fallback in case FixedUpdate is throttled or physics pauses.
        if (!_launched || (_rigidbody2D != null && _rigidbody2D.simulated))
            return;

        transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!ShouldProcessCollision(other))
            return;

        var playerHealth = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }

        if (other.GetComponentInParent<EnemyController>() != null)
            return;

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return;

        OnTriggerEnter2D(collision.collider);
    }

    private bool ShouldProcessCollision(Collider2D other)
    {
        if (!_launched || other == null)
            return false;

        var playerHealth = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();

        if (Time.time < _ignoreCollisionsUntil)
        {
            // Let the projectile leave the muzzle before checking world collisions.
            return playerHealth != null;
        }

        if (other.isTrigger && playerHealth == null)
            return false;

        for (int i = 0; i < _ownerColliders.Length; i++)
        {
            if (_ownerColliders[i] == other)
                return false;
        }

        return true;
    }
}
