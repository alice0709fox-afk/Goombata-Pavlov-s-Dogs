using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private static readonly Color SelectedDifficultyColor = new Color(0.82f, 0.22f, 0.18f, 1f);

    private const string NewGameButtonName = "ButtonNewGame";
    private const string OptionsButtonName = "ButtonOptions";
    private const string OptionsPanelName = "Panel";
    private const string OptionsExitButtonName = "ButtonExit";
    private const string QuitButtonName = "ButtonQuit";
    private const string EasyButtonName = "Easy";
    private const string HardButtonName = "Hard";
    private const string MusicSliderName = "Slider";

    [SerializeField] private string levelSceneName = "Level_0";
    [SerializeField] private AudioClip newGameShotSfx;
    [SerializeField] [Range(0f, 1f)] private float shotVolume = 1f;
    [SerializeField] private float loadDelayAfterShot = 0.2f;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button optionsExitButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button easyDifficultyButton;
    [SerializeField] private Button hardDifficultyButton;
    [SerializeField] private Slider musicVolumeSlider;

    private AudioSource _sfxSource;
    private bool _isStartingGame;
    private int _mainMenuSelectionIndex;
    private int _optionsSelectionIndex;

    private void Awake()
    {
        _sfxSource = GetOrCreateAudioSource("MenuSfxSource");
        GamepadUiNavigation.EnsureEventSystem();

        ResolveReferences();
        ConfigureMenuButtons();
        ConfigureNavigation();
        BindButtons();
    }

    private void Start()
    {
        ResolveReferences();
        ApplySavedSettings();
        SetOptionsPanelVisible(false);
        SelectMainMenuButton(_mainMenuSelectionIndex);
    }

    private void Update()
    {
        if (_isStartingGame)
        {
            return;
        }

        if (!GamepadUiNavigation.TryGetActiveGamepad(out _))
        {
            return;
        }

        var fallbackSelection = optionsPanel != null && optionsPanel.activeSelf
            ? GetPreferredOptionsSelection()
            : newGameButton;

        GamepadUiNavigation.EnsureSelection(fallbackSelection);

        if (GamepadUiNavigation.TryGetNavigationMove(out var navigationMove))
        {
            GameSettings.PreferredControlScheme = "Gamepad";

            if (optionsPanel != null && optionsPanel.activeSelf)
            {
                NavigateOptionsMenu(navigationMove);
            }
            else
            {
                NavigateMainMenu(navigationMove);
            }
        }

        if (optionsPanel != null && optionsPanel.activeSelf)
        {
            if (GamepadUiNavigation.WasCancelPressedThisFrame())
            {
                GameSettings.PreferredControlScheme = "Gamepad";
                CloseOptionsPanel();
            }

            GamepadUiNavigation.HandleSubmit();

            return;
        }

        GamepadUiNavigation.HandleSubmit();
    }

    private void OnDestroy()
    {
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OpenOptionsPanel);
        }

        if (optionsExitButton != null)
        {
            optionsExitButton.onClick.RemoveListener(CloseOptionsPanel);
        }

        if (easyDifficultyButton != null)
        {
            easyDifficultyButton.onClick.RemoveListener(SetEasyDifficulty);
        }

        if (hardDifficultyButton != null)
        {
            hardDifficultyButton.onClick.RemoveListener(SetHardDifficulty);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }
    }

    public void NewGame()
    {
        if (_isStartingGame)
        {
            return;
        }

        if (GamepadUiNavigation.TryGetActiveGamepad(out var gamepad) && (gamepad.buttonSouth.isPressed || gamepad.startButton.isPressed))
        {
            GameSettings.PreferredControlScheme = "Gamepad";
        }

        StartCoroutine(StartNewGameSequence());
    }

    private IEnumerator StartNewGameSequence()
    {
        _isStartingGame = true;

        if (_sfxSource != null && newGameShotSfx != null)
        {
            _sfxSource.PlayOneShot(newGameShotSfx, shotVolume);
        }

        var delay = Mathf.Max(0f, loadDelayAfterShot);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        SceneTransitionManager.Instance.LoadScene(levelSceneName);
    }

    public void OpenOptionsPanel()
    {
        ResolveReferences();
        if (GamepadUiNavigation.TryGetActiveGamepad(out _))
        {
            GameSettings.PreferredControlScheme = "Gamepad";
        }

        SetOptionsPanelVisible(true);
        SelectOptionsButton(_optionsSelectionIndex);
    }

    public void CloseOptionsPanel()
    {
        ResolveReferences();
        SetOptionsPanelVisible(false);
        SelectMainMenuButton(_mainMenuSelectionIndex);
    }

    public void SetEasyDifficulty()
    {
        SetDifficulty(GameDifficulty.Easy);
    }

    public void SetHardDifficulty()
    {
        SetDifficulty(GameDifficulty.Hard);
    }

    private AudioSource GetOrCreateAudioSource(string sourceName)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.name = sourceName;
        return source;
    }

    private void ResolveReferences()
    {
        optionsPanel = ResolveSceneObject(optionsPanel, OptionsPanelName);
        newGameButton = ResolveButton(newGameButton, NewGameButtonName);
        optionsButton = ResolveButton(optionsButton, OptionsButtonName);
        optionsExitButton = ResolveButton(optionsExitButton, OptionsExitButtonName);
        quitButton = ResolveButton(quitButton, QuitButtonName);
        easyDifficultyButton = ResolveButton(easyDifficultyButton, EasyButtonName);
        hardDifficultyButton = ResolveButton(hardDifficultyButton, HardButtonName);
        musicVolumeSlider = ResolveSlider(musicVolumeSlider, MusicSliderName);
    }

    private void BindButtons()
    {
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OpenOptionsPanel);
            optionsButton.onClick.AddListener(OpenOptionsPanel);
        }

        if (optionsExitButton != null)
        {
            optionsExitButton.onClick.RemoveListener(CloseOptionsPanel);
            optionsExitButton.onClick.AddListener(CloseOptionsPanel);
        }

        if (easyDifficultyButton != null)
        {
            easyDifficultyButton.onClick.RemoveListener(SetEasyDifficulty);
            easyDifficultyButton.onClick.AddListener(SetEasyDifficulty);
        }

        if (hardDifficultyButton != null)
        {
            hardDifficultyButton.onClick.RemoveListener(SetHardDifficulty);
            hardDifficultyButton.onClick.AddListener(SetHardDifficulty);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
    }

    private void ConfigureMenuButtons()
    {
        ConfigureButtonHover(newGameButton);
        ConfigureButtonHover(optionsButton);
        ConfigureButtonHover(optionsExitButton);
        ConfigureButtonHover(quitButton);
        ConfigureButtonHover(easyDifficultyButton);
        ConfigureButtonHover(hardDifficultyButton);
    }

    private void ConfigureNavigation()
    {
        LinkVertical(newGameButton, null, optionsButton);
        LinkVertical(optionsButton, newGameButton, quitButton);
        LinkVertical(quitButton, optionsButton, null);

        if (easyDifficultyButton != null && hardDifficultyButton != null)
        {
            var easyNavigation = easyDifficultyButton.navigation;
            easyNavigation.mode = Navigation.Mode.Explicit;
            easyNavigation.selectOnRight = hardDifficultyButton;
            easyNavigation.selectOnDown = musicVolumeSlider != null ? musicVolumeSlider : optionsExitButton;
            easyDifficultyButton.navigation = easyNavigation;

            var hardNavigation = hardDifficultyButton.navigation;
            hardNavigation.mode = Navigation.Mode.Explicit;
            hardNavigation.selectOnLeft = easyDifficultyButton;
            hardNavigation.selectOnDown = musicVolumeSlider != null ? musicVolumeSlider : optionsExitButton;
            hardDifficultyButton.navigation = hardNavigation;
        }

        if (musicVolumeSlider != null)
        {
            var sliderNavigation = musicVolumeSlider.navigation;
            sliderNavigation.mode = Navigation.Mode.Explicit;
            sliderNavigation.selectOnUp = easyDifficultyButton != null ? easyDifficultyButton : hardDifficultyButton;
            sliderNavigation.selectOnDown = optionsExitButton;
            musicVolumeSlider.navigation = sliderNavigation;
        }

        if (optionsExitButton != null)
        {
            var exitNavigation = optionsExitButton.navigation;
            exitNavigation.mode = Navigation.Mode.Explicit;
            exitNavigation.selectOnUp = musicVolumeSlider != null ? musicVolumeSlider : (easyDifficultyButton != null ? easyDifficultyButton : hardDifficultyButton);
            optionsExitButton.navigation = exitNavigation;
        }
    }

    private void SetOptionsPanelVisible(bool isVisible)
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(isVisible);
        }
    }

    private Button FindButton(string buttonName)
    {
        var buttonObject = FindSceneObject(gameObject.scene, buttonName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private Slider FindSlider(string sliderName)
    {
        var sliderObject = FindSceneObject(gameObject.scene, sliderName);
        return sliderObject != null ? sliderObject.GetComponent<Slider>() : null;
    }

    private GameObject ResolveSceneObject(GameObject currentObject, string objectName)
    {
        if (currentObject != null && currentObject.name == objectName)
        {
            return currentObject;
        }

        return FindSceneObject(gameObject.scene, objectName);
    }

    private Button ResolveButton(Button currentButton, string buttonName)
    {
        if (currentButton != null && currentButton.name == buttonName)
        {
            return currentButton;
        }

        return FindButton(buttonName);
    }

    private Slider ResolveSlider(Slider currentSlider, string sliderName)
    {
        if (currentSlider != null && currentSlider.name == sliderName)
        {
            return currentSlider;
        }

        return FindSlider(sliderName);
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

    private void ApplySavedSettings()
    {
        ApplyDifficultyVisuals(GameSettings.Difficulty);

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        }

        AudioController.Instance?.RefreshMusicVolume();
    }

    private void SetDifficulty(GameDifficulty difficulty)
    {
        GameSettings.Difficulty = difficulty;
        ApplyDifficultyVisuals(difficulty);
    }

    private void NavigateMainMenu(Vector2Int move)
    {
        if (move.y == 0)
        {
            return;
        }

        var buttons = GetMainMenuButtons();
        if (buttons.Length == 0)
        {
            return;
        }

        _mainMenuSelectionIndex = Mathf.Clamp(_mainMenuSelectionIndex + (move.y < 0 ? 1 : -1), 0, buttons.Length - 1);
        SelectMainMenuButton(_mainMenuSelectionIndex);
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

        if (current == musicVolumeSlider)
        {
            if (move.x != 0)
            {
                AdjustSliderValue(musicVolumeSlider, move.x);
            }

            if (move.y > 0)
            {
                SelectOptionsControl(easyDifficultyButton != null ? easyDifficultyButton : hardDifficultyButton);
            }
            else if (move.y < 0)
            {
                SelectOptionsControl(optionsExitButton);
            }

            return;
        }

        if ((current == easyDifficultyButton || current == hardDifficultyButton) && move.x != 0 && easyDifficultyButton != null && hardDifficultyButton != null)
        {
            SelectOptionsControl(current == easyDifficultyButton ? hardDifficultyButton : easyDifficultyButton);
            return;
        }

        if (move.y < 0)
        {
            if (current == easyDifficultyButton || current == hardDifficultyButton)
            {
                SelectOptionsControl(musicVolumeSlider != null ? (Selectable)musicVolumeSlider : optionsExitButton);
                return;
            }

            if (current == optionsExitButton)
            {
                SelectOptionsControl(GetPreferredUpperOptionsSelection());
            }

            return;
        }

        if (move.y > 0 && current == optionsExitButton)
        {
            SelectOptionsControl(musicVolumeSlider != null ? (Selectable)musicVolumeSlider : GetPreferredUpperOptionsSelection());
        }
    }

    private void SelectMainMenuButton(int index)
    {
        var buttons = GetMainMenuButtons();
        if (buttons.Length == 0 || EventSystem.current == null)
        {
            return;
        }

        _mainMenuSelectionIndex = Mathf.Clamp(index, 0, buttons.Length - 1);
        EventSystem.current.SetSelectedGameObject(buttons[_mainMenuSelectionIndex].gameObject);
    }

    private void SelectOptionsButton(int index)
    {
        var selectables = GetOptionsSelectables();
        if (selectables.Length == 0 || EventSystem.current == null)
        {
            return;
        }

        _optionsSelectionIndex = Mathf.Clamp(index, 0, selectables.Length - 1);
        EventSystem.current.SetSelectedGameObject(selectables[_optionsSelectionIndex].gameObject);
    }

    private Button[] GetMainMenuButtons()
    {
        return FilterButtons(newGameButton, optionsButton, quitButton);
    }

    private Selectable[] GetOptionsSelectables()
    {
        return FilterSelectables(easyDifficultyButton, hardDifficultyButton, musicVolumeSlider, optionsExitButton);
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

        return GetPreferredUpperOptionsSelection() ?? (Selectable)musicVolumeSlider ?? optionsExitButton;
    }

    private Selectable GetPreferredUpperOptionsSelection()
    {
        return easyDifficultyButton != null ? easyDifficultyButton : hardDifficultyButton;
    }

    private void SelectOptionsControl(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
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
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
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

    private void ApplyDifficultyVisuals(GameDifficulty difficulty)
    {
        SetDifficultyButtonState(easyDifficultyButton, difficulty == GameDifficulty.Easy);
        SetDifficultyButtonState(hardDifficultyButton, difficulty == GameDifficulty.Hard);
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

}