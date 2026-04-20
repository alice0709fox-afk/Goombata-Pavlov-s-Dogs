using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MoneyPickup : MonoBehaviour
{
    private static readonly List<MoneyPickup> ActivePickups = new List<MoneyPickup>();
    private static readonly Dictionary<int, float> CollectCooldownByPlayer = new Dictionary<int, float>();

    [SerializeField] private int amount = 1;
    [SerializeField] [Min(0.05f)] private float collectRadius = 0.6f;
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

        var players = FindObjectsByType<PlayerSouls>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var playerSouls = players[index];
            if (playerSouls == null || IsOnCollectCooldown(playerSouls))
            {
                continue;
            }

            if (!IsPlayerCloseEnough(playerSouls.transform))
            {
                continue;
            }

            if (!IsClosestPickupFor(playerSouls.transform))
            {
                continue;
            }

            _collected = true;
            AudioController.Instance?.PlayMoneyPickup();
            playerSouls.AddSouls(amount);
            SetCollectCooldown(playerSouls);
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

    private bool IsOnCollectCooldown(PlayerSouls playerSouls)
    {
        var playerId = playerSouls.GetInstanceID();
        return CollectCooldownByPlayer.TryGetValue(playerId, out var nextTime) && Time.unscaledTime < nextTime;
    }

    private void SetCollectCooldown(PlayerSouls playerSouls)
    {
        var playerId = playerSouls.GetInstanceID();
        CollectCooldownByPlayer[playerId] = Time.unscaledTime + collectCooldown;
    }
}