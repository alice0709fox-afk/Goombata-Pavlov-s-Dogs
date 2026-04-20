using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private bool useRunner;
    [SerializeField] private string runnerObjectName = "RatWalk";
    [SerializeField] private Transform runner;
    [SerializeField] private Animator runnerAnimator;
    [SerializeField] private GameObject runnerCharacterRoot;
    [SerializeField] private string runnerWalkStateName = "Gangsters_2 Walk";
    [SerializeField] private string runnerWalkParameterName = "Walk";
    [SerializeField] private RectTransform runnerStartPoint;
    [SerializeField] private RectTransform runnerEndPoint;
    [SerializeField] private float runnerYOffset = 0f;
    [SerializeField] private TMP_Text quoteText;
    [SerializeField] private List<string> quotes = new List<string>();

    private void Awake()
    {
        if (quoteText != null && quotes.Count > 0)
        {
            quoteText.text = quotes[Random.Range(0, quotes.Count)];
        }

        ResolveRunnerAnimator();

        ApplyProgress(0f);
    }

    private void OnValidate()
    {
        if (useRunner && runner != null && runnerStartPoint != null && runnerEndPoint != null)
        {
            ApplyRunnerPosition(0f);
        }
    }

    private void Start()
    {
        if (useRunner)
        {
            StartRunnerAnimation();
        }

        StartCoroutine(BeginLoadingAfterFrame());
    }

    private void Update()
    {
        ApplyProgress(SceneTransitionManager.LoadingProgress);
    }

    private IEnumerator BeginLoadingAfterFrame()
    {
        yield return null;

        var manager = SceneTransitionManager.Instance;
        if (manager == null || string.IsNullOrWhiteSpace(manager.PendingTargetSceneName))
        {
            if (manager != null)
            {
                manager.ClearLoadingRequest();
            }

            yield break;
        }

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(manager.PendingTargetSceneName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"LoadingSceneController: Exception while loading '{manager.PendingTargetSceneName}': {ex.Message}", this);
            manager.ClearLoadingRequest();
            yield break;
        }

        if (operation == null)
        {
            Debug.LogError($"LoadingSceneController: Failed to load '{manager.PendingTargetSceneName}'.", this);
            manager.ClearLoadingRequest();
            yield break;
        }

        operation.allowSceneActivation = false;
        var startRealtime = Time.realtimeSinceStartup;
        var minDuration = Mathf.Max(0f, manager.MinimumLoadingScreenSeconds);

        while (true)
        {
            var asyncProgress = Mathf.Clamp01(operation.progress / 0.9f);
            var timeProgress = minDuration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.realtimeSinceStartup - startRealtime) / minDuration);

            SceneTransitionManager.LoadingProgress = Mathf.Min(asyncProgress, timeProgress);

            if (operation.progress >= 0.9f && timeProgress >= 1f)
            {
                break;
            }

            yield return null;
        }

        SceneTransitionManager.LoadingProgress = 1f;
        operation.allowSceneActivation = true;
    }

    private void ApplyProgress(float progress)
    {
        var clamped = Mathf.Clamp01(progress);

        if (loadingSlider != null)
        {
            loadingSlider.value = 1f - clamped;
        }

        if (useRunner && runner != null && runnerStartPoint != null && runnerEndPoint != null)
        {
            ApplyRunnerPosition(clamped);
        }
    }

    private void ApplyRunnerPosition(float progress)
    {
        if (runner == null || runnerStartPoint == null || runnerEndPoint == null)
        {
            return;
        }

        if (runner is RectTransform runnerRect)
        {
            var anchoredPosition = Vector2.Lerp(
                runnerStartPoint.anchoredPosition,
                runnerEndPoint.anchoredPosition,
                Mathf.Clamp01(progress));

            anchoredPosition.y += runnerYOffset;
            runnerRect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, runnerRect.anchoredPosition3D.z);
            return;
        }

        runner.position = Vector3.Lerp(runnerStartPoint.position, runnerEndPoint.position, Mathf.Clamp01(progress));
    }

    private void ResolveRunnerAnimator()
    {
        if (!useRunner)
        {
            return;
        }

        if (runnerAnimator == null && !string.IsNullOrWhiteSpace(runnerObjectName))
        {
            TryResolveRunnerFromSceneObjectName(runnerObjectName);
        }

        if (runnerAnimator == null && runner != null)
        {
            runnerAnimator = runner.GetComponent<Animator>();

            if (runnerAnimator == null)
            {
                runnerAnimator = runner.GetComponentInChildren<Animator>(true);
            }
        }

        if (runnerAnimator == null && runnerCharacterRoot != null)
        {
            runnerAnimator = runnerCharacterRoot.GetComponentInChildren<Animator>(true);
        }
    }

    private void TryResolveRunnerFromSceneObjectName(string objectName)
    {
        var scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var candidate = transforms[transformIndex];
                if (candidate == null)
                {
                    continue;
                }

                if (!candidate.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase) &&
                    !candidate.name.StartsWith(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (runner == null || !IsMatchingRunnerObject(runner, objectName))
                {
                    runner = candidate;
                }

                if (runnerAnimator == null)
                {
                    runnerAnimator = candidate.GetComponent<Animator>();

                    if (runnerAnimator == null)
                    {
                        runnerAnimator = candidate.GetComponentInChildren<Animator>(true);
                    }
                }

                if (runnerCharacterRoot == null || !IsMatchingRunnerObject(runnerCharacterRoot.transform, objectName))
                {
                    runnerCharacterRoot = candidate.gameObject;
                }

                if (runnerAnimator != null)
                {
                    return;
                }
            }
        }
    }

    private static bool IsMatchingRunnerObject(Transform transform, string objectName)
    {
        if (transform == null || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        return transform.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase)
            || transform.name.StartsWith(objectName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void StartRunnerAnimation()
    {
        if (runnerAnimator == null)
        {
            return;
        }

        if (HasBoolParameter(runnerAnimator, runnerWalkParameterName))
        {
            runnerAnimator.SetBool(runnerWalkParameterName, true);
            runnerAnimator.Update(0f);
            return;
        }

        if (!string.IsNullOrWhiteSpace(runnerWalkStateName))
        {
            runnerAnimator.Play(runnerWalkStateName, 0, 0f);
            runnerAnimator.Update(0f);
        }
    }

    private static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        var parameters = animator.parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool && parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}