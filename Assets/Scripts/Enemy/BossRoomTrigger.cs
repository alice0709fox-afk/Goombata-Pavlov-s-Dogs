using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Прикрепить на GameObject с Collider2D (Is Trigger = true).
/// Когда игрок входит в триггер — запускается кат-сцена Timeline.
/// После окончания кат-сцены включается Boss AI и границы арены.
/// </summary>
[DisallowMultipleComponent]
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector cutsceneDirector;
    [SerializeField] private bool keepFinalStateAfterCutscene = true;

    [Header("Boss")]
    [SerializeField] private BossController bossController;

    [Header("Arena Boundaries")]
    [SerializeField] private ArenaBoundary arenaBoundary;

    [Header("Camera")]
    [Tooltip("Отключить основную камеру игрока во время кат-сцены")]
    [SerializeField] private GameObject playerCamera;
    [SerializeField] private GameObject playerVirtualCamera;
    [SerializeField] private string fallbackPlayerVirtualCameraName = "CinemachineCameraPlayer";
    [SerializeField, Min(1)] private int playerCameraPriorityWhileIdle = 100;

    [Header("Fallback Activation")]
    [SerializeField] private bool enableProximityFallback = true;
    [SerializeField, Min(0.1f)] private float proximityActivationRadius = 2.5f;
    [SerializeField] private bool enableLineActivationFallback = true;
    [SerializeField, Min(0f)] private float lineActivationTolerance = 0.25f;
    [SerializeField, Min(0f)] private float lineActivationVerticalRange = 12f;

    [Header("Activation Window")]
    [SerializeField] private bool useActivationWindow = true;
    [SerializeField, Min(0.1f)] private float activationHalfWidth = 1.25f;
    [SerializeField, Min(0.1f)] private float activationHalfHeight = 2f;

    private bool _triggered;
    private bool _bossFightStarted;
    private bool _cutsceneCompletionHandled;
    private bool _isPreparingCutscene;
    private Coroutine _cutsceneWatchdogRoutine;
    private CinemachineVirtualCameraBase _playerVirtualCamera;
    private Collider2D _triggerCollider;
    private readonly Collider2D[] _overlapResults = new Collider2D[16];
    private readonly Dictionary<int, float> _lastKnownPlayerX = new Dictionary<int, float>();

    private void Start()
    {
        _triggerCollider = GetComponent<Collider2D>();
        if (_triggerCollider != null)
            _triggerCollider.isTrigger = true;

        ResolvePlayerCameraReferences();

        if (bossController != null)
            bossController.enabled = false;

        if (cutsceneDirector != null)
        {
            cutsceneDirector.playOnAwake = false;
            cutsceneDirector.Stop();
            cutsceneDirector.stopped -= OnCutsceneStopped;
            cutsceneDirector.time = 0d;
            cutsceneDirector.Evaluate();
        }

        ForcePlayerCameraView();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolveAlivePlayer(other, out _))
            return;

        if (!IsWithinActivationWindow(other.transform.position))
            return;

        TryActivateBossTrigger();
    }

    private void Update()
    {
        if (!_triggered)
        {
            ForcePlayerCameraView();
            BindPlayerCameraToLivePlayer();

            TryActivateFromTriggerOverlaps();

            if (IsAnyAlivePlayerNearTrigger())
            {
                TryActivateBossTrigger();
            }

            if (enableLineActivationFallback && IsAnyAlivePlayerCrossedActivationLine())
            {
                TryActivateBossTrigger();
            }

            return;
        }

        if (!_triggered || _bossFightStarted)
            return;
    }

    private void TryActivateBossTrigger()
    {
        if (_triggered)
            return;

        _triggered = true;
        _cutsceneCompletionHandled = false;

        var triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (cutsceneDirector != null)
        {
            _isPreparingCutscene = true;

            // Release any forced player camera so Timeline can drive cameras normally.
            CinemachineCore.SoloCamera = null;

            if (playerVirtualCamera != null)
            {
                var playerVcamBase = playerVirtualCamera.GetComponent<CinemachineVirtualCameraBase>();
                if (playerVcamBase != null)
                    playerVcamBase.Priority = 0;
            }

            if (playerCamera != null)
                playerCamera.SetActive(true);

            // Rebuild timeline graph to avoid stale state after multi-scene campaign flow.
            cutsceneDirector.gameObject.SetActive(true);
            cutsceneDirector.enabled = true;
            cutsceneDirector.extrapolationMode = keepFinalStateAfterCutscene
                ? DirectorWrapMode.Hold
                : DirectorWrapMode.None;
            cutsceneDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            cutsceneDirector.stopped -= OnCutsceneStopped;
            cutsceneDirector.RebuildGraph();
            cutsceneDirector.Stop();
            cutsceneDirector.time = 0d;
            cutsceneDirector.Evaluate();
            cutsceneDirector.stopped += OnCutsceneStopped;
            _isPreparingCutscene = false;
            cutsceneDirector.Play();

            if (_cutsceneWatchdogRoutine != null)
                StopCoroutine(_cutsceneWatchdogRoutine);

            _cutsceneWatchdogRoutine = StartCoroutine(CutsceneWatchdogRoutine());
            return;
        }

        StartBossFight();
    }

    private bool IsAnyAlivePlayerNearTrigger()
    {
        if (!enableProximityFallback)
            return false;

        var radiusSqr = proximityActivationRadius * proximityActivationRadius;
        var triggerPosition = transform.position;
        var triggerCollider = _triggerCollider != null ? _triggerCollider : GetComponent<Collider2D>();

        var players = GetCandidatePlayerTargets();
        for (var i = 0; i < players.Count; i++)
        {
            var playerTarget = players[i];
            if (playerTarget == null)
                continue;

            var playerPos = playerTarget.position;

            if (!IsWithinActivationWindow(playerPos))
                continue;

            // If player is already intersecting (or almost intersecting) trigger bounds, activate immediately.
            if (triggerCollider != null)
            {
                var sqrDistanceToBounds = triggerCollider.bounds.SqrDistance(playerPos);
                if (sqrDistanceToBounds <= 0.04f)
                    return true;
            }

            var delta = playerPos - triggerPosition;
            if (delta.sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    private void TryActivateFromTriggerOverlaps()
    {
        if (_triggered)
            return;

        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider2D>();

        if (_triggerCollider == null || !_triggerCollider.enabled)
            return;

        var hitCount = _triggerCollider.Overlap(ContactFilter2D.noFilter, _overlapResults);
        for (var i = 0; i < hitCount; i++)
        {
            var candidate = _overlapResults[i];
            if (!TryResolveAlivePlayer(candidate, out _))
                continue;

            if (!IsWithinActivationWindow(candidate.transform.position))
                continue;

            TryActivateBossTrigger();
            return;
        }
    }

    private bool IsAnyAlivePlayerCrossedActivationLine()
    {
        var players = GetCandidatePlayerTargets();
        if (players.Count == 0)
            return false;

        var triggerPos = transform.position;

        for (var i = 0; i < players.Count; i++)
        {
            var playerTarget = players[i];
            if (playerTarget == null)
                continue;

            var playerId = playerTarget.GetInstanceID();
            var pos = playerTarget.position;

            if (!IsWithinActivationWindow(pos))
                continue;

            if (Mathf.Abs(pos.y - triggerPos.y) > lineActivationVerticalRange)
                continue;

            if (!_lastKnownPlayerX.TryGetValue(playerId, out var previousX))
            {
                _lastKnownPlayerX[playerId] = pos.x;

                // If player is already exactly on the line window, activate immediately.
                if (Mathf.Abs(pos.x - triggerPos.x) <= lineActivationTolerance)
                    return true;

                continue;
            }

            var crossedLeftToRight = previousX < triggerPos.x - lineActivationTolerance
                                     && pos.x >= triggerPos.x - lineActivationTolerance;

            var crossedRightToLeft = previousX > triggerPos.x + lineActivationTolerance
                                     && pos.x <= triggerPos.x + lineActivationTolerance;

            if (crossedLeftToRight || crossedRightToLeft)
                return true;

            _lastKnownPlayerX[playerId] = pos.x;
        }

        return false;
    }

    private bool IsWithinActivationWindow(Vector3 playerPosition)
    {
        if (!useActivationWindow)
            return true;

        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider2D>();

        if (_triggerCollider != null)
        {
            var bounds = _triggerCollider.bounds;
            bounds.Expand(new Vector3(activationHalfWidth * 2f, activationHalfHeight * 2f, 0f));
            if (bounds.Contains(playerPosition))
                return true;

            // Small tolerance for frame-perfect edge cases on fast movement.
            if (_triggerCollider.bounds.SqrDistance(playerPosition) <= 0.04f)
                return true;

            return false;
        }

        var triggerPos = transform.position;
        if (Mathf.Abs(playerPosition.x - triggerPos.x) > activationHalfWidth)
            return false;

        if (Mathf.Abs(playerPosition.y - triggerPos.y) > activationHalfHeight)
            return false;

        return true;
    }

    private static List<Transform> GetCandidatePlayerTargets()
    {
        var result = new List<Transform>(8);
        var seenIds = new HashSet<int>();

        var playerInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var i = 0; i < playerInputs.Length; i++)
        {
            var input = playerInputs[i];
            if (input == null)
                continue;

            var body = input.GetComponentInChildren<Rigidbody2D>(true);
            var target = body != null ? body.transform : input.transform;
            if (target == null)
                continue;

            var id = target.GetInstanceID();
            if (seenIds.Add(id))
                result.Add(target);
        }

        var playerMovements = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (var i = 0; i < playerMovements.Length; i++)
        {
            var movement = playerMovements[i];
            if (movement == null)
                continue;

            var body = movement.GetComponentInChildren<Rigidbody2D>(true);
            var target = body != null ? body.transform : movement.transform;
            if (target == null)
                continue;

            var id = target.GetInstanceID();
            if (seenIds.Add(id))
                result.Add(target);
        }

        var healthComponents = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var i = 0; i < healthComponents.Length; i++)
        {
            var health = healthComponents[i];
            if (health == null || health.IsDead)
                continue;

            var body = health.GetComponentInChildren<Rigidbody2D>(true);
            var target = body != null ? body.transform : health.transform;
            if (target == null)
                continue;

            var id = target.GetInstanceID();
            if (seenIds.Add(id))
                result.Add(target);
        }

        return result;
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        if (_isPreparingCutscene)
            return;

        if (!_triggered)
            return;

        CompleteCutscene(director);
    }

    private System.Collections.IEnumerator CutsceneWatchdogRoutine()
    {
        if (cutsceneDirector == null)
            yield break;

        var timeout = Mathf.Max((float)cutsceneDirector.duration + 1f, 5f);
        var elapsed = 0f;

        while (!_cutsceneCompletionHandled && elapsed < timeout)
        {
            if (cutsceneDirector.state != PlayState.Playing && cutsceneDirector.time > 0d)
            {
                CompleteCutscene(cutsceneDirector);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_cutsceneCompletionHandled)
        {
            CompleteCutscene(cutsceneDirector);
        }
    }

    private void CompleteCutscene(PlayableDirector director)
    {
        if (_cutsceneCompletionHandled || director == null)
            return;

        _cutsceneCompletionHandled = true;

        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        director.stopped -= OnCutsceneStopped;

        if (director.state == PlayState.Playing)
            director.Stop();

        if (keepFinalStateAfterCutscene)
        {
            director.time = director.duration;
            director.Evaluate();
        }

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;

        AudioListener.pause = false;

        StartBossFight();
    }

    private void StartBossFight()
    {
        if (_bossFightStarted || !_triggered)
            return;

        _bossFightStarted = true;

        if (cutsceneDirector != null)
            cutsceneDirector.stopped -= OnCutsceneStopped;

        if (bossController != null)
            bossController.ActivateAfterCutscene();

        if (arenaBoundary != null)
            arenaBoundary.ActivateWithEntranceX(transform.position.x);
    }

    private void OnDisable()
    {
        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        if (cutsceneDirector != null)
            cutsceneDirector.stopped -= OnCutsceneStopped;
    }

    private bool TryResolveAlivePlayer(Collider2D other, out PlayerHealth health)
    {
        health = null;
        if (other == null)
            return false;

        health = ResolveHealthFromCollider(other);
        if (health != null)
            return !health.IsDead;

        // Some player hitboxes may not have PlayerHealth in the same parent chain.
        // Allow known player-owned colliders so room trigger still works.
        if (other.GetComponentInParent<PlayerMovement>() != null)
            return true;

        if (other.GetComponentInParent<PlayerInput>() != null)
            return true;

        return other.CompareTag("Player");
    }

    private static PlayerHealth ResolveHealthFromCollider(Collider2D other)
    {
        if (other == null)
            return null;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
            return health;

        var movement = other.GetComponentInParent<PlayerMovement>();
        if (movement != null)
        {
            health = movement.GetComponentInParent<PlayerHealth>();
            if (health != null)
                return health;
        }

        var input = other.GetComponentInParent<PlayerInput>();
        if (input != null)
            return input.GetComponentInParent<PlayerHealth>();

        return null;
    }

    private void AbortCutsceneAndResetTrigger()
    {
        if (_cutsceneWatchdogRoutine != null)
        {
            StopCoroutine(_cutsceneWatchdogRoutine);
            _cutsceneWatchdogRoutine = null;
        }

        if (cutsceneDirector != null)
        {
            cutsceneDirector.stopped -= OnCutsceneStopped;
            if (cutsceneDirector.state == PlayState.Playing)
                cutsceneDirector.Stop();

            cutsceneDirector.time = 0d;
            cutsceneDirector.Evaluate();
        }

        _triggered = false;
        _cutsceneCompletionHandled = false;

        var trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.enabled = true;

        ForcePlayerCameraView();
    }

    private void ResolvePlayerCameraReference()
    {
        if (playerVirtualCamera == null && !string.IsNullOrWhiteSpace(fallbackPlayerVirtualCameraName))
        {
            var candidate = GameObject.Find(fallbackPlayerVirtualCameraName);
            if (candidate != null)
                playerVirtualCamera = candidate;
        }

        if (playerVirtualCamera == null)
            return;

        _playerVirtualCamera = playerVirtualCamera.GetComponent<CinemachineVirtualCameraBase>();
    }

    private void ForcePlayerCameraView()
    {
        if (playerCamera != null && !playerCamera.activeSelf)
            playerCamera.SetActive(true);

        if (playerVirtualCamera != null && !playerVirtualCamera.activeSelf)
            playerVirtualCamera.SetActive(true);

        if (playerVirtualCamera == null)
            ResolvePlayerCameraReferences();

        if (_playerVirtualCamera == null)
            return;

        _playerVirtualCamera.Priority = Mathf.Max(_playerVirtualCamera.Priority, playerCameraPriorityWhileIdle);
        CinemachineCore.SoloCamera = _playerVirtualCamera;
    }

    private void ResolvePlayerCameraReferences()
    {
        ResolvePlayerCameraReference();
    }

    private void BindPlayerCameraToLivePlayer()
    {
        if (playerVirtualCamera == null)
            ResolvePlayerCameraReferences();

        if (_playerVirtualCamera == null)
            return;

        var target = FindLivePlayerTarget();
        if (target == null)
            return;

        var cinemachineCamera = playerVirtualCamera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            var cameraTarget = cinemachineCamera.Target;
            cameraTarget.TrackingTarget = target;
            if (cameraTarget.LookAtTarget == null)
                cameraTarget.LookAtTarget = target;

            cinemachineCamera.Target = cameraTarget;
        }

        if (_playerVirtualCamera != null)
        {
            _playerVirtualCamera.Follow = target;
            if (_playerVirtualCamera.LookAt == null)
                _playerVirtualCamera.LookAt = target;
        }
    }

    private static Transform FindLivePlayerTarget()
    {
        var playerInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var i = 0; i < playerInputs.Length; i++)
        {
            var input = playerInputs[i];
            if (input == null)
                continue;

            var health = input.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsDead)
                continue;

            var body = input.GetComponentInChildren<Rigidbody2D>(true);
            return body != null ? body.transform : input.transform;
        }

        var healthComponents = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var i = 0; i < healthComponents.Length; i++)
        {
            var health = healthComponents[i];
            if (health == null || health.IsDead)
                continue;

            var body = health.GetComponentInChildren<Rigidbody2D>(true);
            return body != null ? body.transform : health.transform;
        }

        return null;
    }

}
