using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class KokaKolaPickup : MonoBehaviour
{
    private static readonly List<KokaKolaPickup> ActivePickups = new List<KokaKolaPickup>();
    private static readonly Dictionary<int, float> CollectCooldownByPlayer = new Dictionary<int, float>();

    [SerializeField] private int amount = 1;
    [SerializeField] [Min(0.05f)] private float collectRadius = 0.7f;
    [SerializeField] [Min(0.01f)] private float collectCooldown = 0.12f;

    private bool _collected;

    private void OnEnable()
    {
        if (!ActivePickups.Contains(this))
        {
            ActivePickups.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    private void Update()
    {
        if (_collected)
        {
            return;
        }

        var players = FindObjectsByType<PlayerConsumables>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var playerConsumables = players[index];
            if (playerConsumables == null || IsOnCollectCooldown(playerConsumables))
            {
                continue;
            }

            if (!IsPlayerCloseEnough(playerConsumables.transform))
            {
                continue;
            }

            if (!IsClosestPickupFor(playerConsumables.transform))
            {
                continue;
            }

            if (!playerConsumables.TryAddKola(amount))
            {
                continue;
            }

            _collected = true;
            SetCollectCooldown(playerConsumables);
            Destroy(gameObject);
            return;
        }
    }

    private bool IsPlayerCloseEnough(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return false;
        }

        return Vector2.Distance(playerTransform.position, transform.position) <= collectRadius;
    }

    private bool IsClosestPickupFor(Transform playerTransform)
    {
        var playerPosition = playerTransform.position;
        var currentDistance = Vector2.Distance(playerPosition, transform.position);

        for (var index = 0; index < ActivePickups.Count; index++)
        {
            var pickup = ActivePickups[index];
            if (pickup == null || pickup == this || pickup._collected)
            {
                continue;
            }

            var pickupDistance = Vector2.Distance(playerPosition, pickup.transform.position);
            if (pickupDistance + 0.01f < currentDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOnCollectCooldown(PlayerConsumables playerConsumables)
    {
        var playerId = playerConsumables.GetInstanceID();
        return CollectCooldownByPlayer.TryGetValue(playerId, out var nextTime) && Time.unscaledTime < nextTime;
    }

    private void SetCollectCooldown(PlayerConsumables playerConsumables)
    {
        var playerId = playerConsumables.GetInstanceID();
        CollectCooldownByPlayer[playerId] = Time.unscaledTime + collectCooldown;
    }
}