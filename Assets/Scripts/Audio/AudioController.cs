using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioController : MonoBehaviour
{
    [Serializable]
    private class SceneMusicEntry
    {
        public string sceneName;
        public AudioClip musicClip;
        [Range(0f, 1f)] public float volume = 0.5f;
        public bool loop = true;
    }

    private static AudioController _instance;

    [SerializeField] private List<SceneMusicEntry> sceneMusic = new List<SceneMusicEntry>();
    [SerializeField] private bool stopWhenSceneHasNoMusic = true;
    [Header("Player Sfx")]
    [SerializeField] private AudioClip playerHitClip;
    [SerializeField] private AudioClip playerHurtClip;
    [SerializeField] [Range(0f, 1f)] private float playerHitVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float playerHurtVolume = 1f;
    [Header("Overlay Sfx")]
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] [Range(0f, 1f)] private float gameOverVolume = 1f;
    [Header("Pickup Sfx")]
    [SerializeField] private AudioClip moneyPickupClip;
    [SerializeField] private AudioClip kolaUseClip;
    [SerializeField] [Range(0f, 1f)] private float moneyPickupVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float kolaUseVolume = 1f;

    private AudioSource _musicSource;
    private AudioSource _sfxSource;
    private AudioSource _overlaySource;
    private AudioSource _zoneLayerSource;
    private AudioSource _zoneLayerSourceSecondary;
    private Coroutine _musicFadeRoutine;
    private Coroutine _zoneLayerFadeRoutine;
    private Coroutine _zoneLayerSecondaryFadeRoutine;
    private Coroutine _musicDuckRoutine;
    private bool _exclusiveAudioLocked;
    private SceneMusicEntry _currentSceneMusic;
    private bool _zoneMusicActive;
    private int _zoneMusicDepth;
    private AudioClip _zoneMusicClip;
    private float _zoneMusicVolume = 0.5f;
    private bool _zoneMusicLoop = true;
    private bool _zoneLayerActive;
    private int _zoneLayerDepth;
    private AudioClip _zoneLayerClip;
    private float _zoneLayerVolume = 0.35f;
    private bool _zoneLayerLoop = true;
    private AudioClip _zoneLayerSecondaryClip;
    private float _zoneLayerSecondaryVolume = 0.25f;
    private bool _zoneLayerSecondaryLoop = true;
    private bool _zoneLayerMuteMainMusic;

    public static AudioController Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _musicSource = GetComponent<AudioSource>();
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
        }

        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;

        _overlaySource = gameObject.AddComponent<AudioSource>();
        _overlaySource.playOnAwake = false;
        _overlaySource.loop = false;
        _overlaySource.spatialBlend = 0f;

        _zoneLayerSource = gameObject.AddComponent<AudioSource>();
        _zoneLayerSource.playOnAwake = false;
        _zoneLayerSource.loop = true;
        _zoneLayerSource.spatialBlend = 0f;

        _zoneLayerSourceSecondary = gameObject.AddComponent<AudioSource>();
        _zoneLayerSourceSecondary.playOnAwake = false;
        _zoneLayerSourceSecondary.loop = true;
        _zoneLayerSourceSecondary.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ApplyMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearExclusiveAudioLock();
        _zoneMusicActive = false;
        _zoneMusicDepth = 0;
        _zoneMusicClip = null;
        _zoneLayerActive = false;
        _zoneLayerDepth = 0;
        _zoneLayerClip = null;
        _zoneLayerSecondaryClip = null;
        _zoneLayerMuteMainMusic = false;
        StopZoneLayerImmediately();
        ApplyMusicForScene(scene.name);
    }

    private void ApplyMusicForScene(string sceneName)
    {
        if (_exclusiveAudioLocked || _zoneMusicActive)
        {
            return;
        }

        var entry = FindEntry(sceneName);
        _currentSceneMusic = entry;
        if (entry == null)
        {
            if (stopWhenSceneHasNoMusic)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }

            return;
        }

        PlayMusic(entry.musicClip, entry.volume, entry.loop, 0f);
    }

    private SceneMusicEntry FindEntry(string sceneName)
    {
        for (var i = 0; i < sceneMusic.Count; i++)
        {
            var entry = sceneMusic[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName))
            {
                continue;
            }

            if (string.Equals(entry.sceneName.Trim(), sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    public void PlayPlayerHit()
    {
        PlayOneShot(playerHitClip, playerHitVolume);
    }

    public void PlayPlayerHurt()
    {
        PlayOneShot(playerHurtClip, playerHurtVolume);
    }

    public void PlayGameOverExclusive()
    {
        if (_overlaySource == null || gameOverClip == null)
        {
            return;
        }

        _exclusiveAudioLocked = true;
        _musicSource?.Stop();
        _sfxSource?.Stop();
        _overlaySource.Stop();
        _zoneLayerSource?.Stop();
        _zoneLayerSourceSecondary?.Stop();
        _overlaySource.clip = gameOverClip;
        _overlaySource.volume = Mathf.Clamp01(gameOverVolume);
        _overlaySource.Play();
    }

    public void PlayMoneyPickup()
    {
        PlayOneShot(moneyPickupClip, moneyPickupVolume);
    }

    public void PlayKolaUse()
    {
        PlayOneShot(kolaUseClip, kolaUseVolume);
    }

    public void ClearExclusiveAudioLock()
    {
        if (!_exclusiveAudioLocked)
        {
            return;
        }

        _exclusiveAudioLocked = false;

        if (_overlaySource != null && _overlaySource.clip == gameOverClip)
        {
            _overlaySource.Stop();
            _overlaySource.clip = null;
        }
    }

    public float GetMusicVolume()
    {
        return GameSettings.MusicVolume;
    }

    public void SetMusicVolume(float volume)
    {
        GameSettings.MusicVolume = volume;
        RefreshMusicVolume();
    }

    public void RefreshMusicVolume()
    {
        if (_exclusiveAudioLocked || _musicSource == null)
        {
            return;
        }

        if (_zoneMusicActive)
        {
            _musicSource.volume = GetEffectiveMusicVolume(_zoneMusicVolume);
        }
        else if (_currentSceneMusic != null)
        {
            _musicSource.volume = GetEffectiveMusicVolume(_currentSceneMusic.volume);
        }

        if (_zoneLayerActive && _zoneLayerMuteMainMusic)
        {
            _musicSource.volume = 0f;
        }

        if (_zoneLayerActive && _zoneLayerSource != null)
        {
            _zoneLayerSource.volume = GetEffectiveMusicVolume(_zoneLayerVolume);
        }

        if (_zoneLayerActive && _zoneLayerSourceSecondary != null)
        {
            _zoneLayerSourceSecondary.volume = GetEffectiveMusicVolume(_zoneLayerSecondaryVolume);
        }
    }

    public void PushZoneMusic(AudioClip clip, float volume = 0.5f, bool loop = true, float fadeDuration = 0.45f)
    {
        if (_exclusiveAudioLocked)
        {
            return;
        }

        _zoneMusicDepth++;
        if (clip == null)
        {
            return;
        }

        _zoneMusicActive = true;
        _zoneMusicClip = clip;
        _zoneMusicVolume = Mathf.Clamp01(volume);
        _zoneMusicLoop = loop;

        PlayMusic(_zoneMusicClip, _zoneMusicVolume, _zoneMusicLoop, fadeDuration);
    }

    public void PopZoneMusic(float fadeDuration = 0.45f)
    {
        _zoneMusicDepth = Mathf.Max(0, _zoneMusicDepth - 1);
        if (_zoneMusicDepth > 0)
        {
            return;
        }

        _zoneMusicActive = false;
        _zoneMusicClip = null;

        if (_currentSceneMusic != null)
        {
            PlayMusic(_currentSceneMusic.musicClip, _currentSceneMusic.volume, _currentSceneMusic.loop, fadeDuration);
            return;
        }

        if (stopWhenSceneHasNoMusic)
        {
            StopMusic(fadeDuration);
        }
    }

    public void PushZoneOverlayMusic(AudioClip clip, float volume = 0.35f, bool loop = true, float fadeDuration = 0.45f)
    {
        PushZoneOverlayMusicPair(clip, volume, loop, null, 0f, true, false, fadeDuration);
    }

    public void PushZoneOverlayMusicPair(
        AudioClip primaryClip,
        float primaryVolume = 0.35f,
        bool primaryLoop = true,
        AudioClip secondaryClip = null,
        float secondaryVolume = 0.25f,
        bool secondaryLoop = true,
        bool muteMainMusic = true,
        float fadeDuration = 0.45f)
    {
        if (_exclusiveAudioLocked)
        {
            return;
        }

        _zoneLayerDepth++;
        if (primaryClip == null && secondaryClip == null)
        {
            return;
        }

        _zoneLayerActive = true;
        _zoneLayerClip = primaryClip;
        _zoneLayerVolume = Mathf.Clamp01(primaryVolume);
        _zoneLayerLoop = primaryLoop;
        _zoneLayerSecondaryClip = secondaryClip;
        _zoneLayerSecondaryVolume = Mathf.Clamp01(secondaryVolume);
        _zoneLayerSecondaryLoop = secondaryLoop;
        _zoneLayerMuteMainMusic = muteMainMusic;

        PlayZoneLayer(_zoneLayerSource, ref _zoneLayerFadeRoutine, _zoneLayerClip, _zoneLayerVolume, _zoneLayerLoop, fadeDuration);
        PlayZoneLayer(_zoneLayerSourceSecondary, ref _zoneLayerSecondaryFadeRoutine, _zoneLayerSecondaryClip, _zoneLayerSecondaryVolume, _zoneLayerSecondaryLoop, fadeDuration);
        StartMusicDucking(_zoneLayerMuteMainMusic, fadeDuration);
    }

    public void PopZoneOverlayMusic(float fadeDuration = 0.45f)
    {
        _zoneLayerDepth = Mathf.Max(0, _zoneLayerDepth - 1);
        if (_zoneLayerDepth > 0)
        {
            return;
        }

        _zoneLayerActive = false;
        _zoneLayerClip = null;
        _zoneLayerSecondaryClip = null;

        StopZoneLayer(_zoneLayerSource, ref _zoneLayerFadeRoutine, fadeDuration);
        StopZoneLayer(_zoneLayerSourceSecondary, ref _zoneLayerSecondaryFadeRoutine, fadeDuration);
        StartMusicDucking(false, fadeDuration);
        _zoneLayerMuteMainMusic = false;
    }

    private void PlayMusic(AudioClip clip, float baseVolume, bool loop, float fadeDuration)
    {
        if (_musicSource == null)
        {
            return;
        }

        if (_musicFadeRoutine != null)
        {
            StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = null;
        }

        var targetVolume = GetEffectiveMusicVolume(baseVolume);
        var safeFade = Mathf.Max(0f, fadeDuration);

        if (safeFade <= 0f)
        {
            _musicSource.loop = loop;
            _musicSource.clip = clip;
            if (clip == null)
            {
                _musicSource.Stop();
                _musicSource.volume = 0f;
                return;
            }

            _musicSource.volume = targetVolume;
            if (!_musicSource.isPlaying)
            {
                _musicSource.Play();
            }

            return;
        }

        _musicFadeRoutine = StartCoroutine(FadeAndSwitchMusic(clip, targetVolume, loop, safeFade));
    }

    private void StopMusic(float fadeDuration)
    {
        PlayMusic(null, 0f, false, fadeDuration);
    }

    private void PlayZoneLayer(AudioSource source, ref Coroutine fadeRoutine, AudioClip clip, float baseVolume, bool loop, float fadeDuration)
    {
        if (source == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        var targetVolume = GetEffectiveMusicVolume(baseVolume);
        var safeFade = Mathf.Max(0f, fadeDuration);

        if (safeFade <= 0f)
        {
            source.loop = loop;
            source.clip = clip;
            if (clip == null)
            {
                source.Stop();
                source.volume = 0f;
                return;
            }

            source.volume = targetVolume;
            if (!source.isPlaying)
            {
                source.Play();
            }

            return;
        }

        fadeRoutine = StartCoroutine(FadeAndSwitchZoneLayer(source, clip, targetVolume, loop, safeFade));
    }

    private void StopZoneLayer(AudioSource source, ref Coroutine fadeRoutine, float fadeDuration)
    {
        PlayZoneLayer(source, ref fadeRoutine, null, 0f, false, fadeDuration);
    }

    private void StopZoneLayerImmediately()
    {
        if (_zoneLayerFadeRoutine != null)
        {
            StopCoroutine(_zoneLayerFadeRoutine);
            _zoneLayerFadeRoutine = null;
        }

        if (_zoneLayerSecondaryFadeRoutine != null)
        {
            StopCoroutine(_zoneLayerSecondaryFadeRoutine);
            _zoneLayerSecondaryFadeRoutine = null;
        }

        if (_musicDuckRoutine != null)
        {
            StopCoroutine(_musicDuckRoutine);
            _musicDuckRoutine = null;
        }

        if (_zoneLayerSource == null)
        {
            return;
        }

        _zoneLayerSource.Stop();
        _zoneLayerSource.clip = null;
        _zoneLayerSource.volume = 0f;

        if (_zoneLayerSourceSecondary != null)
        {
            _zoneLayerSourceSecondary.Stop();
            _zoneLayerSourceSecondary.clip = null;
            _zoneLayerSourceSecondary.volume = 0f;
        }
    }

    private System.Collections.IEnumerator FadeAndSwitchMusic(AudioClip clip, float targetVolume, bool loop, float fadeDuration)
    {
        var startVolume = _musicSource.volume;

        if (_musicSource.isPlaying && _musicSource.clip != null && _musicSource.clip != clip)
        {
            var outTime = 0f;
            while (outTime < fadeDuration)
            {
                outTime += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(outTime / fadeDuration);
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            _musicSource.volume = 0f;
            _musicSource.Stop();
        }

        _musicSource.loop = loop;
        _musicSource.clip = clip;

        if (clip == null)
        {
            _musicSource.volume = 0f;
            _musicFadeRoutine = null;
            yield break;
        }

        if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }

        var inStartVolume = _musicSource.volume;
        var inTime = 0f;
        while (inTime < fadeDuration)
        {
            inTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(inTime / fadeDuration);
            _musicSource.volume = Mathf.Lerp(inStartVolume, targetVolume, t);
            yield return null;
        }

        _musicSource.volume = targetVolume;
        _musicFadeRoutine = null;
    }

    private void StartMusicDucking(bool mute, float fadeDuration)
    {
        if (_musicSource == null)
        {
            return;
        }

        var target = mute ? 0f : ResolveMainMusicTargetVolume();

        if (_musicDuckRoutine != null)
        {
            StopCoroutine(_musicDuckRoutine);
            _musicDuckRoutine = null;
        }

        var safeFade = Mathf.Max(0f, fadeDuration);
        if (safeFade <= 0f)
        {
            _musicSource.volume = target;
            return;
        }

        _musicDuckRoutine = StartCoroutine(FadeMusicVolumeOnly(target, safeFade));
    }

    private float ResolveMainMusicTargetVolume()
    {
        if (_zoneMusicActive)
        {
            return GetEffectiveMusicVolume(_zoneMusicVolume);
        }

        if (_currentSceneMusic != null)
        {
            return GetEffectiveMusicVolume(_currentSceneMusic.volume);
        }

        return 0f;
    }

    private System.Collections.IEnumerator FadeMusicVolumeOnly(float targetVolume, float fadeDuration)
    {
        var start = _musicSource.volume;
        var elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / fadeDuration);
            _musicSource.volume = Mathf.Lerp(start, targetVolume, t);
            yield return null;
        }

        _musicSource.volume = targetVolume;
        _musicDuckRoutine = null;
    }

    private System.Collections.IEnumerator FadeAndSwitchZoneLayer(
        AudioSource source,
        AudioClip clip,
        float targetVolume,
        bool loop,
        float fadeDuration)
    {
        var startVolume = source.volume;

        if (source.isPlaying && source.clip != null && source.clip != clip)
        {
            var outTime = 0f;
            while (outTime < fadeDuration)
            {
                outTime += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(outTime / fadeDuration);
                source.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            source.volume = 0f;
            source.Stop();
        }

        source.loop = loop;
        source.clip = clip;

        if (clip == null)
        {
            source.volume = 0f;
            yield break;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }

        var inStartVolume = source.volume;
        var inTime = 0f;
        while (inTime < fadeDuration)
        {
            inTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(inTime / fadeDuration);
            source.volume = Mathf.Lerp(inStartVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (_exclusiveAudioLocked || _sfxSource == null || clip == null)
        {
            return;
        }

        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static float GetEffectiveMusicVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume * GameSettings.MusicVolume);
    }
}
