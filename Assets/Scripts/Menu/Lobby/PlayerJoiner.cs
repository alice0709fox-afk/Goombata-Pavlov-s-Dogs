using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Utilities;

public class PlayerJoiner : MonoBehaviour
{
    private sealed class RespawnRequest
    {
        public string ControlSchemeName;
        public readonly List<InputDevice> Devices = new List<InputDevice>();
    }

    private static readonly List<RespawnRequest> PendingRespawnRequests = new List<RespawnRequest>();

    [SerializeField] private PlayerInput playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxPlayers = 6;
    [SerializeField] private bool disableWhenPlayerAlreadyExists = true;
    
    private readonly List<InputDevice> _devices = new List<InputDevice>();
    private readonly List<PlayerInput> _players = new List<PlayerInput>();
    
    private int _playerCount;
    private bool _isLocked;
    
    private IDisposable _anyButtonSubscription;

    public static void CaptureCurrentPlayersForRespawn()
    {
        PendingRespawnRequests.Clear();

        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            var player = players[playerIndex];
            if (player == null)
            {
                continue;
            }

            var request = new RespawnRequest
            {
                ControlSchemeName = !string.IsNullOrWhiteSpace(player.currentControlScheme)
                    ? player.currentControlScheme
                    : player.defaultControlScheme
            };

            for (var deviceIndex = 0; deviceIndex < player.user.pairedDevices.Count; deviceIndex++)
            {
                var device = player.user.pairedDevices[deviceIndex];
                if (device != null && !request.Devices.Contains(device))
                {
                    request.Devices.Add(device);
                }
            }

            if (request.Devices.Count == 0)
            {
                var devices = player.devices;
                for (var deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
                {
                    var device = devices[deviceIndex];
                    if (device != null && !request.Devices.Contains(device))
                    {
                        request.Devices.Add(device);
                    }
                }
            }

            PendingRespawnRequests.Add(request);
        }
    }

    public static void ClearPendingRespawn()
    {
        PendingRespawnRequests.Clear();
    }
 
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

        RegisterExistingPlayers();

        var spawnedPendingPlayers = false;
        if (_players.Count == 0)
        {
            spawnedPendingPlayers = TrySpawnPendingPlayers();
            if (!spawnedPendingPlayers)
            {
                TrySpawnPreferredPlayer();
            }
        }
        else if (disableWhenPlayerAlreadyExists)
        {
            Debug.Log("PlayerJoiner: Existing player detected, joiner will reuse it instead of spawning another player.", this);
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

        if (_players.Count > 0)
        {
            SwitchExistingPlayerToDevice(device);
            return;
        }
        
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
        
        SpawnPlayer(new[] { device }, schemeName, hasScheme);
    }

    private void RegisterExistingPlayers()
    {
        _players.Clear();
        _devices.Clear();

        var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var playerIndex = 0; playerIndex < existingPlayers.Length; playerIndex++)
        {
            var player = existingPlayers[playerIndex];
            if (player == null)
            {
                continue;
            }

            _players.Add(player);

            var devices = player.devices;
            for (var deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
            {
                var device = devices[deviceIndex];
                if (device != null && !_devices.Contains(device))
                {
                    _devices.Add(device);
                }
            }
        }

        _playerCount = _players.Count;
    }

    private void SwitchExistingPlayerToDevice(InputDevice device)
    {
        if (device == null)
        {
            return;
        }

        var player = _players.FirstOrDefault(playerInput => playerInput != null);
        if (player == null)
        {
            RegisterExistingPlayers();
            player = _players.FirstOrDefault(playerInput => playerInput != null);
            if (player == null)
            {
                return;
            }
        }

        if (!TryGetControlScheme(device, out var controlScheme))
        {
            return;
        }

        var schemeName = controlScheme.bindingGroup;
        var schemeDevices = BuildDevicesForScheme(device, schemeName);
        if (schemeDevices.Count == 0)
        {
            return;
        }

        player.defaultControlScheme = schemeName;
        player.SwitchCurrentControlScheme(schemeName, schemeDevices.ToArray());
        player.ActivateInput();
        GameSettings.PreferredControlScheme = schemeName;

        for (var deviceIndex = 0; deviceIndex < schemeDevices.Count; deviceIndex++)
        {
            var pairedDevice = schemeDevices[deviceIndex];
            if (!_devices.Contains(pairedDevice))
            {
                _devices.Add(pairedDevice);
            }
        }
    }

