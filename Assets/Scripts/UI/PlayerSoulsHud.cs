using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerSoulsHud : MonoBehaviour
{
    [SerializeField] private PlayerSouls playerSouls;
    [SerializeField] private TMP_Text soulsText;
    [SerializeField] private string labelPrefix = "Money: ";
    [SerializeField] private string loadingSceneName = "2_Loading";

    private Canvas _canvas;

    private void Awake()
    {
        if (playerSouls == null)
            playerSouls = GetComponent<PlayerSouls>() ?? GetComponentInParent<PlayerSouls>() ?? FindFirstObjectByType<PlayerSouls>();

        EnsureHud();
        RefreshText(playerSouls != null ? playerSouls.CurrentSouls : 0);
        UpdateHudVisibility(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        if (playerSouls != null)
            playerSouls.SoulsChanged += RefreshText;

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        if (playerSouls != null)
            playerSouls.SoulsChanged -= RefreshText;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void EnsureHud()
    {
        if (soulsText != null)
            return;

        var canvasObject = new GameObject("SoulsCanvas");
        canvasObject.transform.SetParent(transform, false);

        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var textObject = new GameObject("SoulsText");
        textObject.transform.SetParent(canvasObject.transform, false);

        var rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-35f, -25f);
        rectTransform.sizeDelta = new Vector2(420f, 70f);

        soulsText = textObject.AddComponent<TextMeshProUGUI>();
        soulsText.alignment = TextAlignmentOptions.Right;
        soulsText.fontSize = 36f;
        soulsText.color = new Color(0.98f, 0.85f, 0.35f, 1f);
        soulsText.outlineColor = new Color(0f, 0f, 0f, 1f);
        soulsText.outlineWidth = 0.2f;
    }

    private void RefreshText(int souls)
    {
        if (soulsText == null)
            return;

        soulsText.text = labelPrefix + souls;
    }

    private void OnActiveSceneChanged(Scene current, Scene next)
    {
        UpdateHudVisibility(next);
    }

    private void UpdateHudVisibility(Scene scene)
    {
        if (_canvas == null)
            return;

        _canvas.enabled = !IsLoadingScene(scene.name);
    }

    private bool IsLoadingScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (!string.IsNullOrWhiteSpace(loadingSceneName) && sceneName == loadingSceneName)
            return true;

        return sceneName.Contains("Loading");
    }
}