using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class DeathPitZone2D : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float killDelay = 0.35f;
    [SerializeField] private bool disableControlDuringDelay = true;
    [SerializeField] private bool stopVelocityDuringDelay = true;

    private readonly HashSet<int> _pendingKills = new HashSet<int>();

    private void Awake()
    {
        var trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead)
        {
            return;
        }

        var playerId = health.GetInstanceID();
        if (_pendingKills.Contains(playerId))
        {
            return;
        }

        StartCoroutine(KillWithDelayRoutine(health, playerId));
    }

    private IEnumerator KillWithDelayRoutine(PlayerHealth health, int playerId)
    {
        _pendingKills.Add(playerId);

        if (disableControlDuringDelay && health != null)
        {
            var movement = health.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            var attackHandler = health.GetComponent<PlayerAttackHandler>();
            if (attackHandler != null)
            {
                attackHandler.enabled = false;
            }

            var inputHandler = health.GetComponent<PlayerInputHandler>();
            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }
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
        {
            yield return new WaitForSeconds(killDelay);
        }

        if (health != null && !health.IsDead)
        {
            health.KillInstantly();
        }

        _pendingKills.Remove(playerId);
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