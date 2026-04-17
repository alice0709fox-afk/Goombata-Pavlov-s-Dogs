using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class OneWayPlatformPassThrough : MonoBehaviour
{
    [SerializeField] private LayerMask oneWayPlatformLayers;
    [SerializeField] private float upwardVelocityThreshold = -0.05f;
    [SerializeField] private float restoreDelay = 0.12f;
    [SerializeField] private float restoreIfStuckAfter = 1.25f;
    [SerializeField] private float restoreClearance = 0.03f;

    private readonly Dictionary<Collider2D, float> _ignoredPlatforms = new Dictionary<Collider2D, float>();
    private readonly Collider2D[] _platformScanBuffer = new Collider2D[32];

    private Rigidbody2D _rigidbody2D;
    private Collider2D[] _playerColliders;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnoreFromBelow(collision);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnoreFromBelow(collision);
    }

    private void TryIgnoreFromBelow(Collision2D collision)
    {
        var platformCollider = collision.collider;
        if (platformCollider == null)
        {
            return;
        }

        var platformLayerMask = 1 << platformCollider.gameObject.layer;
        if ((oneWayPlatformLayers.value & platformLayerMask) == 0)
        {
            return;
        }

        if (IsSideContactFromBelow(collision, platformCollider))
        {
            IgnorePlatform(platformCollider, true);
            _ignoredPlatforms[platformCollider] = Time.time;
            return;
        }

        if (!IsMovingUpwardForPassThrough())
        {
            return;
        }

        if (!IsHittingUnderside(collision))
        {
            return;
        }

        IgnorePlatform(platformCollider, true);
        _ignoredPlatforms[platformCollider] = Time.time;
    }

    private void FixedUpdate()
    {
        TryIgnoreNearbyPlatforms();

        if (_ignoredPlatforms.Count == 0)
        {
            return;
        }

        var toRestore = ListPool.Get();

        foreach (var pair in _ignoredPlatforms)
        {
            var platformCollider = pair.Key;
            if (platformCollider == null)
            {
                toRestore.Add(platformCollider);
                continue;
            }

            var ignoredSince = pair.Value;
            if (Time.time < ignoredSince + restoreDelay)
            {
                continue;
            }

            var playerBounds = GetPlayerBounds();
            var platformBounds = platformCollider.bounds;

            var isAboveTop = playerBounds.min.y >= platformBounds.max.y - restoreClearance;
            var forceRestore = Time.time >= ignoredSince + restoreIfStuckAfter;

            if (isAboveTop || forceRestore)
            {
                toRestore.Add(platformCollider);
            }
        }

        foreach (var platformCollider in toRestore)
        {
            if (platformCollider != null)
            {
                SnapPlayerToPlatformTop(platformCollider);
                IgnorePlatform(platformCollider, false);
            }

            _ignoredPlatforms.Remove(platformCollider);
        }

        ListPool.Release(toRestore);
    }

    private void TryIgnoreNearbyPlatforms()
    {
        if (!IsMovingUpwardForPassThrough())
        {
            return;
        }

        var playerBounds = GetPlayerBounds();
        var center = playerBounds.center;
        var size = playerBounds.size + new Vector3(0.08f, 0.12f, 0f);
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = oneWayPlatformLayers,
            useTriggers = true
        };

        var count = Physics2D.OverlapBox(center, size, 0f, filter, _platformScanBuffer);
        for (var i = 0; i < count; i++)
        {
            var platformCollider = _platformScanBuffer[i];
            if (platformCollider == null)
            {
                continue;
            }

            IgnorePlatform(platformCollider, true);
            _ignoredPlatforms[platformCollider] = Time.time;
            _platformScanBuffer[i] = null;
        }
    }

    private bool IsMovingUpwardForPassThrough()
    {
        if (_rigidbody2D == null)
        {
            return false;
        }

        // Prevent negative thresholds from treating idle/falling states as upward movement.
        var requiredUpwardVelocity = Mathf.Max(0.05f, upwardVelocityThreshold);
        return _rigidbody2D.linearVelocity.y > requiredUpwardVelocity;
    }

    private void OnDisable()
    {
        foreach (var pair in _ignoredPlatforms)
        {
            IgnorePlatform(pair.Key, false);
        }

        _ignoredPlatforms.Clear();
    }

    private void IgnorePlatform(Collider2D platformCollider, bool ignore)
    {
        if (platformCollider == null || _playerColliders == null)
        {
            return;
        }

        for (var i = 0; i < _playerColliders.Length; i++)
        {
            var playerCollider = _playerColliders[i];
            if (playerCollider == null)
            {
                continue;
            }

            Physics2D.IgnoreCollision(playerCollider, platformCollider, ignore);
        }
    }

    private Bounds GetPlayerBounds()
    {
        var hasBounds = false;
        var bounds = new Bounds(transform.position, Vector3.zero);

        if (_playerColliders == null || _playerColliders.Length == 0)
        {
            return bounds;
        }

        for (var i = 0; i < _playerColliders.Length; i++)
        {
            var col = _playerColliders[i];
            if (col == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return bounds;
    }

    private void SnapPlayerToPlatformTop(Collider2D platformCollider)
    {
        if (platformCollider == null)
        {
            return;
        }

        var playerBounds = GetPlayerBounds();
        var platformBounds = platformCollider.bounds;

        var targetMinY = platformBounds.max.y + 0.01f;
        var deltaY = targetMinY - playerBounds.min.y;
        if (deltaY <= 0f)
        {
            return;
        }

        transform.position += new Vector3(0f, deltaY, 0f);

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, 0f);
            _rigidbody2D.angularVelocity = 0f;
        }
    }

    private static bool IsHittingUnderside(Collision2D collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return false;
        }

        // For platform as 'other' collider, underside contact points typically have downward normals.
        for (var i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (contact.normal.y < -0.15f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSideContactFromBelow(Collision2D collision, Collider2D platformCollider)
    {
        if (collision == null || platformCollider == null || collision.contactCount == 0)
        {
            return false;
        }

        var playerBounds = GetPlayerBounds();
        var platformBounds = platformCollider.bounds;

        // Only treat as pass-through when player is not already above the platform top.
        if (playerBounds.min.y >= platformBounds.max.y - restoreClearance)
        {
            return false;
        }

        for (var i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (Mathf.Abs(contact.normal.x) > 0.7f && Mathf.Abs(contact.normal.y) < 0.45f)
            {
                return true;
            }
        }

        return false;
    }

    private static class ListPool
    {
        private static readonly Stack<List<Collider2D>> Pool = new Stack<List<Collider2D>>();

        public static List<Collider2D> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<Collider2D>(8);
        }

        public static void Release(List<Collider2D> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}