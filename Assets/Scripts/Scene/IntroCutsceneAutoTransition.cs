using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class IntroCutsceneAutoTransition
{
    private const string IntroSceneName = "3_Intro";
    private const string GameplaySceneName = "Level_0";
    private const double IntroEndFrame = 1071d;
    private const double IntroTimelineFrameRate = 60d;

    private static PlayableDirector _boundDirector;
    private static Coroutine _watchCutsceneRoutine;
    private static bool _isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopWatchRoutine();
        _isTransitioning = false;
        UnbindDirector();

        if (!string.Equals(scene.name, IntroSceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        var director = Object.FindFirstObjectByType<PlayableDirector>();
        if (director == null)
        {
            Debug.LogWarning($"IntroCutsceneAutoTransition: No PlayableDirector found in scene '{IntroSceneName}'. Loading '{GameplaySceneName}' immediately.");
            SceneManager.LoadScene(GameplaySceneName);
            return;
        }

        _boundDirector = director;
        _boundDirector.extrapolationMode = DirectorWrapMode.Hold;
        _boundDirector.stopped += OnDirectorStopped;
        StartWatchRoutine();

        if (_boundDirector.state != PlayState.Playing)
        {
            _boundDirector.Play();
        }
    }

    private static void OnDirectorStopped(PlayableDirector director)
    {
        if (director != _boundDirector)
        {
            return;
        }

        TriggerGameplayTransition();
    }

    private static void StartWatchRoutine()
    {
        var host = SceneTransitionManager.Instance;
        if (host == null)
        {
            return;
        }

        _watchCutsceneRoutine = host.StartCoroutine(WatchCutsceneEndFrame());
    }

    private static void StopWatchRoutine()
    {
        if (_watchCutsceneRoutine == null)
        {
            return;
        }

        var host = SceneTransitionManager.Instance;
        if (host != null)
        {
            host.StopCoroutine(_watchCutsceneRoutine);
        }

        _watchCutsceneRoutine = null;
    }

    private static IEnumerator WatchCutsceneEndFrame()
    {
        var transitionTime = IntroEndFrame / IntroTimelineFrameRate;

        while (_boundDirector != null && !_isTransitioning)
        {
            if (_boundDirector.time >= transitionTime)
            {
                TriggerGameplayTransition();
                yield break;
            }

            yield return null;
        }
    }

    private static void TriggerGameplayTransition()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        StopWatchRoutine();
        UnbindDirector();
        SceneManager.LoadScene(GameplaySceneName);
    }

    private static void UnbindDirector()
    {
        if (_boundDirector == null)
        {
            return;
        }

        _boundDirector.stopped -= OnDirectorStopped;
        _boundDirector = null;
    }
}
