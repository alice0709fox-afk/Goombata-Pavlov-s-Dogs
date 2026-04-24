using UnityEngine;

[DisallowMultipleComponent]
public class CutscenePropLooper : MonoBehaviour
{
    [SerializeField] private Transform[] props;
    [SerializeField] private Transform referencePoint;

    [SerializeField] private Vector2 moveDirection = Vector2.left;
    [SerializeField] private float baseSpeed = 6f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Optional sync with background")]
    [SerializeField] private CutsceneBackgroundScroller speedSource;
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Recycling")]
    [SerializeField] private float recycleBehindDistance = 25f;
    [SerializeField] private float minGap = 12f;
    [SerializeField] private float maxGap = 18f;

    private Vector2 _moveDir;
    private Vector2 _spawnDir;

    private void OnValidate()
    {
        if (props != null && props.Length > 0)
        {
            return;
        }

        var all = GetComponentsInChildren<Transform>(true);
        var count = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != transform)
            {
                count++;
            }
        }

        var filtered = new Transform[count];
        var index = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == transform)
            {
                continue;
            }

            filtered[index++] = all[i];
        }

        props = filtered;
    }

    private void Awake()
    {
        if (referencePoint == null && Camera.main != null)
        {
            referencePoint = Camera.main.transform;
        }

        CacheDirections();
    }

    private void LateUpdate()
    {
        var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f || props == null || props.Length == 0)
        {
            return;
        }

        CacheDirections();

        var speed = ResolveSpeed();
        if (speed <= 0f)
        {
            return;
        }

        var step = (Vector3)(_moveDir * (speed * dt));

        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] != null)
            {
                props[i].position += step;
            }
        }

        if (referencePoint != null)
        {
            RecycleBehindProps();
        }
    }

    private void RecycleBehindProps()
    {
        var referenceProjection = Vector2.Dot((Vector2)referencePoint.position, _spawnDir);

        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];
            if (prop == null)
            {
                continue;
            }

            var propProjection = Vector2.Dot((Vector2)prop.position, _spawnDir);
            var behindDistance = referenceProjection - propProjection;

            if (behindDistance < recycleBehindDistance)
            {
                continue;
            }

            var maxProjection = FindMaxProjection();
            var gap = Random.Range(minGap, Mathf.Max(minGap, maxGap));
            var targetProjection = maxProjection + gap;
            var shift = targetProjection - propProjection;

            prop.position += (Vector3)(_spawnDir * shift);
        }
    }

    private float FindMaxProjection()
    {
        var maxProjection = float.MinValue;

        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];
            if (prop == null)
            {
                continue;
            }

            var projection = Vector2.Dot((Vector2)prop.position, _spawnDir);
            if (projection > maxProjection)
            {
                maxProjection = projection;
            }
        }

        return maxProjection;
    }

    private float ResolveSpeed()
    {
        if (speedSource != null)
        {
            return Mathf.Max(0f, speedSource.CurrentSpeed * speedMultiplier);
        }

        return Mathf.Max(0f, baseSpeed);
    }

    private void CacheDirections()
    {
        _moveDir = moveDirection.sqrMagnitude > 0f ? moveDirection.normalized : Vector2.left;
        _spawnDir = -_moveDir;
    }
}
