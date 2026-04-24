using UnityEngine;

/// <summary>
/// Attaches to the "transition" indicator object.
/// Animates it with a bob (up/down) and a pulse (scale) to draw attention.
/// </summary>
public class TransitionIndicatorEffect : MonoBehaviour
{
    [Header("Bob")]
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 2.5f;

    [Header("Pulse")]
    [SerializeField] private float pulseAmplitude = 0.12f;
    [SerializeField] private float pulseSpeed = 3f;

    [Header("Appear")]
    [SerializeField] private float appearDuration = 0.35f;

    private Vector3 _startLocalPos;
    private Vector3 _baseScale;
    private float _appearTimer;

    private void OnEnable()
    {
        _startLocalPos = transform.localPosition;
        _baseScale = transform.localScale;
        _appearTimer = 0f;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        // Appear pop-in
        if (_appearTimer < appearDuration)
        {
            _appearTimer += Time.deltaTime;
            var t = Mathf.Clamp01(_appearTimer / appearDuration);
            t = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            transform.localScale = _baseScale * t;
            return;
        }

        // Bob
        var bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = _startLocalPos + new Vector3(0f, bobOffset, 0f);

        // Pulse scale
        var pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        transform.localScale = _baseScale * pulse;
    }

    private void OnDisable()
    {
        transform.localPosition = _startLocalPos == Vector3.zero ? transform.localPosition : _startLocalPos;
        transform.localScale = _baseScale == Vector3.zero ? transform.localScale : _baseScale;
    }
}
