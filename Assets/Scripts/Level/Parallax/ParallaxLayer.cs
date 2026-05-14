using UnityEngine;

[DefaultExecutionOrder(5000)]
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private Transform parent;
    [SerializeField] private Vector2 parentOffset;
    [SerializeField] private Renderer[] layers;

    [Range(0f, 1f)] [SerializeField] private float parallaxFactorMin = 0.01f;
    [Range(0f, 1f)] [SerializeField] private float parallaxFactorMax = 0.03f;

    [Header("Stability")]
    [SerializeField] private bool ignoreTinyCameraMovement;
    [SerializeField, Min(0f)] private float minCameraDelta = 0.0005f;
    [SerializeField] private bool smoothParentFollow;
    [SerializeField, Min(0.01f)] private float parentFollowSpeed = 20f;

    private Vector3 _initialCameraPosition;
    private Vector2 _initialParentOffset;

    private MaterialPropertyBlock[] _propertyBlocks;
    private bool[] _useTransformFallback;
    private Vector3[] _initialLayerLocalPositions;
    private float[] _fallbackLoopLengthLocal;
    private SpriteRenderer[] _fallbackSpriteSources;
    private Transform[] _fallbackCloneLeft;
    private Transform[] _fallbackCloneRight;
    private bool _hasTransformFallbackLayers;

    private const string MainTexProperty = "_MainTex_ST";
    private const string BaseMapProperty = "_BaseMap_ST";


    private void Start()
    {
        if (targetCamera == null)
        {
            Debug.LogError("No target camera assigned to ParallaxLayer");
            return;
        }

        if (parent == null)
        {
            Debug.LogError("No parent assigned to ParallaxLayer");
            return;
        }

        _initialCameraPosition = targetCamera.position;
        _initialParentOffset = (Vector2)(parent.position - targetCamera.position);

        var count = layers.Length;

        _propertyBlocks = new MaterialPropertyBlock[count];
        _useTransformFallback = new bool[count];
        _initialLayerLocalPositions = new Vector3[count];
        _fallbackLoopLengthLocal = new float[count];
        _fallbackSpriteSources = new SpriteRenderer[count];
        _fallbackCloneLeft = new Transform[count];
        _fallbackCloneRight = new Transform[count];

        for (int i = 0; i < count; i++)
        {
            if (layers[i] == null)
            {
                continue;
            }

            _propertyBlocks[i] = new MaterialPropertyBlock();
            layers[i].GetPropertyBlock(_propertyBlocks[i]);
            _initialLayerLocalPositions[i] = layers[i].transform.localPosition;
            _useTransformFallback[i] = ShouldUseTransformFallback(layers[i]);
            _fallbackLoopLengthLocal[i] = ResolveLocalLoopLength(layers[i], parent);

            if (_useTransformFallback[i])
            {
                _hasTransformFallbackLayers = true;

                var spriteSource = layers[i] as SpriteRenderer;
                _fallbackSpriteSources[i] = spriteSource;

                if (spriteSource != null && _fallbackLoopLengthLocal[i] > 0.0001f)
                {
                    _fallbackCloneLeft[i] = CreateLoopClone(spriteSource, "__LoopCloneL", -_fallbackLoopLengthLocal[i]);
                    _fallbackCloneRight[i] = CreateLoopClone(spriteSource, "__LoopCloneR", _fallbackLoopLengthLocal[i]);
                }
            }
        }
    }

    private void LateUpdate()
    {
        var cameraPosition = targetCamera.position;
        var cameraDisplacement = cameraPosition - _initialCameraPosition;

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null)
            {
                continue;
            }

            var t = layers.Length == 1 ? 0 : (float)i / (layers.Length - 1);
            var parallax = Mathf.Lerp(parallaxFactorMin, parallaxFactorMax, t);

            if (_useTransformFallback[i])
            {
                var rawOffset = -cameraDisplacement.x * parallax;
                var loopLength = _fallbackLoopLengthLocal[i];

                var wrappedOffset = rawOffset;
                if (loopLength > 0.0001f)
                {
                    wrappedOffset = Mathf.Repeat(rawOffset + 0.5f * loopLength, loopLength) - 0.5f * loopLength;
                }

                var local = _initialLayerLocalPositions[i];
                local.x += wrappedOffset;
                layers[i].transform.localPosition = local;

                if (_fallbackCloneLeft[i] != null)
                {
                    var left = local;
                    left.x -= loopLength;
                    _fallbackCloneLeft[i].localPosition = left;
                }

                if (_fallbackCloneRight[i] != null)
                {
                    var right = local;
                    right.x += loopLength;
                    _fallbackCloneRight[i].localPosition = right;
                }

                continue;
            }

            var uvOffsetX = Mathf.Repeat(cameraDisplacement.x * parallax, 1f);

            var offsetVector = new Vector4(1f, 1f, uvOffsetX, 0f);

            _propertyBlocks[i].SetVector(MainTexProperty, offsetVector);
            _propertyBlocks[i].SetVector(BaseMapProperty, offsetVector);

            layers[i].SetPropertyBlock(_propertyBlocks[i]);
        }

        var targetParentPosition2D = (Vector2)cameraPosition + _initialParentOffset + parentOffset;
        var targetParentPosition = new Vector3(targetParentPosition2D.x, targetParentPosition2D.y, parent.position.z);

        if (smoothParentFollow && !_hasTransformFallbackLayers)
        {
            var t = 1f - Mathf.Exp(-parentFollowSpeed * Time.deltaTime);
            parent.position = Vector3.Lerp(parent.position, targetParentPosition, t);
        }
        else
        {
            parent.position = targetParentPosition;
        }
    }

    private static bool ShouldUseTransformFallback(Renderer layer)
    {
        if (layer == null)
        {
            return false;
        }

        var spriteRenderer = layer as SpriteRenderer;
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return false;
        }

        var texture = spriteRenderer.sprite.texture;
        if (texture == null)
        {
            return false;
        }

        return texture.wrapMode != TextureWrapMode.Repeat;
    }

    private static float ResolveLocalLoopLength(Renderer layer, Transform parentTransform)
    {
        if (layer == null)
        {
            return 0f;
        }

        var worldWidth = layer.bounds.size.x;
        if (worldWidth <= 0f)
        {
            return 0f;
        }

        if (parentTransform == null)
        {
            return worldWidth;
        }

        var parentLossyScaleX = Mathf.Abs(parentTransform.lossyScale.x);
        if (parentLossyScaleX <= 0.0001f)
        {
            return worldWidth;
        }

        return worldWidth / parentLossyScaleX;
    }

    private static Transform CreateLoopClone(SpriteRenderer source, string suffix, float localXOffset)
    {
        if (source == null)
        {
            return null;
        }

        var cloneObject = new GameObject(source.gameObject.name + suffix);
        cloneObject.hideFlags = HideFlags.DontSave;

        var cloneTransform = cloneObject.transform;
        cloneTransform.SetParent(source.transform.parent, false);
        cloneTransform.localPosition = source.transform.localPosition + new Vector3(localXOffset, 0f, 0f);
        cloneTransform.localRotation = source.transform.localRotation;
        cloneTransform.localScale = source.transform.localScale;

        var cloneRenderer = cloneObject.AddComponent<SpriteRenderer>();
        CopySpriteRendererVisuals(source, cloneRenderer);

        return cloneTransform;
    }

    private static void CopySpriteRendererVisuals(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.tileMode = source.tileMode;
        target.adaptiveModeThreshold = source.adaptiveModeThreshold;
        target.maskInteraction = source.maskInteraction;
        target.sharedMaterial = source.sharedMaterial;

        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }
    
}