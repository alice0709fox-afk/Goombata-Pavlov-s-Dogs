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
    private bool _exclusiveAudioLocked;
    private SceneMusicEntry _currentSceneMusic;

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
        ApplyMusicForScene(scene.name);
    }

    private void ApplyMusicForScene(string sceneName)
    {
        if (_exclusiveAudioLocked)
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

        var clipChanged = _musicSource.clip != entry.musicClip;
        _musicSource.loop = entry.loop;
    _musicSource.volume = GetEffectiveMusicVolume(entry.volume);

        if (clipChanged)
        {
            _musicSource.clip = entry.musicClip;
            if (_musicSource.clip != null)
            {
                _musicSource.Play();
            }

            return;
        }

        if (_musicSource.clip != null && !_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
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
        if (_exclusiveAudioLocked || _musicSource == null || _currentSceneMusic == null)
        {
            return;
        }

        _musicSource.volume = GetEffectiveMusicVolume(_currentSceneMusic.volume);
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