    private static List<InputDevice> BuildDevicesForScheme(InputDevice triggerDevice, string schemeName)
    {
        var devices = new List<InputDevice>();
        if (string.Equals(schemeName, "Keyboard", StringComparison.OrdinalIgnoreCase))
        {
            if (Keyboard.current != null)
            {
                devices.Add(Keyboard.current);
            }

            if (Mouse.current != null)
            {
                devices.Add(Mouse.current);
            }

            if (devices.Count == 0 && triggerDevice != null)
            {
                devices.Add(triggerDevice);
            }

            return devices;
        }

        if (triggerDevice != null)
        {
            devices.Add(triggerDevice);
        }

        return devices;
    }

    private bool TrySpawnPendingPlayers()
    {
        if (PendingRespawnRequests.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < PendingRespawnRequests.Count; index++)
        {
            var request = PendingRespawnRequests[index];
            if (request == null || request.Devices.Count == 0)
            {
                continue;
            }

            SpawnPlayer(request.Devices, request.ControlSchemeName, !string.IsNullOrWhiteSpace(request.ControlSchemeName));
        }

        PendingRespawnRequests.Clear();
        return true;
    }

    private void TrySpawnPreferredPlayer()
    {
        if (_playerCount > 0)
        {
            return;
        }

        var preferredScheme = GameSettings.PreferredControlScheme;
        if (string.Equals(preferredScheme, "Gamepad", StringComparison.OrdinalIgnoreCase))
        {
            if (GamepadUiNavigation.TryGetActiveGamepad(out var gamepad))
            {
                SpawnPlayer(new InputDevice[] { gamepad }, "Gamepad", hasScheme: true);
                return;
            }
        }

        var keyboardDevices = new List<InputDevice>();
        if (Keyboard.current != null)
        {
            keyboardDevices.Add(Keyboard.current);
        }

        if (Mouse.current != null)
        {
            keyboardDevices.Add(Mouse.current);
        }

        if (keyboardDevices.Count > 0)
        {
            SpawnPlayer(keyboardDevices, "Keyboard", hasScheme: true);
            return;
        }

        if (GamepadUiNavigation.TryGetActiveGamepad(out var fallbackGamepad))
        {
            SpawnPlayer(new InputDevice[] { fallbackGamepad }, "Gamepad", hasScheme: true);
        }
    }

    private void SpawnPlayer(IReadOnlyList<InputDevice> devices, string schemeName, bool hasScheme)
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

        if (devices != null)
        {
            for (var deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
            {
                var device = devices[deviceIndex];
                if (device == null)
                {
                    continue;
                }

                InputUser.PerformPairingWithDevice(device, playerInput.user);

                if (!_devices.Contains(device))
                {
                    _devices.Add(device);
                }
            }
        }

        if ((devices == null || devices.Count == 0) && !hasScheme)
        {
            Debug.LogWarning("PlayerJoiner: Respawned player has no paired devices.", this);
        }

        _players.Add(playerInput);
        _playerCount++;
        
        if (hasScheme && !string.IsNullOrEmpty(schemeName))
        {
            playerInput.defaultControlScheme = schemeName;

            var pairedDevices = new List<InputDevice>();
            if (devices != null)
            {
                for (var deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
                {
                    var device = devices[deviceIndex];
                    if (device != null)
                    {
                        pairedDevices.Add(device);
                    }
                }
            }

            if (pairedDevices.Count > 0)
            {
                playerInput.SwitchCurrentControlScheme(schemeName, pairedDevices.ToArray());
            }

            GameSettings.PreferredControlScheme = schemeName;
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
