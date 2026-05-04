using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Unity.Cinemachine;

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
    [SerializeField] private bool forcePlayerPositionAfterCutscene = true;
    [SerializeField] private Vector3 playerPositionAfterCutscene = new Vector3(432.220215f, -132.739212f, 0f);

    [Header("Cutscene Camera")]
    [SerializeField] private GameObject cutsceneCameraToDisableOnFinish;
    [SerializeField] private string fallbackCutsceneCameraName = "CinemachineCamera (2)";
    [SerializeField] private GameObject cutsceneCameraRootToDisableOnFinish;
    [SerializeField] private string fallbackCutsceneCameraRootName = "CameraCM";
    [SerializeField] private GameObject playerCameraToEnableOnFinish;
    [SerializeField] private string fallbackPlayerCameraName = "CinemachineCameraPlayer";
    [SerializeField] private bool disablePlayerCameraDuringCutscene = true;
    [SerializeField] private int cameraRebindFramesAfterCutscene = 20;
    [SerializeField] private float cameraEnforceSecondsAfterCutscene = 6f;
    [SerializeField] private bool logCameraRebindInfo;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    private Collider2D _triggerCollider;
    private bool _triggered;
    private bool _armed = true;
    private bool _cutsceneCompletionHandled;

    private GameObject _playerRoot;
    private PlayerMovement _playerMovement;
    private PlayerInputHandler _playerInputHandler;
    private PlayerAttackHandler _playerAttackHandler;
    private Rigidbody2D _playerBody;
    private SpriteRenderer[] _playerRenderers = System.Array.Empty<SpriteRenderer>();

    private Coroutine _cutsceneWatchdogRoutine;
    private Coroutine _cameraRebindRoutine;
    private Coroutine _mainCameraRefreshRoutine;
    private float _forcePlayerCameraUntilUnscaledTime;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        _triggerCollider.isTrigger = true;

        PrepareDirector();
        ResolveCameraReferences();
    }

    private void Start()
    {
        if (requireEnterFromOutside)
        {
            _armed = !IsAnyPlayerInsideTrigger();
        }
    }

    private void LateUpdate()
    {
        if (DeathScreenManager.IsDeathFlowActive)
        {
            _forcePlayerCameraUntilUnscaledTime = 0f;
            CinemachineCore.SoloCamera = null;
            return;
        }

        if (Time.unscaledTime > _forcePlayerCameraUntilUnscaledTime)
        {
            return;
        }

        var target = ResolvePlayerCameraTarget();
        SwitchToPlayerCamera();
        BindPlayerCameraToTarget(target);
        DisableShakeOnNonPlayerCameras();
    }

    private void OnDisable()
    {
        if (timelineDirector != null)
        {
            timelineDirector.stopped -= OnTimelineStopped;
        }

        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        if (_cameraRebindRoutine != null)
        {
            StopCoroutine(_cameraRebindRoutine);
            _cameraRebindRoutine = null;
        }

        if (_mainCameraRefreshRoutine != null)
        {
            StopCoroutine(_mainCameraRefreshRoutine);
            _mainCameraRefreshRoutine = null;
        }

        _forcePlayerCameraUntilUnscaledTime = 0f;

        SetPlayerVisible(true);
        SetPlayerControlEnabled(true);
        CinemachineCore.SoloCamera = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (DeathScreenManager.IsDeathFlowActive || !_armed || timelineDirector == null)
        {
            return;
        }

        if (!TryResolveAlivePlayer(other))
        {
            return;
        }

        if (playOnFirstEnterOnly && _triggered)
        {
            return;
        }

        _triggered = true;
        _cutsceneCompletionHandled = false;

        CachePlayer(other);
        SetPlayerVisible(!hideTriggeringPlayerDuringCutscene);
        SetPlayerControlEnabled(false);

        ResolveCameraReferences();
        if (disablePlayerCameraDuringCutscene && playerCameraToEnableOnFinish != null)
        {
            playerCameraToEnableOnFinish.SetActive(false);
        }

        if (cutsceneCameraRootToDisableOnFinish != null)
        {
            cutsceneCameraRootToDisableOnFinish.SetActive(true);
        }

        if (cutsceneCameraToDisableOnFinish != null)
        {
            cutsceneCameraToDisableOnFinish.SetActive(true);
        }

        timelineDirector.stopped -= OnTimelineStopped;
        timelineDirector.stopped += OnTimelineStopped;
        timelineDirector.extrapolationMode = keepFinalStateAfterCutscene ? DirectorWrapMode.Hold : DirectorWrapMode.None;
        timelineDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        timelineDirector.enabled = true;

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

        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
        }

        _cutsceneWatchdogRoutine = StartCoroutine(CutsceneWatchdogRoutine());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!requireEnterFromOutside || _armed || !IsPlayer(other))
        {
            return;
        }

        _armed = !IsAnyPlayerInsideTrigger();
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        CompleteCutscene(director);
    }

    private void CompleteCutscene(PlayableDirector director)
    {
        if (_cutsceneCompletionHandled || director == null)
        {
            return;
        }

        if (DeathScreenManager.IsDeathFlowActive)
        {
            AbortCutsceneFlow();
            return;
        }

        _cutsceneCompletionHandled = true;

        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        director.stopped -= OnTimelineStopped;

        if (director.state == PlayState.Playing)
        {
            director.Stop();
        }

        if (keepFinalStateAfterCutscene)
        {
            director.time = director.duration;
            director.Evaluate();
        }

        director.enabled = false;

        RefreshPlayerFromScene();
        ApplyPlayerPositionAfterCutscene();

        var target = ResolvePlayerCameraTarget();
        SwitchToPlayerCamera();
        BindPlayerCameraToTarget(target);

        if (_cameraRebindRoutine != null)
        {
            StopCoroutine(_cameraRebindRoutine);
        }

        _cameraRebindRoutine = StartCoroutine(ForcePlayerCameraRoutine(target));
        _forcePlayerCameraUntilUnscaledTime = Time.unscaledTime + Mathf.Max(cameraEnforceSecondsAfterCutscene, 0f);
        RefreshMainCameraAfterCutscene();

        SetPlayerVisible(true);
        SetPlayerControlEnabled(true);

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        AudioListener.pause = false;
    }

    private System.Collections.IEnumerator CutsceneWatchdogRoutine()
    {
        if (timelineDirector == null)
        {
            yield break;
        }

        var timeout = Mathf.Max((float)timelineDirector.duration + 1f, 5f);
        var elapsed = 0f;

        while (!_cutsceneCompletionHandled && elapsed < timeout)
        {
            if (timelineDirector.state != PlayState.Playing && timelineDirector.time > 0d)
            {
                CompleteCutscene(timelineDirector);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_cutsceneCompletionHandled)
        {
            CompleteCutscene(timelineDirector);
        }
    }

    private System.Collections.IEnumerator ForcePlayerCameraRoutine(Transform target)
    {
        var frames = Mathf.Max(1, cameraRebindFramesAfterCutscene);
        var playerVcam = playerCameraToEnableOnFinish != null
            ? playerCameraToEnableOnFinish.GetComponent<CinemachineVirtualCameraBase>()
            : null;

        for (var i = 0; i < frames; i++)
        {
            SwitchToPlayerCamera();
            BindPlayerCameraToTarget(target);
            DisableShakeOnNonPlayerCameras();

            if (playerVcam != null)
            {
                CinemachineCore.SoloCamera = playerVcam;
            }

            yield return null;
        }

        CinemachineCore.SoloCamera = null;
        _cameraRebindRoutine = null;
    }

    private void SwitchToPlayerCamera()
    {
        ResolveCameraReferences();

        if (cutsceneCameraRootToDisableOnFinish != null)
        {
            cutsceneCameraRootToDisableOnFinish.SetActive(false);
        }

        if (cutsceneCameraToDisableOnFinish != null)
        {
            cutsceneCameraToDisableOnFinish.SetActive(false);
        }

        if (playerCameraToEnableOnFinish != null)
        {
            playerCameraToEnableOnFinish.SetActive(true);
        }
    }

    private void BindPlayerCameraToTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (playerCameraToEnableOnFinish == null)
        {
            return;
        }

        var playerVcam = playerCameraToEnableOnFinish.GetComponent<CinemachineVirtualCameraBase>();
        if (playerVcam != null)
        {
            playerVcam.Priority = 100;
            playerVcam.Follow = target;
            if (playerVcam.LookAt == null)
            {
                playerVcam.LookAt = target;
            }
        }

        var playerCinemachineCamera = playerCameraToEnableOnFinish.GetComponent<CinemachineCamera>();
        if (playerCinemachineCamera != null)
        {
            var cameraTarget = playerCinemachineCamera.Target;
            cameraTarget.TrackingTarget = target;
            if (cameraTarget.LookAtTarget == null)
            {
                cameraTarget.LookAtTarget = target;
            }

            playerCinemachineCamera.Target = cameraTarget;
        }

        var cutsceneVcam = cutsceneCameraToDisableOnFinish != null
            ? cutsceneCameraToDisableOnFinish.GetComponent<CinemachineVirtualCameraBase>()
            : null;
        if (cutsceneVcam != null)
        {
            cutsceneVcam.Priority = -100;
        }

        if (logCameraRebindInfo)
        {
            Debug.Log($"TimelineTriggerZone2D: Player camera '{playerCameraToEnableOnFinish.name}' bound to '{target.name}'.", this);
        }
    }

    private void DisableShakeOnNonPlayerCameras()
    {
        var cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            if (playerCameraToEnableOnFinish != null && camera.gameObject == playerCameraToEnableOnFinish)
            {
                continue;
            }

            var noise = camera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
            {
                noise.AmplitudeGain = 0f;
                noise.FrequencyGain = 0f;
                noise.enabled = false;
            }

            if (string.Equals(camera.gameObject.name, fallbackCutsceneCameraName, System.StringComparison.Ordinal))
            {
                camera.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshMainCameraAfterCutscene()
    {
        if (_mainCameraRefreshRoutine != null)
        {
            StopCoroutine(_mainCameraRefreshRoutine);
        }

        _mainCameraRefreshRoutine = StartCoroutine(RefreshMainCameraRoutine());
    }

    private System.Collections.IEnumerator RefreshMainCameraRoutine()
    {
        // Wait one frame so timeline state and camera activation changes are fully applied.
        yield return null;

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            _mainCameraRefreshRoutine = null;
            yield break;
        }

        var brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.enabled = false;
            yield return null;
            brain.enabled = true;
        }

        var cameraComponent = mainCamera.GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.enabled = false;
            yield return null;
            cameraComponent.enabled = true;
        }

        CinemachineCore.SoloCamera = null;
        _mainCameraRefreshRoutine = null;
    }

    private Transform ResolvePlayerCameraTarget()
    {
        if (_playerBody != null)
        {
            return _playerBody.transform;
        }

        if (_playerMovement != null)
        {
            return _playerMovement.transform;
        }

        if (_playerRoot != null)
        {
            return _playerRoot.transform;
        }

        var input = FindFirstObjectByType<PlayerInput>();
        if (input != null)
        {
            var body = input.GetComponentInChildren<Rigidbody2D>(true);
            return body != null ? body.transform : input.transform;
        }

        return null;
    }

    private void PrepareDirector()
    {
        if (timelineDirector == null)
        {
            return;
        }

        timelineDirector.playOnAwake = false;
        timelineDirector.Stop();
        timelineDirector.stopped -= OnTimelineStopped;
        timelineDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

        if (restartFromBeginning)
        {
            timelineDirector.time = 0d;
            timelineDirector.Evaluate();
        }
    }

    private void ResolveCameraReferences()
    {
        if (cutsceneCameraToDisableOnFinish == null && !string.IsNullOrWhiteSpace(fallbackCutsceneCameraName))
        {
            cutsceneCameraToDisableOnFinish = GameObject.Find(fallbackCutsceneCameraName);
        }

        if (cutsceneCameraRootToDisableOnFinish == null && !string.IsNullOrWhiteSpace(fallbackCutsceneCameraRootName))
        {
            cutsceneCameraRootToDisableOnFinish = GameObject.Find(fallbackCutsceneCameraRootName);
        }

        if (playerCameraToEnableOnFinish == null)
        {
            var cameras = FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                if (string.Equals(camera.gameObject.name, fallbackPlayerCameraName, System.StringComparison.Ordinal))
                {
                    playerCameraToEnableOnFinish = camera.gameObject;
                    break;
                }
            }
        }

        if (cutsceneCameraRootToDisableOnFinish == null && cutsceneCameraToDisableOnFinish != null)
        {
            var parent = cutsceneCameraToDisableOnFinish.transform.parent;
            cutsceneCameraRootToDisableOnFinish = parent != null ? parent.gameObject : cutsceneCameraToDisableOnFinish;
        }
    }

    private void CachePlayer(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return;
        }

        var health = sourceCollider.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            _playerRoot = health.gameObject;
        }
        else
        {
            var movement = sourceCollider.GetComponentInParent<PlayerMovement>();
            _playerRoot = movement != null ? movement.gameObject : sourceCollider.gameObject;
        }

        RefreshCachedPlayerComponents();
    }

    private void RefreshPlayerFromScene()
    {
        var input = FindFirstObjectByType<PlayerInput>();
        if (input != null)
        {
            var health = input.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                _playerRoot = health.gameObject;
            }
            else
            {
                _playerRoot = input.gameObject;
            }

            RefreshCachedPlayerComponents();
        }
    }

    private void RefreshCachedPlayerComponents()
    {
        if (_playerRoot == null)
        {
            return;
        }

        _playerMovement = _playerRoot.GetComponentInChildren<PlayerMovement>(true);
        _playerInputHandler = _playerRoot.GetComponentInChildren<PlayerInputHandler>(true);
        _playerAttackHandler = _playerRoot.GetComponentInChildren<PlayerAttackHandler>(true);
        _playerBody = _playerRoot.GetComponentInChildren<Rigidbody2D>(true);
        _playerRenderers = _playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void SetPlayerVisible(bool visible)
    {
        if (_playerRenderers == null)
        {
            return;
        }

        for (var i = 0; i < _playerRenderers.Length; i++)
        {
            var rendererRef = _playerRenderers[i];
            if (rendererRef != null)
            {
                rendererRef.enabled = visible;
            }
        }
    }

    private void SetPlayerControlEnabled(bool enabled)
    {
        if (_playerInputHandler != null)
        {
            _playerInputHandler.enabled = enabled;
        }

        if (_playerAttackHandler != null)
        {
            _playerAttackHandler.enabled = enabled;
        }

        if (_playerMovement != null)
        {
            _playerMovement.enabled = enabled;
        }

        if (_playerBody != null && !enabled)
        {
            _playerBody.linearVelocity = Vector2.zero;
            _playerBody.angularVelocity = 0f;
        }

        var playerInput = _playerRoot != null ? _playerRoot.GetComponentInChildren<PlayerInput>(true) : null;
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

    private void ApplyPlayerPositionAfterCutscene()
    {
        if (!forcePlayerPositionAfterCutscene || _playerRoot == null)
        {
            return;
        }

        _playerRoot.transform.position = playerPositionAfterCutscene;

        if (_playerBody != null)
        {
            _playerBody.position = (Vector2)playerPositionAfterCutscene;
            _playerBody.linearVelocity = Vector2.zero;
            _playerBody.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();
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

        return other.GetComponentInParent<PlayerMovement>() != null || other.GetComponentInParent<PlayerHealth>() != null;
    }

    private bool TryResolveAlivePlayer(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return false;
        }

        var health = other.GetComponentInParent<PlayerHealth>();
        return health == null || !health.IsDead;
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

    private void AbortCutsceneFlow()
    {
        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        if (_cameraRebindRoutine != null)
        {
            StopCoroutine(_cameraRebindRoutine);
            _cameraRebindRoutine = null;
        }

        if (_mainCameraRefreshRoutine != null)
        {
            StopCoroutine(_mainCameraRefreshRoutine);
            _mainCameraRefreshRoutine = null;
        }

        _forcePlayerCameraUntilUnscaledTime = 0f;
        CinemachineCore.SoloCamera = null;

        if (timelineDirector != null)
        {
            timelineDirector.stopped -= OnTimelineStopped;
            if (timelineDirector.state == PlayState.Playing)
            {
                timelineDirector.Stop();
            }

            if (restartFromBeginning)
            {
                timelineDirector.time = 0d;
                timelineDirector.Evaluate();
            }

            timelineDirector.enabled = false;
        }

        SetPlayerVisible(true);
        SetPlayerControlEnabled(true);
        _cutsceneCompletionHandled = false;
    }
}
