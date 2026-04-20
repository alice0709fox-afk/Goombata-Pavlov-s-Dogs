using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseMenuManager : MonoBehaviour
{
    private static readonly Color SelectedDifficultyColor = new Color(0.82f, 0.22f, 0.18f, 1f);
    private static PauseMenuManager _instance;

    private const string MainMenuSceneName = "1_Menu";
    private const string PauseCanvasName = "Pause";
    private const string PausePanelName = "Pause";
    private const string PauseOptionsButtonName = "Options";
    private const string PauseQuitButtonName = "Quit";
    private const string OptionsPanelName = "Panel";
    private const string OptionsPanelResourceName = "Panel";
    private const string OptionsExitButtonName = "ButtonExit";
    private const string EasyButtonName = "Easy";
    private const string HardButtonName = "Hard";
    private const string MusicSliderName = "Slider";
    private const string HealthHudName = "HpPlayer";

    private GameObject _pauseCanvasRoot;
    private GameObject _pausePanel;
    private GameObject _optionsPanel;
    private GameObject _instantiatedOptionsPanel;
    private GameObject _healthHud;
    private Button _pauseOptionsButton;
    private Button _pauseQuitButton;
    private Button _optionsExitButton;
    private Button _easyDifficultyButton;
    private Button _hardDifficultyButton;
    private Slider _musicVolumeSlider;
    private bool _isPaused;
    private int _pauseSelectionIndex;
    private int _optionsSelectionIndex;
    private float _nextRebindAttemptTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(PauseMenuManager));
        _instance = managerObject.AddComponent<PauseMenuManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Start()
    {
        RebindSceneObjects(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnbindButtons();
    }

    private void Update()
    {
        EnsureBindingsForActiveScene();

        if (_pauseCanvasRoot == null)
        {
            return;
        }

        if (_isPaused)
        {
            var fallbackSelection = _optionsPanel != null && _optionsPanel.activeSelf
                ? GetPreferredOptionsSelection()
                : (_pauseOptionsButton != null ? _pauseOptionsButton : _pauseQuitButton);

            GamepadUiNavigation.EnsureSelection(fallbackSelection);

            if (GamepadUiNavigation.TryGetNavigationMove(out var navigationMove))
            {
                if (_optionsPanel != null && _optionsPanel.activeSelf)
                {
                    NavigateOptionsMenu(navigationMove);
                }
                else
                {
                    NavigatePauseMenu(navigationMove);
                }
            }

            if (_optionsPanel != null && _optionsPanel.activeSelf)
            {
                if (GamepadUiNavigation.WasCancelPressedThisFrame())
                {
                    CloseOptionsPanel();
                    return;
                }

                GamepadUiNavigation.HandleSubmit();
                return;
            }

            GamepadUiNavigation.HandleSubmit();

        }

        if (!WasPausePressedThisFrame())
        {
            return;
        }

        TogglePause();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        _isPaused = false;
        _nextRebindAttemptTime = 0f;
        RebindSceneObjects(scene);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        ClearEditorSelectionForSceneObjects(scene);
    }

    private void RebindSceneObjects(Scene scene)
    {
        GamepadUiNavigation.EnsureEventSystem();
        UnbindButtons();

        if (_instantiatedOptionsPanel != null)
        {
            ClearEditorSelectionForInstantiatedOptionsPanel();
            Destroy(_instantiatedOptionsPanel);
            _instantiatedOptionsPanel = null;
        }

        _pauseCanvasRoot = FindPauseCanvas(scene);
        _pausePanel = FindPausePanel();
        _optionsPanel = null;

        if (_pauseCanvasRoot != null)
        {
            _optionsPanel = CreateOptionsPanelInstance();
        }

        if (_optionsPanel == null)
        {
            _optionsPanel = ResolveOptionsPanel(scene);
        }

        if (_optionsPanel == null)
        {
            _optionsPanel = CreateFallbackOptionsPanel();
        }

        AttachOptionsPanelToPauseCanvas();

        _healthHud = FindSceneObject(scene, HealthHudName, includeInactive: true);
        _pauseOptionsButton = FindButton(_pausePanel, PauseOptionsButtonName) ?? FindButton(_pauseCanvasRoot, PauseOptionsButtonName);
        _pauseQuitButton = FindButton(_pausePanel, PauseQuitButtonName) ?? FindButton(_pauseCanvasRoot, PauseQuitButtonName);
        _optionsExitButton = FindButton(_optionsPanel, OptionsExitButtonName);
        _easyDifficultyButton = FindButton(_optionsPanel, EasyButtonName);
        _hardDifficultyButton = FindButton(_optionsPanel, HardButtonName);
        _musicVolumeSlider = FindSlider(_optionsPanel, MusicSliderName);

        ConfigureButtons();
        ConfigureNavigation();
        BindButtons();
        ApplySavedSettings();
        ShowPauseCanvas(false);
        ShowOptionsPanel(false);
        ShowHealthHud(true);
    }

    private void TogglePause()
    {
        if (_isPaused)
        {
            ResumeGame();
            return;
        }

        PauseGame();
    }

    private void PauseGame()
    {
        _isPaused = true;
        SetGameplayInputActive(false);
        Time.timeScale = 0f;
        ShowPauseCanvas(true);
        ShowOptionsPanel(false);
        ShowHealthHud(false);
        SelectPauseButton(_pauseSelectionIndex);
    }

    private void ResumeGame()
    {
        ResumeGame(reactivateGameplayInput: true);
    }

    private void ResumeGame(bool reactivateGameplayInput)
    {
        _isPaused = false;
        Time.timeScale = 1f;
        if (reactivateGameplayInput)
        {
            SetGameplayInputActive(true);
        }

        ShowOptionsPanel(false);
        ShowPauseCanvas(false);
        ShowHealthHud(true);
        ClearSelection();
    }

    private void OpenOptionsPanel()
    {
        if (!_isPaused)
        {
            PauseGame();
        }

        EnsureBindingsForActiveScene(force: true);

        if (_optionsPanel == null || !_optionsPanel)
        {
            _optionsPanel = CreateOptionsPanelInstance();
        }

        if (_optionsPanel == null)
        {
            _optionsPanel = CreateFallbackOptionsPanel();
        }

        if (_optionsPanel != null)
        {
            AttachOptionsPanelToPauseCanvas();
            _optionsExitButton = FindButton(_optionsPanel, OptionsExitButtonName);
            _easyDifficultyButton = FindButton(_optionsPanel, EasyButtonName);
            _hardDifficultyButton = FindButton(_optionsPanel, HardButtonName);
            _musicVolumeSlider = FindSlider(_optionsPanel, MusicSliderName);
            ConfigureButtons();
            ConfigureNavigation();
            BindButtons();
            ApplySavedSettings();
        }

        ShowPauseCanvas(true);
        ShowOptionsPanel(true);
        ShowHealthHud(false);
        SelectOptionsButton(_optionsSelectionIndex);
    }

    private void EnsureBindingsForActiveScene(bool force = false)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!ShouldManageScene(activeScene))
        {
            return;
        }

        if (!force && Time.unscaledTime < _nextRebindAttemptTime)
        {
            return;
        }

        var requiresRebind = force ||
            _pauseCanvasRoot == null || !_pauseCanvasRoot ||
            _pauseOptionsButton == null || !_pauseOptionsButton ||
            _pauseQuitButton == null || !_pauseQuitButton ||
            (_instantiatedOptionsPanel != null && (!_instantiatedOptionsPanel || _instantiatedOptionsPanel.transform.parent == null));

        if (!requiresRebind)
        {
            return;
        }

        _nextRebindAttemptTime = Time.unscaledTime + 0.35f;
        RebindSceneObjects(activeScene);
    }

    private bool ShouldManageScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        if (scene.name == MainMenuSceneName)
        {
            return false;
        }

        return !scene.name.Contains("Loading");
    }

    private void CloseOptionsPanel()
    {
        ShowOptionsPanel(false);
        ShowPauseCanvas(true);
        ShowHealthHud(false);
        SelectPauseButton(_pauseSelectionIndex);
    }

    private void QuitToMainMenu()
    {
        ResumeGame(reactivateGameplayInput: false);
        SceneTransitionManager.DestroyPersistentPlayers();
        SceneTransitionManager.Instance.LoadSceneWithoutSpawn(MainMenuSceneName);
    }

    private void SetEasyDifficulty()
    {
        SetDifficulty(GameDifficulty.Easy);
    }

    private void SetHardDifficulty()
    {
        SetDifficulty(GameDifficulty.Hard);
    }

    private void SetDifficulty(GameDifficulty difficulty)
    {
        GameSettings.Difficulty = difficulty;
        ApplyDifficultyVisuals(difficulty);
    }

    private void ApplySavedSettings()
    {
        ApplyDifficultyVisuals(GameSettings.Difficulty);

        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        }

        AudioController.Instance?.RefreshMusicVolume();
    }

    private void ApplyDifficultyVisuals(GameDifficulty difficulty)
    {
        SetDifficultyButtonState(_easyDifficultyButton, difficulty == GameDifficulty.Easy);
        SetDifficultyButtonState(_hardDifficultyButton, difficulty == GameDifficulty.Hard);
    }

    private void SetDifficultyButtonState(Button button, bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        var hover = button.GetComponent<MenuButtonHoverText>();
        hover?.SetLockedState(isSelected, SelectedDifficultyColor);

        var targetText = button.GetComponentInChildren<TMP_Text>(true);
        var textHover = targetText != null ? targetText.GetComponent<MenuButtonHoverText>() : null;
        textHover?.SetLockedState(isSelected, SelectedDifficultyColor);
    }

    private void OnMusicVolumeChanged(float volume)
    {
        if (AudioController.Instance != null)
        {
            AudioController.Instance.SetMusicVolume(volume);
            return;
        }

        GameSettings.MusicVolume = volume;
    }

    private void BindButtons()
    {
        if (_pauseOptionsButton != null)
        {
            _pauseOptionsButton.onClick.RemoveListener(OpenOptionsPanel);
            _pauseOptionsButton.onClick.AddListener(OpenOptionsPanel);
        }

        if (_pauseQuitButton != null)
        {
            _pauseQuitButton.onClick.RemoveListener(QuitToMainMenu);
            _pauseQuitButton.onClick.AddListener(QuitToMainMenu);
        }

        if (_optionsExitButton != null)
        {
            _optionsExitButton.onClick.RemoveAllListeners();
            _optionsExitButton.onClick.AddListener(CloseOptionsPanel);
        }

        if (_easyDifficultyButton != null)
        {
            _easyDifficultyButton.onClick.RemoveListener(SetEasyDifficulty);
            _easyDifficultyButton.onClick.AddListener(SetEasyDifficulty);
        }

        if (_hardDifficultyButton != null)
        {
            _hardDifficultyButton.onClick.RemoveListener(SetHardDifficulty);
            _hardDifficultyButton.onClick.AddListener(SetHardDifficulty);
        }

        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }

    private void UnbindButtons()
    {
        if (_pauseOptionsButton != null)
        {
            _pauseOptionsButton.onClick.RemoveListener(OpenOptionsPanel);
        }

        if (_pauseQuitButton != null)
        {
            _pauseQuitButton.onClick.RemoveListener(QuitToMainMenu);
        }

        if (_optionsExitButton != null)
        {
            _optionsExitButton.onClick.RemoveListener(CloseOptionsPanel);
        }

        if (_easyDifficultyButton != null)
        {
            _easyDifficultyButton.onClick.RemoveListener(SetEasyDifficulty);
        }

        if (_hardDifficultyButton != null)
        {
            _hardDifficultyButton.onClick.RemoveListener(SetHardDifficulty);
        }

        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }
    }

    private void ConfigureButtons()
    {
        ConfigureButtonHover(_pauseOptionsButton);
        ConfigureButtonHover(_pauseQuitButton);
        ConfigureButtonHover(_optionsExitButton);
        ConfigureButtonHover(_easyDifficultyButton);
        ConfigureButtonHover(_hardDifficultyButton);
    }

    private void ConfigureNavigation()
    {
        LinkVertical(_pauseOptionsButton, null, _pauseQuitButton);
        LinkVertical(_pauseQuitButton, _pauseOptionsButton, null);

        if (_easyDifficultyButton != null && _hardDifficultyButton != null)
        {
            var easyNavigation = _easyDifficultyButton.navigation;
            easyNavigation.mode = Navigation.Mode.Explicit;
            easyNavigation.selectOnRight = _hardDifficultyButton;
            easyNavigation.selectOnDown = _musicVolumeSlider != null ? _musicVolumeSlider : _optionsExitButton;
            _easyDifficultyButton.navigation = easyNavigation;

            var hardNavigation = _hardDifficultyButton.navigation;
            hardNavigation.mode = Navigation.Mode.Explicit;
            hardNavigation.selectOnLeft = _easyDifficultyButton;
            hardNavigation.selectOnDown = _musicVolumeSlider != null ? _musicVolumeSlider : _optionsExitButton;
            _hardDifficultyButton.navigation = hardNavigation;
        }

        if (_musicVolumeSlider != null)
        {
            var sliderNavigation = _musicVolumeSlider.navigation;
            sliderNavigation.mode = Navigation.Mode.Explicit;
            sliderNavigation.selectOnUp = _easyDifficultyButton != null ? _easyDifficultyButton : _hardDifficultyButton;
            sliderNavigation.selectOnDown = _optionsExitButton;
            _musicVolumeSlider.navigation = sliderNavigation;
        }

        if (_optionsExitButton != null)
        {
            var exitNavigation = _optionsExitButton.navigation;
            exitNavigation.mode = Navigation.Mode.Explicit;
            exitNavigation.selectOnUp = _musicVolumeSlider != null ? _musicVolumeSlider : (_easyDifficultyButton != null ? _easyDifficultyButton : _hardDifficultyButton);
            _optionsExitButton.navigation = exitNavigation;
        }
    }

    private void NavigatePauseMenu(Vector2Int move)
    {
        if (move.y == 0)
        {
            return;
        }

        var buttons = GetPauseButtons();
        if (buttons.Length == 0)
        {
            return;
        }

        _pauseSelectionIndex = Mathf.Clamp(_pauseSelectionIndex + (move.y < 0 ? 1 : -1), 0, buttons.Length - 1);
        SelectPauseButton(_pauseSelectionIndex);
    }

    private void NavigateOptionsMenu(Vector2Int move)
    {
        var selectables = GetOptionsSelectables();
        if (selectables.Length == 0)
        {
            return;
        }

        _optionsSelectionIndex = Mathf.Clamp(_optionsSelectionIndex, 0, selectables.Length - 1);
        var current = selectables[_optionsSelectionIndex];

        if (current == _musicVolumeSlider)
        {
            if (move.x != 0)
            {
                AdjustSliderValue(_musicVolumeSlider, move.x);
            }

            if (move.y > 0)
            {
                SelectOptionsControl(_easyDifficultyButton != null ? _easyDifficultyButton : _hardDifficultyButton);
            }
            else if (move.y < 0)
            {
                SelectOptionsControl(_optionsExitButton);
            }

            return;
        }

        if ((current == _easyDifficultyButton || current == _hardDifficultyButton) && move.x != 0 && _easyDifficultyButton != null && _hardDifficultyButton != null)
        {
            SelectOptionsControl(current == _easyDifficultyButton ? _hardDifficultyButton : _easyDifficultyButton);
            return;
        }

        if (move.y < 0)
        {
            if (current == _easyDifficultyButton || current == _hardDifficultyButton)
            {
                SelectOptionsControl(_musicVolumeSlider != null ? (Selectable)_musicVolumeSlider : _optionsExitButton);
                return;
            }

            if (current == _optionsExitButton)
            {
                SelectOptionsControl(GetPreferredUpperOptionsSelection());
            }

            return;
        }

        if (move.y > 0 && current == _optionsExitButton)
        {
            SelectOptionsControl(_musicVolumeSlider != null ? (Selectable)_musicVolumeSlider : GetPreferredUpperOptionsSelection());
        }
    }

    private void SelectPauseButton(int index)
    {
        var buttons = GetPauseButtons();
        if (buttons.Length == 0)
        {
            return;
        }

        _pauseSelectionIndex = Mathf.Clamp(index, 0, buttons.Length - 1);
        SelectButton(buttons[_pauseSelectionIndex]);
    }

    private void SelectOptionsButton(int index)
    {
        var selectables = GetOptionsSelectables();
        if (selectables.Length == 0)
        {
            return;
        }

        _optionsSelectionIndex = Mathf.Clamp(index, 0, selectables.Length - 1);
        SelectSelectable(selectables[_optionsSelectionIndex]);
    }

    private Button[] GetPauseButtons()
    {
        return FilterButtons(_pauseOptionsButton, _pauseQuitButton);
    }

    private Selectable[] GetOptionsSelectables()
    {
        return FilterSelectables(_easyDifficultyButton, _hardDifficultyButton, _musicVolumeSlider, _optionsExitButton);
    }

    private static Button[] FilterButtons(params Button[] buttons)
    {
        var result = new System.Collections.Generic.List<Button>();
        for (var index = 0; index < buttons.Length; index++)
        {
            if (buttons[index] != null)
            {
                result.Add(buttons[index]);
            }
        }

        return result.ToArray();
    }

    private static Selectable[] FilterSelectables(params Selectable[] selectables)
    {
        var result = new System.Collections.Generic.List<Selectable>();
        for (var index = 0; index < selectables.Length; index++)
        {
            if (selectables[index] != null)
            {
                result.Add(selectables[index]);
            }
        }

        return result.ToArray();
    }

    private Selectable GetPreferredOptionsSelection()
    {
        if (_optionsSelectionIndex >= 0)
        {
            var selectables = GetOptionsSelectables();
            if (_optionsSelectionIndex < selectables.Length)
            {
                return selectables[_optionsSelectionIndex];
            }
        }

        return GetPreferredUpperOptionsSelection() ?? (Selectable)_musicVolumeSlider ?? _optionsExitButton;
    }

    private Selectable GetPreferredUpperOptionsSelection()
    {
        return _easyDifficultyButton != null ? _easyDifficultyButton : _hardDifficultyButton;
    }

    private void SelectOptionsControl(Selectable selectable)
    {
        if (selectable == null)
        {
            return;
        }

        var selectables = GetOptionsSelectables();
        for (var index = 0; index < selectables.Length; index++)
        {
            if (selectables[index] != selectable)
            {
                continue;
            }

            _optionsSelectionIndex = index;
            SelectSelectable(selectable);
            return;
        }
    }

    private static void AdjustSliderValue(Slider slider, int horizontalDirection)
    {
        if (slider == null || horizontalDirection == 0)
        {
            return;
        }

        var step = Mathf.Max(0.01f, (slider.maxValue - slider.minValue) * 0.05f);
        slider.SetValueWithoutNotify(Mathf.Clamp(slider.value + step * horizontalDirection, slider.minValue, slider.maxValue));
        slider.onValueChanged.Invoke(slider.value);
    }

    private void AttachOptionsPanelToPauseCanvas()
    {
        if (_optionsPanel == null || _pauseCanvasRoot == null)
        {
            return;
        }

        if (_optionsPanel.transform.parent != _pauseCanvasRoot.transform)
        {
            _optionsPanel.transform.SetParent(_pauseCanvasRoot.transform, false);
        }

        var rectTransform = _optionsPanel.transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = Vector3.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.SetAsLastSibling();
    }

    private void ShowPauseCanvas(bool isVisible)
    {
        if (_pauseCanvasRoot != null)
        {
            _pauseCanvasRoot.SetActive(isVisible);
        }

        if (_pausePanel != null)
        {
            _pausePanel.SetActive(isVisible);
        }
    }

    private void ShowOptionsPanel(bool isVisible)
    {
        if (_optionsPanel != null)
        {
            AttachOptionsPanelToPauseCanvas();
            var rectTransform = _optionsPanel.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.SetAsLastSibling();
            }

            _optionsPanel.SetActive(isVisible);
        }

        if (_pausePanel != null && _pausePanel != _pauseCanvasRoot)
        {
            _pausePanel.SetActive(!isVisible);
        }
    }

    private void ShowHealthHud(bool isVisible)
    {
        if (_healthHud != null)
        {
            _healthHud.SetActive(isVisible);
        }
    }

    private static bool WasPausePressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        return GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) &&
            (gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame);
    }

    private static void SetGameplayInputActive(bool isActive)
    {
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player == null)
            {
                continue;
            }

            if (isActive)
            {
                player.ActivateInput();
                continue;
            }

            player.DeactivateInput();
        }
    }

    private static void SelectButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        SelectSelectable(button);
    }

    private static void SelectSelectable(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private static void ClearSelection()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
    }

    private static GameObject FindPauseCanvas(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root == null || root.name != PauseCanvasName)
            {
                continue;
            }

            if (root.GetComponent<Canvas>() != null)
            {
                return root;
            }
        }

        return FindSceneObject(scene, PauseCanvasName, includeInactive: true);
    }

    private GameObject FindPausePanel()
    {
        if (_pauseCanvasRoot == null)
        {
            return null;
        }

        var children = _pauseCanvasRoot.GetComponentsInChildren<Transform>(true);
        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            if (child == null || child.gameObject == _pauseCanvasRoot)
            {
                continue;
            }

            if (child.name != PausePanelName)
            {
                continue;
            }

            if (FindButton(child.gameObject, PauseOptionsButtonName) != null || FindButton(child.gameObject, PauseQuitButtonName) != null)
            {
                return child.gameObject;
            }
        }

        return _pauseCanvasRoot;
    }

    private GameObject ResolveOptionsPanel(Scene scene)
    {
        if (_pauseCanvasRoot != null)
        {
            var panelUnderPause = FindNamedChild(_pauseCanvasRoot, OptionsPanelName);
            if (IsOptionsPanel(panelUnderPause))
            {
                return panelUnderPause;
            }
        }

        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root == null)
            {
                continue;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                var current = transforms[transformIndex];
                if (current == null || current.name != OptionsPanelName)
                {
                    continue;
                }

                if (IsOptionsPanel(current.gameObject))
                {
                    return current.gameObject;
                }
            }
        }

        return null;
    }

    private static GameObject FindNamedChild(GameObject parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        var transforms = parent.GetComponentsInChildren<Transform>(true);
        for (var index = 0; index < transforms.Length; index++)
        {
            var current = transforms[index];
            if (current != null && current.gameObject != parent && current.name == objectName)
            {
                return current.gameObject;
            }
        }

        return null;
    }

    private bool IsOptionsPanel(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return FindButton(candidate, OptionsExitButtonName) != null &&
            FindButton(candidate, EasyButtonName) != null &&
            FindButton(candidate, HardButtonName) != null &&
            FindSlider(candidate, MusicSliderName) != null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName, bool includeInactive)
    {
        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            var root = roots[index];
            if (root == null)
            {
                continue;
            }

            if ((includeInactive || root.activeInHierarchy) && root.name == objectName)
            {
                return root;
            }

            var transforms = root.GetComponentsInChildren<Transform>(includeInactive);
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

    private GameObject CreateOptionsPanelInstance()
    {
        if (_pauseCanvasRoot == null)
        {
            return null;
        }

        if (_instantiatedOptionsPanel != null)
        {
            if (_instantiatedOptionsPanel && _instantiatedOptionsPanel.transform.parent == _pauseCanvasRoot.transform)
            {
                return _instantiatedOptionsPanel;
            }

            if (_instantiatedOptionsPanel)
            {
                ClearEditorSelectionForInstantiatedOptionsPanel();
                Destroy(_instantiatedOptionsPanel);
            }

            _instantiatedOptionsPanel = null;
        }

        var optionsPrefab = Resources.Load<GameObject>(OptionsPanelResourceName);
        if (optionsPrefab == null)
        {
            return null;
        }

        _instantiatedOptionsPanel = Instantiate(optionsPrefab, _pauseCanvasRoot.transform);
        _instantiatedOptionsPanel.name = OptionsPanelName;

        var rectTransform = _instantiatedOptionsPanel.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.SetAsLastSibling();
        }

        _instantiatedOptionsPanel.SetActive(false);
        return _instantiatedOptionsPanel;
    }

    private GameObject CreateFallbackOptionsPanel()
    {
        if (_pauseCanvasRoot == null)
        {
            return null;
        }

        var panelObject = new GameObject(OptionsPanelName, typeof(RectTransform), typeof(Image));
        _instantiatedOptionsPanel = panelObject;
        panelObject.transform.SetParent(_pauseCanvasRoot.transform, false);

        var rootRect = panelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.2f, 0.15f);
        rootRect.anchorMax = new Vector2(0.8f, 0.85f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.SetAsLastSibling();

        var rootImage = panelObject.GetComponent<Image>();
        rootImage.color = new Color(0.1f, 0.1f, 0.1f, 0.96f);

        CreateLabel(panelObject.transform, "OptionsTitle", "Options", new Vector2(0.5f, 0.88f), 34);
        CreateButton(panelObject.transform, EasyButtonName, "Easy", new Vector2(0.35f, 0.65f), new Vector2(220f, 52f));
        CreateButton(panelObject.transform, HardButtonName, "Hard", new Vector2(0.65f, 0.65f), new Vector2(220f, 52f));
        CreateLabel(panelObject.transform, "MusicVolumeLabel", "Music volume", new Vector2(0.5f, 0.46f), 28);
        CreateSlider(panelObject.transform, MusicSliderName, new Vector2(0.5f, 0.34f), new Vector2(360f, 24f));
        CreateButton(panelObject.transform, OptionsExitButtonName, "Exit", new Vector2(0.5f, 0.16f), new Vector2(220f, 52f));

        panelObject.SetActive(false);
        return panelObject;
    }

    private static TMP_Text CreateLabel(Transform parent, string objectName, string text, Vector2 anchor, float fontSize)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        var rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = new Vector2(420f, 50f);
        rectTransform.anchoredPosition = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    private static Button CreateButton(Transform parent, string objectName, string text, Vector2 anchor, Vector2 size)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var button = buttonObject.GetComponent<Button>();
        CreateLabel(buttonObject.transform, "Text (TMP)", text, new Vector2(0.5f, 0.5f), 24f);
        return button;
    }

    private static Slider CreateSlider(Transform parent, string objectName, Vector2 anchor, Vector2 size)
    {
        var sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        var rectTransform = sliderObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundObject.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(10f, 4f);
        fillAreaRect.offsetMax = new Vector2(-10f, -4f);

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillArea.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillObject.GetComponent<Image>().color = new Color(0.8f, 0.45f, 0f, 1f);

        var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideArea.transform.SetParent(sliderObject.transform, false);
        var handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleSlideArea.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 34f);
        handleObject.GetComponent<Image>().color = Color.white;

        var slider = sliderObject.GetComponent<Slider>();
        slider.targetGraphic = handleObject.GetComponent<Image>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = GameSettings.MusicVolume;
        return slider;
    }

    private static Button FindButton(GameObject parent, string buttonName)
    {
        if (parent == null)
        {
            return null;
        }

        var buttons = parent.GetComponentsInChildren<Button>(true);
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            if (button != null && button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static Slider FindSlider(GameObject parent, string sliderName)
    {
        if (parent == null)
        {
            return null;
        }

        var sliders = parent.GetComponentsInChildren<Slider>(true);
        for (var index = 0; index < sliders.Length; index++)
        {
            var slider = sliders[index];
            if (slider != null && slider.name == sliderName)
            {
                return slider;
            }
        }

        return null;
    }

    private static void LinkVertical(Selectable current, Selectable up, Selectable down)
    {
        if (current == null)
        {
            return;
        }

        var navigation = current.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        current.navigation = navigation;
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

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ClearEditorSelectionForInstantiatedOptionsPanel()
    {
#if UNITY_EDITOR
        if (_instantiatedOptionsPanel == null)
        {
            return;
        }

        var selectedObject = Selection.activeObject;
        if (selectedObject is GameObject selectedGameObject)
        {
            if (selectedGameObject == _instantiatedOptionsPanel || selectedGameObject.transform.IsChildOf(_instantiatedOptionsPanel.transform))
            {
                Selection.activeObject = null;
                Selection.objects = System.Array.Empty<Object>();
            }

            return;
        }

        if (selectedObject is Component selectedComponent &&
            (selectedComponent.gameObject == _instantiatedOptionsPanel || selectedComponent.transform.IsChildOf(_instantiatedOptionsPanel.transform)))
        {
            Selection.activeObject = null;
            Selection.objects = System.Array.Empty<Object>();
        }
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ClearEditorSelectionForSceneObjects(Scene scene)
    {
#if UNITY_EDITOR
        if (!scene.IsValid())
        {
            return;
        }

        var selectedObject = Selection.activeObject;
        if (selectedObject is GameObject selectedGameObject)
        {
            if (selectedGameObject.scene == scene)
            {
                Selection.activeObject = null;
                Selection.objects = System.Array.Empty<Object>();
            }

            return;
        }

        if (selectedObject is Component selectedComponent && selectedComponent.gameObject.scene == scene)
        {
            Selection.activeObject = null;
            Selection.objects = System.Array.Empty<Object>();
        }
#endif
    }
}