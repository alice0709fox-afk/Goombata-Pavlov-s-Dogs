using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class MusicSwitchZone2D : MonoBehaviour
{
    private enum ZoneMusicMode
    {
        SwitchMainMusic,
        OverlayOnTop
    }

    [Header("Music In Zone")]
    [SerializeField] private ZoneMusicMode musicMode = ZoneMusicMode.OverlayOnTop;
    [SerializeField] private AudioClip insideMusicClipA;
    [SerializeField, Range(0f, 1f)] private float insideVolumeA = 0.4f;
    [SerializeField] private bool insideLoopA = true;
    [SerializeField] private AudioClip insideMusicClipB;
    [SerializeField, Range(0f, 1f)] private float insideVolumeB = 0.3f;
    [SerializeField] private bool insideLoopB = true;
    [SerializeField] private bool muteMainMusicInZone = true;

    [Header("Music Transition")]
    [SerializeField, Min(0f)] private float musicFadeDuration = 0.45f;

    [Header("Light (Optional)")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Range(0f, 1.5f)] private float insideIntensity = 0.5f;
    [SerializeField] private bool restoreOutsideOnExit = true;
    [SerializeField, Range(0f, 1.5f)] private float outsideIntensity = 1f;

    [Header("Light Transition")]
    [SerializeField, Min(0.01f)] private float lightFadeDuration = 0.6f;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private int _insideCounter;
    private Coroutine _lightFadeRoutine;
    private bool _hasCachedOutsideIntensity;

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
        if (!IsPlayer(other))
        {
            return;
        }

        _insideCounter++;

        var audio = AudioController.Instance;
        if (musicMode == ZoneMusicMode.OverlayOnTop)
        {
            audio?.PushZoneOverlayMusicPair(
                insideMusicClipA,
                insideVolumeA,
                insideLoopA,
                insideMusicClipB,
                insideVolumeB,
                insideLoopB,
                muteMainMusicInZone,
                musicFadeDuration);
        }
        else
        {
            audio?.PushZoneMusic(insideMusicClipA, insideVolumeA, insideLoopA, musicFadeDuration);
        }

        if (globalLight != null)
        {
            StartLightFade(insideIntensity);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        _insideCounter = Mathf.Max(0, _insideCounter - 1);
        if (_insideCounter > 0)
        {
            return;
        }

        var audio = AudioController.Instance;
        if (musicMode == ZoneMusicMode.OverlayOnTop)
        {
            audio?.PopZoneOverlayMusic(musicFadeDuration);
        }
        else
        {
            audio?.PopZoneMusic(musicFadeDuration);
        }

        if (globalLight != null && restoreOutsideOnExit)
        {
            var targetOutside = _hasCachedOutsideIntensity ? outsideIntensity : globalLight.intensity;
            StartLightFade(targetOutside);
        }
    }

    private void StartLightFade(float target)
    {
        if (_lightFadeRoutine != null)
        {
            StopCoroutine(_lightFadeRoutine);
        }

        _lightFadeRoutine = StartCoroutine(FadeLightRoutine(target));
    }

    private System.Collections.IEnumerator FadeLightRoutine(float target)
    {
        var start = globalLight.intensity;
        var elapsed = 0f;

        while (elapsed < lightFadeDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / lightFadeDuration);
            globalLight.intensity = Mathf.Lerp(start, target, t);
            yield return null;
        }

        globalLight.intensity = target;
        _lightFadeRoutine = null;
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
