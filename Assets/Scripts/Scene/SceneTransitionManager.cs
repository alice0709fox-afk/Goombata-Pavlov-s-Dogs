using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneTransitionManager : MonoBehaviour
{
    private const string Level2SceneName = "Level_2";

    private static SceneTransitionManager _instance;
    private static float _loadingProgress;

    [SerializeField] private string loadingSceneName = "2_Loading";
    [SerializeField] private float minimumLoadingScreenSeconds = 6f;

    private string _pendingSpawnPointName;
    private string _pendingTargetSceneName;
    private bool _isLoading;
    private Coroutine _applySpawnRoutine;
    private Coroutine _attachCameraRoutine;
    private bool _initialSceneHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceAfterSceneLoad()
    {
        _ = Instance;
    }

    public static float LoadingProgress
    {
        get => _loadingProgress;
        set => _loadingProgress = Mathf.Clamp01(value);
    }

    public string PendingTargetSceneName => _pendingTargetSceneName;
    public float MinimumLoadingScreenSeconds => minimumLoadingScreenSeconds;

    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<SceneTransitionManager>();
            if (_instance != null)
            {
                _instance.Initialize();
                return _instance;
            }

            var managerObject = new GameObject(nameof(SceneTransitionManager));
            _instance = managerObject.AddComponent<SceneTransitionManager>();
            _instance.Initialize();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Initialize();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_applySpawnRoutine != null)
            {
                StopCoroutine(_applySpawnRoutine);
                _applySpawnRoutine = null;
            }

            if (_attachCameraRoutine != null)
            {
                StopCoroutine(_attachCameraRoutine);
                _attachCameraRoutine = null;
            }

            _instance = null;
        }
    }

    public void LoadScene(string sceneName, string spawnPointName = "TargetSpawnPointName")
    {
        BeginSceneLoad(sceneName, spawnPointName, useSpawnPoint: true);
    }

    public void LoadSceneWithoutSpawn(string sceneName)
    {
        BeginSceneLoad(sceneName, null, useSpawnPoint: false);
    }

    private void BeginSceneLoad(string sceneName, string spawnPointName, bool useSpawnPoint)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneTransitionManager: Scene name is empty.", this);
            return;
        }

        if (_isLoading)
        {
            return;
        }

        _pendingSpawnPointName = useSpawnPoint
            ? (string.IsNullOrWhiteSpace(spawnPointName) ? "TargetSpawnPointName" : spawnPointName)
            : null;
        _isLoading = true;
        LoadingProgress = 0f;

        ClearEditorSelectionForActiveScene();
        PreparePersistentPlayersForTransition();

        if (!string.IsNullOrWhiteSpace(loadingSceneName) && !string.Equals(sceneName, loadingSceneName))
        {
            _pendingTargetSceneName = sceneName;
            SceneManager.LoadScene(loadingSceneName);
            return;
        }

        _pendingTargetSceneName = null;
        SceneManager.LoadScene(sceneName);
    }

    public void ClearLoadingRequest()
    {
        _pendingTargetSceneName = null;
        LoadingProgress = 0f;
    }

    public static void DestroyPersistentPlayers()
    {
        ClearEditorSelectionForActiveScene();

        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player != null)
            {
                Destroy(player.gameObject);
            }
        }
    }

    private void Initialize()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (_initialSceneHandled)
        {
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        _initialSceneHandled = true;
        OnSceneLoaded(activeScene, LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isLoading = false;

        if (!string.IsNullOrWhiteSpace(_pendingTargetSceneName))
        {
            if (!string.Equals(scene.name, _pendingTargetSceneName))
            {
                return;
            }

            _pendingTargetSceneName = null;
            LoadingProgress = 0f;
        }

        if (string.IsNullOrWhiteSpace(_pendingSpawnPointName))
        {
            if (!string.Equals(scene.name, Level2SceneName))
            {
                return;
            }

            var fallbackTarget = GetFirstPlayerTarget();
            if (fallbackTarget != null)
            {
                AttachSceneCamerasToTarget(scene, fallbackTarget);
            }
            else
            {
                if (_attachCameraRoutine != null)
                {
                    StopCoroutine(_attachCameraRoutine);
                }

                _attachCameraRoutine = StartCoroutine(AttachSceneCamerasWhenReady(scene));
            }

            return;
        }

        if (_applySpawnRoutine != null)
        {
            StopCoroutine(_applySpawnRoutine);
        }

        _applySpawnRoutine = StartCoroutine(ApplyPendingSpawnWhenReady(scene, _pendingSpawnPointName));
    }

    private IEnumerator AttachSceneCamerasWhenReady(Scene scene)
    {
        const int maxFramesToWait = 120;
        for (var frame = 0; frame <= maxFramesToWait; frame++)
        {
            var target = GetFirstPlayerTarget();
            if (target != null)
            {
                AttachSceneCamerasToTarget(scene, target);
                _attachCameraRoutine = null;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning($"SceneTransitionManager: Could not find player target for camera binding in scene '{scene.name}'.", this);
        _attachCameraRoutine = null;
    }

    private static Transform GetFirstPlayerTarget()
    {
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null)
            {
                continue;
            }

            var body = player.GetComponent<Rigidbody2D>();
            return body != null ? body.transform : player.transform;
        }

        var healthComponents = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var index = 0; index < healthComponents.Length; index++)
        {
            var health = healthComponents[index];
            if (health == null)
            {
                continue;
            }

            var body = health.GetComponent<Rigidbody2D>();
            return body != null ? body.transform : health.transform;
        }

        var movementComponents = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (var index = 0; index < movementComponents.Length; index++)
        {
            var movement = movementComponents[index];
            if (movement == null)
            {
                continue;
            }

            var body = movement.GetComponent<Rigidbody2D>();
            return body != null ? body.transform : movement.transform;
        }

        try
        {
            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                var body = taggedPlayer.GetComponent<Rigidbody2D>();
                return body != null ? body.transform : taggedPlayer.transform;
            }
        }
        catch (UnityException)
        {
            // The project may not define Player tag in every scene.
        }

        return null;
    }

    private IEnumerator ApplyPendingSpawnWhenReady(Scene scene, string spawnPointName)
    {
        var spawnPoint = FindSpawnPointTransform(scene, spawnPointName);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"SceneTransitionManager: Spawn point '{spawnPointName}' was not found in scene '{scene.name}'.", this);
            RestorePersistentPlayersAfterTransition();
            _pendingSpawnPointName = null;
            _applySpawnRoutine = null;
            yield break;
        }

        const int maxFramesToWait = 30;
        for (var frame = 0; frame <= maxFramesToWait; frame++)
        {
            var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            if (players.Length > 0)
            {
                var basePosition = spawnPoint.position;
                Transform cameraTarget = null;
                for (var index = 0; index < players.Length; index++)
                {
                    var player = players[index];
                    if (player == null)
                    {
                        continue;
                    }

                    var body = player.GetComponent<Rigidbody2D>();
                    var spawnTarget = body != null ? body.transform : player.transform;
                    spawnTarget.position = basePosition + new Vector3(index * 1.25f, 0f, 0f);

                    if (body != null)
                    {
                        body.position = spawnTarget.position;
                    }

                    PlayerSpawnHelper.RestoreAfterSceneTransition(spawnTarget);
                    PlayerSpawnHelper.StabilizeAfterSpawn(spawnTarget);

                    if (cameraTarget == null)
                    {
                        cameraTarget = spawnTarget;
                    }
                }

                AttachSceneCamerasToTarget(scene, cameraTarget);

                _pendingSpawnPointName = null;
                _applySpawnRoutine = null;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning($"SceneTransitionManager: No PlayerInput found after loading scene '{scene.name}'. Spawn was not applied.", this);
        RestorePersistentPlayersAfterTransition();
        _pendingSpawnPointName = null;
        _applySpawnRoutine = null;
    }

    private static void PreparePersistentPlayersForTransition()
    {
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null)
            {
                continue;
            }

            PlayerSpawnHelper.PrepareForSceneTransition(player.transform);
        }
    }

    private static void RestorePersistentPlayersAfterTransition()
    {
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null)
            {
                continue;
            }

            PlayerSpawnHelper.RestoreAfterSceneTransition(player.transform);
        }
    }

    private static Transform FindSpawnPointTransform(Scene scene, string spawnPointName)
    {
        var fallbackByPrefix = new List<Transform>();
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.name == spawnPointName)
                {
                    return child;
                }

                if (child.name.StartsWith(spawnPointName))
                {
                    fallbackByPrefix.Add(child);
                }
            }
        }

        return fallbackByPrefix.Count > 0 ? fallbackByPrefix[0] : null;
    }

    private static void AttachSceneCamerasToTarget(Scene scene, Transform target)
    {
        if (target == null)
        {
            return;
        }

        var virtualCameras = FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsSortMode.None);
        foreach (var virtualCamera in virtualCameras)
        {
            if (virtualCamera == null || virtualCamera.gameObject.scene != scene)
            {
                continue;
            }

            virtualCamera.Follow = target;
            if (virtualCamera.LookAt == null)
            {
                virtualCamera.LookAt = target;
            }

            if (virtualCamera is CinemachineCamera cinemachineCamera)
            {
                var cameraTarget = cinemachineCamera.Target;
                cameraTarget.TrackingTarget = target;
                if (cameraTarget.LookAtTarget == null)
                {
                    cameraTarget.LookAtTarget = target;
                }

                cinemachineCamera.Target = cameraTarget;
            }
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ClearEditorSelectionForActiveScene()
    {
#if UNITY_EDITOR
        var selectedObject = Selection.activeObject;
        if (selectedObject == null)
        {
            return;
        }

        GameObject selectedGameObject = null;
        if (selectedObject is GameObject gameObject)
        {
            selectedGameObject = gameObject;
        }
        else if (selectedObject is Component component)
        {
            selectedGameObject = component.gameObject;
        }

        if (selectedGameObject == null)
        {
            return;
        }

        if (!selectedGameObject.scene.IsValid())
        {
            return;
        }

        Selection.activeObject = null;
        Selection.objects = System.Array.Empty<Object>();
        EditorApplication.delayCall += ForceRebuildInspectorAfterSelectionClear;
#endif
    }

    private static void ForceRebuildInspectorAfterSelectionClear()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= ForceRebuildInspectorAfterSelectionClear;
        ActiveEditorTracker.sharedTracker.ForceRebuild();
#endif
    }
}