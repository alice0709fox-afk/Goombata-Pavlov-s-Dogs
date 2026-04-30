using UnityEngine;

[DisallowMultipleComponent]
public class WaterDropTransformLock : MonoBehaviour
{
    [SerializeField] private bool lockLocalPosition = true;
    [SerializeField] private bool lockLocalRotation = false;
    [SerializeField] private bool lockLocalScale = false;

    private Vector3 _startLocalPosition;
    private Quaternion _startLocalRotation;
    private Vector3 _startLocalScale;

    private void Awake()
    {
        CacheStartTransform();
    }

    private void OnEnable()
    {
        CacheStartTransform();
    }

    private void LateUpdate()
    {
        if (lockLocalPosition)
        {
            transform.localPosition = _startLocalPosition;
        }

        if (lockLocalRotation)
        {
            transform.localRotation = _startLocalRotation;
        }

        if (lockLocalScale)
        {
            transform.localScale = _startLocalScale;
        }
    }

    private void CacheStartTransform()
    {
        _startLocalPosition = transform.localPosition;
        _startLocalRotation = transform.localRotation;
        _startLocalScale = transform.localScale;
    }
}
