using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossVictoryPanelManager : MonoBehaviour
{
    private static BossVictoryPanelManager _instance;

    [Header("Panel Binding")]
    [SerializeField] private Canvas victoryCanvas;
    [SerializeField] private GameObject victoryPanelRoot;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Button nextButton;

    [Header("Formatting")]
    [SerializeField] private string scorePrefix = "";
    [SerializeField] private string timePrefix = "";

    [Header("Button Action")]
    [SerializeField] private UnityEvent onNextClicked;
    [SerializeField] private string nextSceneName = "Level_2";
    [SerializeField] private string nextSpawnPointName = "TargetSpawnPointName";

    [Header("Show Delay")]
    [SerializeField, Min(0f)] private float extraShowDelay = 0f;

    private bool _isVisible;
    private bool _showPending;
    private float _elapsedAtVictory;
    private Scene _boundScene;
    private Coroutine _showRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        var host = new GameObject(nameof(BossVictoryPanelManager));
        _instance = host.AddComponent<BossVictoryPanelManager>();
        DontDestroyOnLoad(host);
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
        EnemyController.AnyEnemyDied += OnAnyEnemyDied;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        EnemyController.AnyEnemyDied -= OnAnyEnemyDied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindNextButton();
    }

    private void Start()
    {
        if (!IsBossScene(SceneManager.GetActiveScene()))
            return;

        ResolveBindings(SceneManager.GetActiveScene());
        SetPanelVisible(false);
    }

    private void Update()
    {
        if (!IsBossScene(SceneManager.GetActiveScene()))
            return;

        if (_isVisible)
        {
            if (victoryPanelRoot != null && victoryPanelRoot.activeInHierarchy && nextButton != null)
            {
                GamepadUiNavigation.EnsureEventSystem();

                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != nextButton.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
                }

                if (GamepadUiNavigation.WasSubmitPressedThisFrame())
                {
                    nextButton.onClick.Invoke();
                }
            }

            return;
        }

        if (victoryPanelRoot != null)
            return;

        ResolveBindings(SceneManager.GetActiveScene());
        if (victoryPanelRoot != null)
            victoryPanelRoot.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        ResetBindings();

        if (!IsBossScene(scene))
            return;

        ResolveBindings(scene);
        SetPanelVisible(false);
    }

    private void OnAnyEnemyDied(EnemyController enemy)
    {
        HandleEnemyDied(enemy);
    }

    public static void NotifyEnemyDied(EnemyController enemy)
    {
        EnsureInstance();
        _instance?.HandleEnemyDied(enemy);
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        _instance = FindFirstObjectByType<BossVictoryPanelManager>();
        if (_instance != null)
            return;

        var host = new GameObject(nameof(BossVictoryPanelManager));
        _instance = host.AddComponent<BossVictoryPanelManager>();
        DontDestroyOnLoad(host);
    }

    private void HandleEnemyDied(EnemyController enemy)
    {
        if (!IsBossScene(SceneManager.GetActiveScene()))
            return;

        if (_isVisible || _showPending || enemy == null)
            return;

        if (!IsBossEnemy(enemy))
            return;

        _showRoutine = StartCoroutine(ShowVictoryAfterDeathRoutine(enemy));
    }

    private System.Collections.IEnumerator ShowVictoryAfterDeathRoutine(EnemyController enemy)
    {
        _showPending = true;

        var delay = extraShowDelay;
        if (enemy != null)
            delay += enemy.DeathPresentationDuration;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _showPending = false;
        _showRoutine = null;

        if (_isVisible)
            yield break;

        ShowVictory();
    }

    private static bool IsBossEnemy(EnemyController enemy)
    {
        if (enemy == null)
            return false;

        if (enemy.GetComponent<BossController>() != null)
            return true;

        if (enemy.GetComponentInParent<BossController>() != null)
            return true;

        if (enemy.GetComponentInChildren<BossController>(true) != null)
            return true;

        // CompareTag throws if a tag is missing in Tag Manager. Use plain tag string check safely.
        if (string.Equals(enemy.tag, "Boss", System.StringComparison.Ordinal))
            return true;

        if (enemy.name.ToLowerInvariant().Contains("boss"))
            return true;

        // Фолбэк: если в сцене не найдено явных "boss" маркеров, считаем смерть врага победой.
        var hasBossController = FindFirstObjectByType<BossController>(FindObjectsInactive.Include) != null;
        if (!hasBossController)
            return true;

        return false;
    }

    private void ShowVictory()
    {
        ResolveBindings(SceneManager.GetActiveScene());

        if (victoryPanelRoot == null)
        {
            Debug.LogError("BossVictoryPanelManager: Victory panel not found in expected hierarchy Canvas/Victory.");
            return;
        }

        if (victoryCanvas != null)
            victoryCanvas.gameObject.SetActive(true);

        if (victoryPanelRoot != null)
            victoryPanelRoot.SetActive(true);

        _elapsedAtVictory = Time.timeSinceLevelLoad;
        RefreshTexts();
        if (SetPanelVisible(true))
            Time.timeScale = 0f;
    }

    private void RefreshTexts()
    {
        if (scoreText != null)
            scoreText.text = scorePrefix + GetCurrentScore();

        if (timeText != null)
            timeText.text = timePrefix + FormatTime(_elapsedAtVictory);
    }

    private int GetCurrentScore()
    {
        var souls = FindObjectsByType<PlayerSouls>(FindObjectsSortMode.None);
        var total = 0;
        for (int i = 0; i < souls.Length; i++)
        {
            if (souls[i] != null)
                total += souls[i].CurrentSouls;
        }

        return total;
    }

    private static string FormatTime(float time)
    {
        var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(time));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private bool SetPanelVisible(bool visible)
    {
        _isVisible = visible;

        if (victoryPanelRoot == null)
            return false;

        victoryPanelRoot.SetActive(visible);

        if (visible)
        {
            BindNextButton();
            if (nextButton != null)
                GamepadUiNavigation.EnsureSelection(nextButton);
        }
        else
        {
            UnbindNextButton();
        }

        return true;
    }

    private void BindNextButton()
    {
        if (nextButton == null)
            return;

        nextButton.onClick.RemoveListener(OnNextButtonClicked);
        nextButton.onClick.AddListener(OnNextButtonClicked);
    }

    private void UnbindNextButton()
    {
        if (nextButton == null)
            return;

        nextButton.onClick.RemoveListener(OnNextButtonClicked);
    }

    private void OnNextButtonClicked()
    {
        Time.timeScale = 1f;
        onNextClicked?.Invoke();

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("BossVictoryPanelManager: Next scene is not configured.", this);
            return;
        }

        var transitionManager = SceneTransitionManager.Instance;
        if (transitionManager == null)
        {
            Debug.LogError("BossVictoryPanelManager: SceneTransitionManager is not available.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSpawnPointName))
        {
            transitionManager.LoadSceneWithoutSpawn(nextSceneName);
            return;
        }

        transitionManager.LoadScene(nextSceneName, nextSpawnPointName);
    }

    private void ResolveBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (!IsBossScene(scene))
            return;

        if (_boundScene == scene && victoryPanelRoot != null)
            return;

        _boundScene = scene;

        var roots = scene.GetRootGameObjects();

        // Жесткая привязка к вашей иерархии: Canvas/Victory/(Score, Time, Button).
        if (TryBindExactVictoryHierarchy(roots))
        {
            BindNextButton();
            return;
        }
    }

    private bool TryBindExactVictoryHierarchy(GameObject[] roots)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;

            if (!root.name.Equals("Canvas", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                continue;

            var panel = root.transform.Find("Victory");
            if (panel == null)
                continue;

            var scoreValueNode = panel.Find("Score/ScoreTXT");
            var timeValueNode = panel.Find("Time/TimeTXT");
            var buttonNode = panel.Find("Button");
            var button = buttonNode != null ? buttonNode.GetComponent<Button>() : null;

            if (scoreValueNode == null || timeValueNode == null || button == null)
                continue;

            victoryCanvas = canvas;
            victoryPanelRoot = panel.gameObject;
            scoreText = scoreValueNode.GetComponent<TMP_Text>();
            timeText = timeValueNode.GetComponent<TMP_Text>();
            nextButton = button;

            return true;
        }

        return false;
    }

    private static bool IsBossScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        if (scene.name.ToLowerInvariant().Contains("boss"))
            return true;

        return FindFirstObjectByType<BossController>(FindObjectsInactive.Include) != null;
    }

    private void ResetBindings()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        UnbindNextButton();
        victoryCanvas = null;
        victoryPanelRoot = null;
        scoreText = null;
        timeText = null;
        nextButton = null;
        _boundScene = default;
        _isVisible = false;
        _showPending = false;
    }
}
