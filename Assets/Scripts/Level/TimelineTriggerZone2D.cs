using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class TimelineTriggerZone2D : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private bool playOnFirstEnterOnly = true;
    [SerializeField] private bool restartFromBeginning = true;
    [SerializeField] private bool keepFinalStateAfterCutscene = true;
    [SerializeField] private bool requireEnterFromOutside = true;

    [Header("Player During Cutscene")]
    [SerializeField] private bool hideTriggeringPlayerDuringCutscene = true;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private Collider2D _triggerCollider;
    private bool _triggered;
    private bool _armed = true;

    private GameObject _cutscenePlayerRoot;
    private PlayerMovement _cutscenePlayerMovement;
    private PlayerInputHandler _cutscenePlayerInput;
    private PlayerAttackHandler _cutscenePlayerAttack;
    private Rigidbody2D _cutscenePlayerBody;
    private SpriteRenderer[] _cutscenePlayerRenderers = System.Array.Empty<SpriteRenderer>();

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        _triggerCollider.isTrigger = true;

        EnsureDirectorIsReadyForManualTrigger();
    }

    private void Start()
    {
        if (requireEnterFromOutside)
        {
            _armed = !IsAnyPlayerInsideTrigger();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        if (!_armed)
        {
            return;
        }

        if (timelineDirector == null)
        {
            return;
        }

        if (playOnFirstEnterOnly && _triggered)
        {
            return;
        }

        _triggered = true;
        CacheCutscenePlayer(other);
        SetCutscenePlayerVisible(visible: !hideTriggeringPlayerDuringCutscene);
        SetCutscenePlayerControlEnabled(enabled: false);

        timelineDirector.stopped -= OnTimelineStopped;
        timelineDirector.stopped += OnTimelineStopped;
        timelineDirector.extrapolationMode = keepFinalStateAfterCutscene
            ? DirectorWrapMode.Hold
            : DirectorWrapMode.None;
        timelineDirector.timeUpdateMode = DirectorUpdateMode.GameTime;

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        AudioListener.pause = false;

        if (restartFromBeginning)
        {
            timelineDirector.time = 0d;
        }

        timelineDirector.Play();
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        if (director == null)
        {
            return;
        }

        director.stopped -= OnTimelineStopped;

        if (keepFinalStateAfterCutscene)
        {
            director.time = director.duration;
            director.Evaluate();
        }

        SetCutscenePlayerVisible(visible: true);
        SetCutscenePlayerControlEnabled(enabled: true);

        // Guard: if any cutscene signal paused gameplay globally, restore normal running state.
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        AudioListener.pause = false;
    }

    private void OnDisable()
    {
        if (timelineDirector != null)
        {
            timelineDirector.stopped -= OnTimelineStopped;
        }

        // In case this object gets disabled while a cutscene is active.
        SetCutscenePlayerVisible(visible: true);
        SetCutscenePlayerControlEnabled(enabled: true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!requireEnterFromOutside || _armed || !IsPlayer(other))
        {
            return;
        }

        _armed = !IsAnyPlayerInsideTrigger();
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

        return other.GetComponentInParent<PlayerMovement>() != null
            || other.GetComponentInParent<PlayerHealth>() != null;
    }

    private bool IsAnyPlayerInsideTrigger()
    {
        if (_triggerCollider == null)
        {
            return false;
        }

        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };

        var overlaps = new Collider2D[16];
        var count = _triggerCollider.Overlap(filter, overlaps);
        for (var i = 0; i < count; i++)
        {
            if (IsPlayer(overlaps[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureDirectorIsReadyForManualTrigger()
    {
        if (timelineDirector == null)
        {
            return;
        }

        timelineDirector.playOnAwake = false;
        timelineDirector.Stop();
        timelineDirector.stopped -= OnTimelineStopped;

        if (restartFromBeginning)
        {
            timelineDirector.time = 0d;
            timelineDirector.Evaluate();
        }
    }

    private void CacheCutscenePlayer(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return;
        }

        var health = sourceCollider.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            _cutscenePlayerRoot = health.gameObject;
        }
        else
        {
            var movement = sourceCollider.GetComponentInParent<PlayerMovement>();
            _cutscenePlayerRoot = movement != null ? movement.gameObject : sourceCollider.gameObject;
        }

        if (_cutscenePlayerRoot == null)
        {
            return;
        }

        _cutscenePlayerMovement = _cutscenePlayerRoot.GetComponent<PlayerMovement>();
        _cutscenePlayerInput = _cutscenePlayerRoot.GetComponent<PlayerInputHandler>();
        _cutscenePlayerAttack = _cutscenePlayerRoot.GetComponent<PlayerAttackHandler>();
        _cutscenePlayerBody = _cutscenePlayerRoot.GetComponent<Rigidbody2D>();
        _cutscenePlayerRenderers = _cutscenePlayerRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void SetCutscenePlayerVisible(bool visible)
    {
        if (_cutscenePlayerRenderers == null)
        {
            return;
        }

        for (var i = 0; i < _cutscenePlayerRenderers.Length; i++)
        {
            var rendererRef = _cutscenePlayerRenderers[i];
            if (rendererRef == null)
            {
                continue;
            }

            rendererRef.enabled = visible;
        }
    }

    private void SetCutscenePlayerControlEnabled(bool enabled)
    {
        if (_cutscenePlayerInput != null)
        {
            _cutscenePlayerInput.enabled = enabled;
        }

        if (_cutscenePlayerAttack != null)
        {
            _cutscenePlayerAttack.enabled = enabled;
        }

        if (_cutscenePlayerMovement != null)
        {
            _cutscenePlayerMovement.enabled = enabled;
        }

        if (_cutscenePlayerBody != null)
        {
            if (!enabled)
            {
                _cutscenePlayerBody.linearVelocity = Vector2.zero;
                _cutscenePlayerBody.angularVelocity = 0f;
            }
        }

        var playerInput = _cutscenePlayerRoot != null ? _cutscenePlayerRoot.GetComponent<PlayerInput>() : null;
        if (playerInput != null)
        {
            if (enabled)
            {
                playerInput.ActivateInput();
            }
            else
            {
                playerInput.DeactivateInput();
            }
        }
    }
}
