using System;
using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private Transform parent;
    [SerializeField] private Vector2 parentOffset;
    [SerializeField] private Renderer[] layers;

    [Range(0f, 1f)] [SerializeField] private float parallaxFactorMin = 0.01f;
    [Range(0f, 1f)] [SerializeField] private float parallaxFactorMax = 0.03f;

    private Vector3 _lastCameraPosition;
    private Vector2 _initialParentOffset;

    private MaterialPropertyBlock[] _propertyBlocks;
    private Vector2[] _offsets;

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

        _lastCameraPosition = targetCamera.position;
        _initialParentOffset = (Vector2)(parent.position - targetCamera.position);

        var count = layers.Length;

        _propertyBlocks = new MaterialPropertyBlock[count];
        _offsets = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            _propertyBlocks[i] = new MaterialPropertyBlock();
            layers[i].GetPropertyBlock(_propertyBlocks[i]);
        }
    }

    private void LateUpdate()
    {
        var delta = targetCamera.position - _lastCameraPosition;

        for (int i = 0; i < layers.Length; i++)
        {
            var t = layers.Length == 1 ? 0 : (float)i / (layers.Length - 1);
            var parallax = Mathf.Lerp(parallaxFactorMin, parallaxFactorMax, t);

            _offsets[i].x += delta.x * parallax;
            _offsets[i].x = Mathf.Repeat(_offsets[i].x, 1);

            var offsetVector = new Vector4(1, 1, _offsets[i].x, 0);
            
            _propertyBlocks[i].SetVector(MainTexProperty, offsetVector);
            _propertyBlocks[i].SetVector(BaseMapProperty, offsetVector);

            layers[i].SetPropertyBlock(_propertyBlocks[i]);
        }

        _lastCameraPosition = targetCamera.position;
        parent.position = (Vector2)targetCamera.position + _initialParentOffset + parentOffset;
    }
    
}