using UnityEngine;

[DisallowMultipleComponent]
public class MoneyVisualAnimator : MonoBehaviour
{
    [SerializeField] private float floatFrequency = 2.2f;
    [SerializeField] private float floatAmplitude = 0.18f;
    [SerializeField] private float swayAmplitude = 0.04f;
    [SerializeField] private float pulseFrequency = 2.2f;
    [SerializeField] private float pulseAmount = 0.08f;
    [SerializeField] private float rotationSpeed = 28f;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.55f, 1f);
    [SerializeField] private float glowStrength = 0.45f;

    private Vector3 _baseScale;
    private Vector3 _baseLocalPosition;
    private float _phaseOffset;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        _baseLocalPosition = transform.localPosition;
        _phaseOffset = (transform.position.x + transform.position.y) * 0.15f;
    }

    private void OnEnable()
    {
        _baseScale = transform.localScale;
        _baseLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        var time = Time.time + _phaseOffset;
        var bobY = Mathf.Sin(time * floatFrequency) * floatAmplitude;
        var swayX = Mathf.Cos(time * (floatFrequency * 0.7f)) * swayAmplitude;
        transform.localPosition = _baseLocalPosition + new Vector3(swayX, bobY, 0f);

        var pulse = 1f + Mathf.Sin(time * pulseFrequency) * pulseAmount;
        transform.localScale = _baseScale * pulse;

        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f) * rotationSpeed);

        if (_spriteRenderer != null)
        {
            var glow = (Mathf.Sin(time * pulseFrequency) + 1f) * 0.5f * glowStrength;
            _spriteRenderer.color = Color.Lerp(baseColor, glowColor, glow);
        }
    }
}