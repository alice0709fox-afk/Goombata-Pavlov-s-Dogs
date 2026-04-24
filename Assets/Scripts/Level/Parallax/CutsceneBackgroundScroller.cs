using UnityEngine;

[DisallowMultipleComponent]
public class CutsceneBackgroundScroller : MonoBehaviour
{
    private enum ScrollMode
    {
        UvOffset,
        TransformPosition
    }

    [Header("Layers")]
    [SerializeField] private Renderer[] layers;
    [SerializeField] private ScrollMode scrollMode = ScrollMode.TransformPosition;

    [Header("Direction")]
    [SerializeField] private Vector2 scrollDirection = Vector2.left;

    [Header("Speed")]
    [SerializeField] private float startSpeed = 0f;
    [SerializeField] private float baseSpeed = 1.5f;
    [SerializeField] private float accelerationDuration = 6f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Parallax")]
    [Range(0f, 2f)] [SerializeField] private float parallaxFactorMin = 0.9f;
    [Range(0f, 2f)] [SerializeField] private float parallaxFactorMax = 1f;
    [Range(0f, 1f)] [SerializeField] private float parallaxStrength = 0.25f;

    [Header("Optional speed sway")]
    [SerializeField] private float swayAmplitude = 0f;
    [SerializeField] private float swayFrequency = 0.8f;

    public float CurrentSpeed { get; private set; }

    private const string MainTexProperty = "_MainTex_ST";
    private const string BaseMapProperty = "_BaseMap_ST";

    private MaterialPropertyBlock[] _propertyBlocks;
    private Vector2[] _uvOffsets;

    private Transform[] _primary;
    private Transform[] _secondary;
    private Vector3[] _primaryStartWorld;
    private Vector3[] _secondaryStartWorld;
    private float[] _segmentLengths;

    private bool _isInitialized;
    private float _elapsedScrollTime;
    private float _loopProgress;

    private void OnEnable()
    {
        InitializeIfNeeded();
        _elapsedScrollTime = 0f;
        _loopProgress = 0f;
        CurrentSpeed = 0f;
    }

    private void OnDisable()
    {
        CleanupLoopClones();
        _isInitialized = false;
    }

    private void OnValidate()
    {
        if (layers == null || layers.Length != 0)
        {
            return;
        }

        layers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (!_isInitialized || layers == null || layers.Length == 0)
        {
            return;
        }

        var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        _elapsedScrollTime += dt;

        var dir = GetSafeDirection();
        var ramp = accelerationDuration <= 0f ? 1f : Mathf.Clamp01(_elapsedScrollTime / accelerationDuration);
        var targetSpeed = Mathf.Lerp(startSpeed, baseSpeed, ramp);
        var timeSource = useUnscaledTime ? Time.unscaledTime : Time.time;
        var sway = Mathf.Sin(timeSource * swayFrequency) * swayAmplitude;
        var speed = Mathf.Max(0f, targetSpeed + sway);

        CurrentSpeed = speed;
        _loopProgress += speed * dt;

        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer == null)
            {
                continue;
            }

            var t = layers.Length <= 1 ? 1f : (float)i / (layers.Length - 1);
            var rawParallax = Mathf.Lerp(parallaxFactorMin, parallaxFactorMax, t);
            var parallax = Mathf.Lerp(1f, rawParallax, parallaxStrength);

            if (scrollMode == ScrollMode.TransformPosition)
            {
                MoveTransformLayer(i, dir, parallax);
            }
            else
            {
                MoveUvLayer(i, layer, dir, speed, parallax, dt);
            }
        }
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized)
        {
            return;
        }

        if (layers == null || layers.Length == 0)
        {
            layers = GetComponentsInChildren<Renderer>(true);
        }

        if (layers == null || layers.Length == 0)
        {
            return;
        }

        _propertyBlocks = new MaterialPropertyBlock[layers.Length];
        _uvOffsets = new Vector2[layers.Length];

        _primary = new Transform[layers.Length];
        _secondary = new Transform[layers.Length];
        _primaryStartWorld = new Vector3[layers.Length];
        _secondaryStartWorld = new Vector3[layers.Length];
        _segmentLengths = new float[layers.Length];

        var dir = GetSafeDirection();

        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer == null)
            {
                continue;
            }

            _propertyBlocks[i] = new MaterialPropertyBlock();
            layer.GetPropertyBlock(_propertyBlocks[i]);

            if (scrollMode == ScrollMode.TransformPosition)
            {
                SetupLoopPair(i, layer, dir);
            }
        }

        _isInitialized = true;
    }

    private Vector2 GetSafeDirection()
    {
        return scrollDirection.sqrMagnitude > 0f ? scrollDirection.normalized : Vector2.left;
    }

    private void MoveUvLayer(int index, Renderer layer, Vector2 dir, float speed, float parallax, float dt)
    {
        var delta = dir * (speed * parallax * dt);
        _uvOffsets[index] += delta;

        var block = _propertyBlocks[index];
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            _propertyBlocks[index] = block;
        }

        layer.GetPropertyBlock(block);

        var uv = new Vector4(1f, 1f, _uvOffsets[index].x, _uvOffsets[index].y);
        block.SetVector(MainTexProperty, uv);
        block.SetVector(BaseMapProperty, uv);

        layer.SetPropertyBlock(block);
    }

    private void MoveTransformLayer(int index, Vector2 dir, float parallax)
    {
        var primary = _primary[index];
        var secondary = _secondary[index];
        var segmentLength = _segmentLengths[index];

        if (primary == null || secondary == null || segmentLength <= 0f)
        {
            return;
        }

        var shift = Mathf.Repeat(_loopProgress * parallax, segmentLength);
        var offset = (Vector3)(dir * shift);

        primary.position = _primaryStartWorld[index] + offset;
        secondary.position = _secondaryStartWorld[index] + offset;
    }

    private void SetupLoopPair(int index, Renderer layer, Vector2 dir)
    {
        var length = ResolveSegmentLength(layer, dir);
        if (length <= 0f)
        {
            _primary[index] = layer.transform;
            _secondary[index] = null;
            _segmentLengths[index] = 0f;
            return;
        }

        var original = layer.transform;
        var cloneObject = Instantiate(layer.gameObject, original.parent);
        cloneObject.name = original.gameObject.name + "_LoopClone";

        var clone = cloneObject.transform;

        var primaryStart = original.position;
        var secondaryStart = primaryStart - (Vector3)(dir * length);

        clone.position = secondaryStart;

        _primary[index] = original;
        _secondary[index] = clone;
        _primaryStartWorld[index] = primaryStart;
        _secondaryStartWorld[index] = secondaryStart;
        _segmentLengths[index] = length;
    }

    private static float ResolveSegmentLength(Renderer layer, Vector2 dir)
    {
        if (layer == null)
        {
            return 0f;
        }

        var ext = layer.bounds.extents;
        var axis = new Vector2(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
        return 2f * (axis.x * ext.x + axis.y * ext.y);
    }

    private void CleanupLoopClones()
    {
        if (_secondary == null)
        {
            return;
        }

        for (int i = 0; i < _secondary.Length; i++)
        {
            var clone = _secondary[i];
            if (clone == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(clone.gameObject);
            }
            else
            {
                DestroyImmediate(clone.gameObject);
            }

            _secondary[i] = null;
        }
    }
}
