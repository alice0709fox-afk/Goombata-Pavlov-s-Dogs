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
    private static SceneTransitionManager _instance;
    private static float _loadingProgress;

    [SerializeField] private string loadingSceneName = "2_Loading";
    [SerializeField] private float minimumLoadingScreenSeconds = 6f;

    private string _pendingSpawnPointName;
    private string _pendingTargetSceneName;
    private bool _isLoading;
    private Coroutine _applySpawnRoutine;

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
            return;
        }

        if (_applySpawnRoutine != null)
        {
            StopCoroutine(_applySpawnRoutine);
        }

        _applySpawnRoutine = StartCoroutine(ApplyPendingSpawnWhenReady(scene, _pendingSpawnPointName));
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