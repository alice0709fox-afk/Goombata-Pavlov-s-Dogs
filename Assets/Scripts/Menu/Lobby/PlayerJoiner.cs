using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;

public class PlayerJoiner : MonoBehaviour
{
    [SerializeField] private PlayerInput playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxPlayers = 6;
    [SerializeField] private bool disableWhenPlayerAlreadyExists = true;
    
    private readonly List<InputDevice> _devices = new List<InputDevice>();
    private readonly List<PlayerInput> _players = new List<PlayerInput>();
    
    private int _playerCount;
    private bool _isLocked;
    
    private IDisposable _anyButtonSubscription;
 
    private void OnEnable()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerJoiner: Player prefab is not assigned.", this);
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("PlayerJoiner: Spawn points are not assigned.", this);
            return;
        }

        if (disableWhenPlayerAlreadyExists)
        {
            var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            if (existingPlayers.Length > 0)
            {
                Debug.Log("PlayerJoiner: Existing player detected, joiner will not spawn another player.", this);
                return;
            }
        }

        _anyButtonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
    }

    private void OnDisable()
    {
        _anyButtonSubscription?.Dispose();
    }

    private void OnAnyButtonPressed(InputControl control)
    {
        if (control == null || control.device == null) return;

        if(_isLocked) return;

        var device = control.device;
        
        if(_devices.Contains(device)) return;
        if(_playerCount >= maxPlayers) return;
        
        var hasScheme = TryGetControlScheme(device, out var controlScheme);
        var schemeName = hasScheme ? controlScheme.bindingGroup : string.Empty;

        var alreadyHasPlayerForScheme = hasScheme && _players.Any(p => p.defaultControlScheme == schemeName);

        if (alreadyHasPlayerForScheme)
        {
            var existingPlayer = _players.First(p => p.defaultControlScheme == schemeName);
            if (!_devices.Contains(device))
            {
                InputUser.PerformPairingWithDevice(device, existingPlayer.user);
                _devices.Add(device);
            }
            return;
        }
        
        SpawnPlayer(device, schemeName, hasScheme);
    }

    private void SpawnPlayer(InputDevice device, string schemeName, bool hasScheme)
    {
        if (_playerCount >= spawnPoints.Length)
        {
            Debug.LogError("PlayerJoiner: Not enough spawn points for requested player count.", this);
            return;
        }

        var spawnPoint = spawnPoints[_playerCount];
        if (spawnPoint == null)
        {
            Debug.LogError($"PlayerJoiner: Spawn point at index {_playerCount} is null.", this);
            return;
        }
        
        var player = Instantiate(playerPrefab, spawnPoint.position, Quaternion.identity);
        var playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerJoiner: Spawned player is missing PlayerInput component.", this);
            return;
        }

        DontDestroyOnLoad(player.gameObject);
        PlayerSpawnHelper.StabilizeAfterSpawn(player.transform);
        
        if (hasScheme)
        {
            InputUser.PerformPairingWithDevice(device, playerInput.user);
        }
        else
        {
            Debug.LogWarning("PlayerJoiner: Control scheme not found for device, player spawned without explicit pairing.", this);
        }
        
        _devices.Add(device);
        _players.Add(playerInput);
        _playerCount++;
        
        if (hasScheme && !string.IsNullOrEmpty(schemeName))
        {
            playerInput.defaultControlScheme = schemeName;
        }
        playerInput.ActivateInput();
    }

    private bool TryGetControlScheme(InputDevice device, out InputControlScheme controlScheme)
    {
        if (playerPrefab == null || playerPrefab.actions == null)
        {
            controlScheme = default;
            return false;
        }

        foreach (var s in playerPrefab.actions.controlSchemes)
        {
            if (s.SupportsDevice(device))
            {
                controlScheme = s;
                return true; 
            }
        }
        
        controlScheme = default;
        return false;
    }
    
    public void SetLock(bool isLocked) => _isLocked = isLocked;
}
