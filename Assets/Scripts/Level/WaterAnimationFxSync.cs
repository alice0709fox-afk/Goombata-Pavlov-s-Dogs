using UnityEngine;

[DisallowMultipleComponent]
public class WaterAnimationFxSync : MonoBehaviour
{
    [Header("Animation Source")]
    [SerializeField] private Animator animator;
    [SerializeField] private int animatorLayer = 0;
    [SerializeField] private string watchedStateName = "";

    [Header("Fallback (if Animator is not used)")]
    [SerializeField] private AnimationClip fallbackClip;

    [Header("FX")]
    [SerializeField] private ParticleSystem fxWater;

    [Header("Timing")]
    [SerializeField] private float triggerDelay = 0f;

    [Header("Optional Vertical Override")]
    [SerializeField] private bool overrideVerticalFromNormalizedTime = false;
    [SerializeField] private Transform animatedTransform;
    [SerializeField] private float topLocalY = 0f;
    [SerializeField] private float bottomLocalY = -1f;

    private int _watchedStateHash;
    private bool _useStateFilter;

    private bool _wasInState;
    private int _lastLoopIndex;

    private bool _capturedTopY;

    private float _fallbackElapsed;
    private float _fallbackDuration;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animatedTransform == null)
        {
            animatedTransform = animator != null ? animator.transform : transform;
        }

        if (fxWater == null)
        {
            fxWater = GetComponentInChildren<ParticleSystem>(true);
        }

        _useStateFilter = !string.IsNullOrWhiteSpace(watchedStateName);
        if (_useStateFilter)
        {
            _watchedStateHash = Animator.StringToHash(watchedStateName);
        }

        _fallbackDuration = fallbackClip != null ? Mathf.Max(0f, fallbackClip.length) : 0f;
        triggerDelay = Mathf.Max(0f, triggerDelay);

        if (overrideVerticalFromNormalizedTime && !_capturedTopY && animatedTransform != null)
        {
            topLocalY = animatedTransform.localPosition.y;
            _capturedTopY = true;
        }
    }

    private void OnEnable()
    {
        _wasInState = false;
        _lastLoopIndex = -1;
        _fallbackElapsed = 0f;

        if (fxWater != null)
        {
            fxWater.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (TrySyncFromAnimator())
        {
            return;
        }

        SyncFromFallbackTimer();
    }

    private bool TrySyncFromAnimator()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return false;
        }

        var stateInfo = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        var isWatchedState = !_useStateFilter || stateInfo.shortNameHash == _watchedStateHash;

        if (!isWatchedState)
        {
            _wasInState = false;
            _lastLoopIndex = -1;
            return true;
        }

        var normalizedTime = stateInfo.normalizedTime;
        var currentLoop = Mathf.FloorToInt(normalizedTime);
        var cycleTime = normalizedTime - currentLoop;

        ApplyVerticalOverride(cycleTime);

        if (!_wasInState)
        {
            _wasInState = true;
            _lastLoopIndex = currentLoop;
            return true;
        }

        if (currentLoop > _lastLoopIndex)
        {
            _lastLoopIndex = currentLoop;
            TriggerFxWater();
        }

        return true;
    }

    private void ApplyVerticalOverride(float cycleTime)
    {
        if (!overrideVerticalFromNormalizedTime || animatedTransform == null)
        {
            return;
        }

        var pos = animatedTransform.localPosition;
        pos.y = Mathf.Lerp(topLocalY, bottomLocalY, Mathf.Clamp01(cycleTime));
        animatedTransform.localPosition = pos;
    }

    private void SyncFromFallbackTimer()
    {
        if (_fallbackDuration <= 0f)
        {
            return;
        }

        _fallbackElapsed += Time.deltaTime;

        while (_fallbackElapsed >= _fallbackDuration)
        {
            _fallbackElapsed -= _fallbackDuration;
            TriggerFxWater();
        }
    }

    private void TriggerFxWater()
    {
        if (fxWater == null)
        {
            return;
        }

        if (triggerDelay <= 0f)
        {
            PlayFxNow();
            return;
        }

        CancelInvoke(nameof(PlayFxNow));
        Invoke(nameof(PlayFxNow), triggerDelay);
    }

    private void PlayFxNow()
    {
        if (fxWater == null)
        {
            return;
        }

        fxWater.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fxWater.Play(true);
    }
}
