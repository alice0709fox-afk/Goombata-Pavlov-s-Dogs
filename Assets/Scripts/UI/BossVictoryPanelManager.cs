using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossVictoryPanelManager : MonoBehaviour
{
    private static BossVictoryPanelManager _instance;

    [Header("Panel Binding")]
    [SerializeField] private GameObject victoryPanelRoot;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Button nextButton;

    [Header("Formatting")]
    [SerializeField] private string scorePrefix = "";
    [SerializeField] private string timePrefix = "";

    [Header("Auto Find Name Hints")]
    [SerializeField] private string[] panelNameHints = { "panel", "victory", "win", "pobeda", "panelwin", "panelvictory" };
    [SerializeField] private string[] scoreNameHints = { "scorevalue", "pointsvalue", "glasses", "scoresum", "resultscore" };
    [SerializeField] private string[] timeNameHints = { "timevalue", "timeresult", "timer_value", "resulttime" };
    [SerializeField] private string[] nextButtonNameHints = { "next", "dalee", "continue" };

    [Header("Button Action")]
    [SerializeField] private UnityEvent onNextClicked;

    private bool _isVisible;
    private float _elapsedAtVictory;
    private Scene _boundScene;

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
        ResolveBindings(SceneManager.GetActiveScene());
        SetPanelVisible(false);
    }

    private void Update()
    {
        if (_isVisible)
            return;

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
        ResolveBindings(scene);
        SetPanelVisible(false);
    }

    private void OnAnyEnemyDied(EnemyController enemy)
    {
        if (_isVisible || enemy == null)
            return;

        if (enemy.GetComponent<BossController>() == null)
            return;

        ShowVictory();
    }

    private void ShowVictory()
    {
        ResolveBindings(SceneManager.GetActiveScene());

        if (victoryPanelRoot == null)
        {
            Debug.LogError("BossVictoryPanelManager: Victory panel not found! Attempting manual bind...");
            var canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvas.Length; i++)
            {
                var panel = canvas[i].transform.Find("Panel");
                if (panel != null)
                {
                    victoryPanelRoot = panel.gameObject;
                    break;
                }
            }
        }

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

        if (onNextClicked == null || onNextClicked.GetPersistentEventCount() == 0)
            Debug.Log("BossVictoryPanelManager: Next clicked. Assign onNextClicked later to load next level.");
    }

    private void ResolveBindings(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (_boundScene == scene && victoryPanelRoot != null)
            return;

        _boundScene = scene;

        var roots = scene.GetRootGameObjects();

        // First, try to find Canvas named "Victory" directly
        if (victoryPanelRoot == null)
        {
            var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                if (allCanvases[i] != null && allCanvases[i].name == "Victory")
                {
                    victoryPanelRoot = allCanvases[i].gameObject;
                    break;
                }
            }
        }

        if (victoryPanelRoot == null)
            victoryPanelRoot = FindCanvasPanelRoot(roots);

        if (victoryPanelRoot == null)
            victoryPanelRoot = FindObjectByHints(roots, panelNameHints);

        if (victoryPanelRoot == null)
            return;

        // If victoryPanelRoot is a Canvas, don't normalize
        var canvasComponent = victoryPanelRoot.GetComponent<Canvas>();
        if (canvasComponent == null)
            victoryPanelRoot = NormalizePanelRoot(victoryPanelRoot);

        // Жесткая привязка к текущей иерархии сцены: Panel/glasses и Panel/Time/Time.
        if (scoreText == null)
        {
            var scoreByPath = FindByPath(victoryPanelRoot.transform, "glasses");
            if (scoreByPath != null)
                scoreText = scoreByPath.GetComponent<TMP_Text>();
        }

        if (timeText == null)
        {
            var timeByPath = FindByPath(victoryPanelRoot.transform, "Time/Time");
            if (timeByPath != null)
                timeText = timeByPath.GetComponent<TMP_Text>();
        }

        if (scoreText == null)
            scoreText = FindTextByHints(victoryPanelRoot.transform, scoreNameHints);

        if (timeText == null)
            timeText = FindTextByHints(victoryPanelRoot.transform, timeNameHints);

        ResolveValueTextsFromNumericPlaceholders(victoryPanelRoot.transform, ref scoreText, ref timeText);

        if (nextButton == null)
            nextButton = FindButtonByHints(victoryPanelRoot.transform, nextButtonNameHints);

        if (nextButton == null)
            nextButton = victoryPanelRoot.GetComponentInChildren<Button>(true);

        BindNextButton();
    }

    private static GameObject FindObjectByHints(GameObject[] roots, string[] hints)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindTransformByHints(roots[i].transform, hints);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static GameObject FindCanvasPanelRoot(GameObject[] roots)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;

            if (!root.name.Equals("Canvas", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var panel = root.transform.Find("Panel");
            if (panel != null)
                return panel.gameObject;
        }

        return null;
    }

    private static TMP_Text FindTextByHints(Transform root, string[] hints)
    {
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (ContainsAnyHint(texts[i].name, hints))
                return texts[i];
        }

        return null;
    }

    private static void ResolveValueTextsFromNumericPlaceholders(Transform root, ref TMP_Text score, ref TMP_Text time)
    {
        if (score != null && time != null)
            return;

        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        var numericCandidates = new System.Collections.Generic.List<TMP_Text>();

        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
                continue;

            var value = text.text.Trim();
            if (!ContainsDigit(value))
                continue;

            numericCandidates.Add(text);
        }

        if (numericCandidates.Count == 0)
            return;

        numericCandidates.Sort((a, b) => b.rectTransform.position.y.CompareTo(a.rectTransform.position.y));

        if (score == null)
            score = numericCandidates[0];

        if (time == null)
        {
            if (numericCandidates.Count > 1)
                time = numericCandidates[1];
            else
                time = numericCandidates[0];
        }
    }

    private static Button FindButtonByHints(Transform root, string[] hints)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (ContainsAnyHint(buttons[i].name, hints))
                return buttons[i];
        }

        return null;
    }

    private static Transform FindTransformByHints(Transform root, string[] hints)
    {
        if (ContainsAnyHint(root.name, hints))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindTransformByHints(root.GetChild(i), hints);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject NormalizePanelRoot(GameObject source)
    {
        if (source == null)
            return null;

        var current = source.transform;
        while (current != null)
        {
            var hasButton = current.GetComponentInChildren<Button>(true) != null;
            var textCount = current.GetComponentsInChildren<TMP_Text>(true).Length;
            if (hasButton && textCount >= 2)
                return current.gameObject;

            current = current.parent;
        }

        return source;
    }

    private static bool ContainsDigit(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
                return true;
        }

        return false;
    }

    private static bool ContainsAnyHint(string value, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(value) || hints == null)
            return false;

        var lower = value.ToLowerInvariant();
        for (int i = 0; i < hints.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(hints[i]))
                continue;

            if (lower.Contains(hints[i].ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private static Transform FindByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        return root.Find(path);
    }

    private void ResetBindings()
    {
        UnbindNextButton();
        victoryPanelRoot = null;
        scoreText = null;
        timeText = null;
        nextButton = null;
        _boundScene = default;
        _isVisible = false;
    }
}
