using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class RisingDeathZone2D : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField, Min(0f)] private float startupGraceTime = 0.75f;
    [SerializeField] private bool requireActivationByProximity = false;
    [SerializeField] private Vector2 activationCenterOffset = Vector2.zero;
    [SerializeField, Min(0.1f)] private float activationRadius = 8f;

    [Header("Tracking")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float distanceBelowHighestPlayer = 5f;
    [SerializeField, Min(0f)] private float riseSpeed = 15f;
    [SerializeField] private bool initializeFromCurrentPlayers = true;

    [Header("Camera Fail Check")]
    [SerializeField] private bool enableCameraFailCheck = true;
    [SerializeField] private Camera referenceCamera;
    [SerializeField, Min(0f)] private float cameraBottomMargin = 0.35f;
    [SerializeField, Min(0f)] private float extraDistanceBelowCameraToKill = 1.5f;
    [SerializeField, Min(0f)] private float cameraBelowKillGraceTime = 0.35f;

    [Header("Kill")]
    [SerializeField, Min(0f)] private float killDelay = 0.1f;
    [SerializeField] private bool disableControlDuringDelay = true;
    [SerializeField] private bool stopVelocityDuringDelay = true;

    [Header("Restart")]
    [SerializeField] private bool restartLevelOnDeath = false;
    [SerializeField, Min(0f)] private float restartDelay = 0.4f;

    private readonly HashSet<int> _pendingKills = new HashSet<int>();
    private readonly Dictionary<int, float> _cameraBelowSince = new Dictionary<int, float>();
    private float _currentZoneY;
    private bool _activationSatisfied;
    private bool _zoneArmed;
    private float _startedAtTime;

    private void Awake()
    {
        var trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void Start()
    {
        _startedAtTime = Time.time;
        _currentZoneY = transform.position.y;
        _activationSatisfied = !requireActivationByProximity;
    }

    private void Update()
    {
        if (!_activationSatisfied)
        {
            _activationSatisfied = IsAnyAlivePlayerNearActivationZone();
            if (!_activationSatisfied)
                return;

            // Grace отсчитывается с момента активации, а не со старта сцены.
            _startedAtTime = Time.time;
            _cameraBelowSince.Clear();
        }

        var highestY = GetHighestAlivePlayerY(out var hasPlayer);

        if (!_zoneArmed && hasPlayer)
        {
            if (initializeFromCurrentPlayers)
            {
                _currentZoneY = highestY - distanceBelowHighestPlayer;
                SetZoneY(_currentZoneY);
            }

            _zoneArmed = true;
            _cameraBelowSince.Clear();
        }

        if (!_zoneArmed)
            return;

        UpdateZoneHeight(highestY, hasPlayer);

        if (Time.time - _startedAtTime < startupGraceTime)
            return;

        CheckPlayersBelowZoneOrCamera();
    }

    private void UpdateZoneHeight(float highestY, bool hasPlayer)
    {
        if (!hasPlayer)
            return;

        var desiredY = highestY - distanceBelowHighestPlayer;
        if (desiredY <= _currentZoneY)
            return;

        if (riseSpeed <= 0f)
            _currentZoneY = desiredY;
        else
            _currentZoneY = Mathf.MoveTowards(_currentZoneY, desiredY, riseSpeed * Time.deltaTime);

        SetZoneY(_currentZoneY);
    }

    private void CheckPlayersBelowZoneOrCamera()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
            return;

        var cameraBottomY = GetCameraBottomY(out var hasCamera);
        var cameraKillY = cameraBottomY - extraDistanceBelowCameraToKill;

        for (int i = 0; i < players.Length; i++)
        {
            var health = players[i];
            if (health == null || health.IsDead)
                continue;

            var playerId = health.GetInstanceID();
            var playerY = health.transform.position.y;

            if (playerY < _currentZoneY)
            {
                QueueKill(health);
                _cameraBelowSince.Remove(playerId);
                continue;
            }

            if (!enableCameraFailCheck || !hasCamera)
            {
                _cameraBelowSince.Remove(playerId);
                continue;
            }

            if (playerY < cameraKillY)
            {
                if (!_cameraBelowSince.TryGetValue(playerId, out var started))
                {
                    _cameraBelowSince[playerId] = Time.time;
                }
                else if (Time.time - started >= cameraBelowKillGraceTime)
                {
                    QueueKill(health);
                }

                continue;
            }

            _cameraBelowSince.Remove(playerId);
        }
    }

    private float GetHighestAlivePlayerY(out bool hasPlayer)
    {
        hasPlayer = false;
        var highestY = float.MinValue;

        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var health = players[i];
            if (health == null || health.IsDead)
                continue;

            hasPlayer = true;
            var y = health.transform.position.y;
            if (y > highestY)
                highestY = y;
        }

        return highestY;
    }

    private float GetCameraBottomY(out bool hasCamera)
    {
        var cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            hasCamera = false;
            return float.MinValue;
        }

        hasCamera = true;
        return cam.transform.position.y - cam.orthographicSize - cameraBottomMargin;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_activationSatisfied || !_zoneArmed)
            return;

        if (Time.time - _startedAtTime < startupGraceTime)
            return;

        if (!IsPlayer(other))
            return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead)
            return;

        QueueKill(health);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
            return true;

        return other.GetComponentInParent<PlayerMovement>() != null;
    }

    private void QueueKill(PlayerHealth health)
    {
        var playerId = health.GetInstanceID();
        if (_pendingKills.Contains(playerId))
            return;

        StartCoroutine(KillWithDelayRoutine(health, playerId));
    }

    private IEnumerator KillWithDelayRoutine(PlayerHealth health, int playerId)
    {
        _pendingKills.Add(playerId);

        if (disableControlDuringDelay && health != null)
        {
            var movement = health.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = false;

            var attackHandler = health.GetComponent<PlayerAttackHandler>();
            if (attackHandler != null)
                attackHandler.enabled = false;

            var inputHandler = health.GetComponent<PlayerInputHandler>();
            if (inputHandler != null)
                inputHandler.enabled = false;
        }

        if (stopVelocityDuringDelay && health != null)
        {
            var rb = health.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        if (killDelay > 0f)
            yield return new WaitForSeconds(killDelay);

        if (health != null && !health.IsDead)
            health.KillInstantly();

        _pendingKills.Remove(playerId);

        if (!restartLevelOnDeath)
            yield break;

        // Optional legacy behavior: auto-restart scene after death.
        if (restartDelay > 0f)
            yield return new WaitForSeconds(restartDelay);
    }

    private void SetZoneY(float y)
    {
        var position = transform.position;
        position.y = y;
        transform.position = position;
    }

    private bool IsAnyAlivePlayerNearActivationZone()
    {
        var center = (Vector2)transform.position + activationCenterOffset;
        var radiusSqr = activationRadius * activationRadius;

        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var i = 0; i < players.Length; i++)
        {
            var health = players[i];
            if (health == null || health.IsDead)
                continue;

            var delta = (Vector2)health.transform.position - center;
            if (delta.sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!requireActivationByProximity)
            return;

        var center = (Vector2)transform.position + activationCenterOffset;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.65f);
        Gizmos.DrawWireSphere(center, activationRadius);
    }
}
