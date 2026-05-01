using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class DeathScreenManager : MonoBehaviour
{
    private static DeathScreenManager _instance;
    private const string DeadObjectName = "Dead";
    private const string StartOverButtonName = "StartOver";
    private const string ExitGameButtonName = "ExitGame";
    private const float RegistrationInterval = 0.5f;

    [SerializeField] private string mainMenuSceneName = "1_Menu";
    [SerializeField] private string firstGameplaySceneName = "Level_0";
    [SerializeField] private string loadingSceneName = "2_Loading";

    private readonly HashSet<PlayerHealth> _registeredPlayers = new HashSet<PlayerHealth>();

    private GameObject _scenePanelRoot;
    private Canvas _sceneCanvas;
    private RectTransform _sceneCanvasRect;
    private Button _startOverButton;
    private Button _exitGameButton;
    private string _lastGameplaySceneName;
    private string _restartSceneName;
    private bool _isInitialized;
    private bool _isVisible;
    private bool _isTransitioning;
    private float _nextRegistrationTime;
    private int _selectionIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(DeathScreenManager));
        _instance = managerObject.AddComponent<DeathScreenManager>();
        _instance.Initialize();
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RegisterAvailablePlayers();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterPlayers();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (_isVisible)
        {
            var fallbackSelection = _startOverButton != null ? _startOverButton : _exitGameButton;
            GamepadUiNavigation.EnsureSelection(fallbackSelection);

            if (GamepadUiNavigation.TryGetNavigationMove(out var navigationMove))
            {
                NavigateButtons(navigationMove);
            }

            GamepadUiNavigation.HandleSubmit();
        }

        if (Time.unscaledTime < _nextRegistrationTime)
        {
            return;
        }

        _nextRegistrationTime = Time.unscaledTime + RegistrationInterval;
        RegisterAvailablePlayers();
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        DontDestroyOnLoad(gameObject);

        RefreshSceneBindings(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        _isTransitioning = false;
        RefreshSceneBindings(scene);
        CleanupDeadPlayerReferences();
        RegisterAvailablePlayers();
    }

    private void RegisterAvailablePlayers()
    {
        CleanupDeadPlayerReferences();

        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null || _registeredPlayers.Contains(player))
            {
                continue;
            }

            player.Died += OnPlayerDied;
            _registeredPlayers.Add(player);
        }
    }

    private void CleanupDeadPlayerReferences()
    {
        _registeredPlayers.RemoveWhere(player => player == null);
    }

    private void UnregisterPlayers()
    {
        foreach (var player in _registeredPlayers)
        {
            if (player != null)
            {
                player.Died -= OnPlayerDied;
            }
        }

        _registeredPlayers.Clear();
    }

    private void OnPlayerDied()
    {
        if (_isVisible || _isTransitioning)
        {
            return;
        }

        // Make sure death UI is never blocked by a stale pause/cutscene state.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        _restartSceneName = ResolveRestartSceneName();
        AudioController.Instance?.PlayGameOverExclusive();
        SetPanelVisible(true);
    }

    private void SetPanelVisible(bool visible)
    {
        if (visible && !ResolveScenePanel(SceneManager.GetActiveScene()))
        {
            Debug.LogWarning("DeathScreenManager: Scene object 'Dead' was not found. Add the Dead panel to the gameplay scene or make it a prefab and place it there.", this);
            return;
        }

        _isVisible = visible;

        if (_scenePanelRoot != null)
        {
            _scenePanelRoot.SetActive(visible);
        }

        if (_sceneCanvasRect != null && _sceneCanvasRect.localScale == Vector3.zero)
        {
            _sceneCanvasRect.localScale = Vector3.one;
        }

        if (visible)
        {
            EnsureEventSystem();
            SelectButton(_selectionIndex);
            return;
        }

        _selectionIndex = 0;
    }

    public void StartOver()
    {
        BeginTransition(ResolveRestartSceneName(), restartGameplay: true);
    }

    public void ExitGame()
    {
        BeginTransition(mainMenuSceneName, restartGameplay: false);
    }

    private void BeginTransition(string sceneName, bool restartGameplay)
    {
        if (_isTransitioning || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        _isTransitioning = true;
        ClearEditorSelectionForScenePanel();
        SetPanelVisible(false);
        UnregisterPlayers();

        if (restartGameplay)
        {
            PlayerJoiner.CaptureCurrentPlayersForRespawn();
        }
        else
        {
            PlayerJoiner.ClearPendingRespawn();
        }

        SceneTransitionManager.DestroyPersistentPlayers();

        if (restartGameplay)
        {
            SceneTransitionManager.Instance.LoadScene(sceneName);
            return;
        }

        SceneTransitionManager.Instance.LoadSceneWithoutSpawn(sceneName);
    }

    private string ResolveRestartSceneName()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (IsGameplayScene(activeScene.name))
        {
            return activeScene.name;
        }

        if (!string.IsNullOrWhiteSpace(_restartSceneName))
        {
            return _restartSceneName;
        }

        if (!string.IsNullOrWhiteSpace(_lastGameplaySceneName))
        {
            return _lastGameplaySceneName;
        }

        return firstGameplaySceneName;
    }

    private void RefreshSceneBindings(Scene scene)
    {
        if (IsGameplayScene(scene.name))
        {
            _lastGameplaySceneName = scene.name;
        }

        ReleaseSceneBindings();
        ResolveScenePanel(scene);
        SetPanelVisible(false);
    }

    private bool IsGameplayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && sceneName == mainMenuSceneName)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(loadingSceneName) && sceneName == loadingSceneName)
        {
            return false;
        }

        if (sceneName.Contains("Intro"))
        {
            return false;
        }

        return sceneName.StartsWith("Level_", System.StringComparison.OrdinalIgnoreCase)
            || sceneName.ToLowerInvariant().Contains("boss");
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private bool ResolveScenePanel(Scene scene)
    {
        if (_scenePanelRoot != null)
        {
            return true;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        var deadRoot = FindSceneObject(scene, DeadObjectName);
        if (deadRoot == null)
        {
            return false;
        }

        _scenePanelRoot = deadRoot;
        _sceneCanvas = deadRoot.GetComponentInChildren<Canvas>(true);
        _sceneCanvasRect = _sceneCanvas != null ? _sceneCanvas.transform as RectTransform : null;

        if (_sceneCanvasRect != null && _sceneCanvasRect.localScale == Vector3.zero)
        {
            _sceneCanvasRect.localScale = Vector3.one;
        }

        _startOverButton = BindButton(deadRoot, StartOverButtonName, StartOver);
        _exitGameButton = BindButton(deadRoot, ExitGameButtonName, ExitGame);
        if (_startOverButton == null || _exitGameButton == null)
        {
            Debug.LogWarning("DeathScreenManager: The scene object 'Dead' was found, but Start Over / Exit Game buttons were not found under it. Add two UI Button components inside Dead and the manager will bind to them automatically.", deadRoot);
        }

        return true;
    }

    private void ReleaseSceneBindings()
    {
        if (_startOverButton != null)
        {
            _startOverButton.onClick.RemoveListener(StartOver);
        }

        var exitGameButton = _scenePanelRoot != null ? FindButton(_scenePanelRoot, ExitGameButtonName) : null;
        exitGameButton?.onClick.RemoveListener(ExitGame);

        _scenePanelRoot = null;
        _sceneCanvas = null;
        _sceneCanvasRect = null;
        _startOverButton = null;
        _exitGameButton = null;
    }

    private void NavigateButtons(Vector2Int move)
    {
        if (move.y == 0)
        {
            return;
        }

        var buttons = GetButtons();
        if (buttons.Length == 0)
        {
            return;
        }

        _selectionIndex = Mathf.Clamp(_selectionIndex + (move.y < 0 ? 1 : -1), 0, buttons.Length - 1);
        SelectButton(_selectionIndex);
    }

    private Button[] GetButtons()
    {
        var buttons = new List<Button>(2);
        if (_startOverButton != null)
        {
            buttons.Add(_startOverButton);
        }

        if (_exitGameButton != null)
        {
            buttons.Add(_exitGameButton);
        }

        return buttons.ToArray();
    }

    private void SelectButton(int index)
    {
        var buttons = GetButtons();
        if (buttons.Length == 0 || EventSystem.current == null)
        {
            return;
        }

        _selectionIndex = Mathf.Clamp(index, 0, buttons.Length - 1);
        EventSystem.current.SetSelectedGameObject(buttons[_selectionIndex].gameObject);
    }

    private static Button BindButton(GameObject root, string buttonName, UnityEngine.Events.UnityAction callback)
    {
        var button = FindButton(root, buttonName);
        if (button == null)
        {
            return null;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
        ConfigureButtonHover(button);
        return button;
    }

    private static Button FindButton(GameObject root, string buttonName)
    {
        if (root == null)
        {
            return null;
        }

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (var index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] != null && buttons[index].name == buttonName)
            {
                return buttons[index];
            }
        }

        return null;
    }

    private static void ConfigureButtonHover(Button button)
    {
        if (button == null)
        {
            return;
        }

        var targetText = button.GetComponentInChildren<TMP_Text>(true);
        if (targetText == null)
        {
            return;
        }

        ApplyMenuButtonColors(button);
        targetText.raycastTarget = true;

        var hover = button.GetComponent<MenuButtonHoverText>();
        if (hover == null)
        {
            hover = button.gameObject.AddComponent<MenuButtonHoverText>();
        }

        hover.Configure(
            targetText,
            MenuButtonHoverText.DefaultNormalColor,
            MenuButtonHoverText.DefaultHoverColor,
            MenuButtonHoverText.DefaultPressedColor);

        var textHover = targetText.GetComponent<MenuButtonHoverText>();
        if (textHover == null)
        {
            textHover = targetText.gameObject.AddComponent<MenuButtonHoverText>();
        }

        textHover.Configure(
            targetText,
            MenuButtonHoverText.DefaultNormalColor,
            MenuButtonHoverText.DefaultHoverColor,
            MenuButtonHoverText.DefaultPressedColor);
    }

    private static void ApplyMenuButtonColors(Button button)
    {
        var colors = button.colors;
        colors.normalColor = new Color(0.9019608f, 0.9019608f, 0.9019608f, 1f);
        colors.highlightedColor = new Color(1f, 0.81960785f, 0.4f, 1f);
        colors.pressedColor = new Color(0.95686275f, 0.63529414f, 0.38039216f, 1f);
        colors.selectedColor = new Color(1f, 0.8862745f, 0.6039216f, 1f);
        colors.disabledColor = new Color(0.47843137f, 0.47843137f, 0.47843137f, 0.5019608f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root == null)
            {
                continue;
            }

            if (root.name == objectName)
            {
                return root;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var current = transforms[transformIndex];
                if (current != null && current.name == objectName)
                {
                    return current.gameObject;
                }
            }
        }

        return null;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ClearEditorSelectionForScenePanel()
    {
#if UNITY_EDITOR
        if (_scenePanelRoot == null)
        {
            return;
        }

        var selectedObject = Selection.activeObject;
        if (selectedObject is GameObject selectedGameObject)
        {
            if (selectedGameObject == _scenePanelRoot || selectedGameObject.transform.IsChildOf(_scenePanelRoot.transform))
            {
                Selection.activeObject = null;
            }

            return;
        }

        if (selectedObject is Component selectedComponent &&
            (selectedComponent.gameObject == _scenePanelRoot || selectedComponent.transform.IsChildOf(_scenePanelRoot.transform)))
        {
            Selection.activeObject = null;
        }
#endif
    }
}