using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class LightDimmingZone2D : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Range(0f, 1.5f)] private float insideIntensity = 0.5f;
    [SerializeField] private bool restoreOutsideOnExit = true;
    [SerializeField, Range(0f, 1.5f)] private float outsideIntensity = 1f;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.6f;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private Coroutine _fadeRoutine;
    private bool _hasCachedOutsideIntensity;
    private int _insideCounter;

    private void Awake()
    {
        var trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        if (globalLight != null)
        {
            outsideIntensity = globalLight.intensity;
            _hasCachedOutsideIntensity = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other) || globalLight == null)
        {
            return;
        }

        _insideCounter++;
        StartFade(insideIntensity);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other) || globalLight == null)
        {
            return;
        }

        _insideCounter = Mathf.Max(0, _insideCounter - 1);
        if (_insideCounter > 0 || !restoreOutsideOnExit)
        {
            return;
        }

        var targetOutside = _hasCachedOutsideIntensity ? outsideIntensity : globalLight.intensity;
        StartFade(targetOutside);
    }

    private void StartFade(float target)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }

        _fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        var start = globalLight.intensity;
        var elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / fadeDuration);
            globalLight.intensity = Mathf.Lerp(start, target, t);
            yield return null;
        }

        globalLight.intensity = target;
        _fadeRoutine = null;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMovement>() != null;
    }
}