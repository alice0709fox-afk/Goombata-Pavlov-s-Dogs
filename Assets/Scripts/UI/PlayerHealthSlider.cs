using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHealthSlider : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private bool autoFindFillImage = true;
    [SerializeField] private bool hideWhenPlayerMissing;
    [SerializeField] private Color fillColor = new Color(0.1496978f, 0.9622642f, 0.031772893f, 1f);
    [SerializeField] private string sliderObjectName = "HpPlayer";
    [Header("Size By Max HP")]
    [SerializeField] private bool resizeByMaxHealth = true;
    [SerializeField, Min(1f)] private float baseMaxHealthForSize = 100f;
    [SerializeField, Min(0f)] private float widthPerHealthPoint = 1.2f;
    [SerializeField, Min(1f)] private float maxWidthMultiplier = 3f;

    private PlayerHealth _subscribedPlayerHealth;
    private RectTransform _rectTransform;
    private RectTransform _sliderRectTransform;
    private float _baseSliderWidth;
    private float _nextRebindAttemptTime;

    private void Reset()
    {
        _rectTransform = transform as RectTransform;
        ResolveSlider();
        ResolveFillImage();
        ApplyFillColor();
    }

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        ResolveSlider();
        CacheSliderRect();

        ResolveFillImage();
        ApplyFillColor();
        NormalizeScale();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Rebind();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnValidate()
    {
        _rectTransform = transform as RectTransform;
        ResolveSlider();
        CacheSliderRect();

        ResolveFillImage();
        ApplyFillColor();
        RefreshImmediately();
    }

    private void Update()
    {
        if (!autoFindPlayer || _subscribedPlayerHealth != null || Time.unscaledTime < _nextRebindAttemptTime)
        {
            return;
        }

        _nextRebindAttemptTime = Time.unscaledTime + 0.25f;
        Rebind();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Rebind();
    }

    public void Rebind()
    {
        Unsubscribe();

        ResolveSlider();
        CacheSliderRect();

        ResolveFillImage();
        ApplyFillColor();
        NormalizeScale();

        if (autoFindPlayer)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            UpdateVisibility();
            return;
        }

        _subscribedPlayerHealth = playerHealth;
        _subscribedPlayerHealth.HealthChanged += HandleHealthChanged;
        HandleHealthChanged(_subscribedPlayerHealth.CurrentHealth, _subscribedPlayerHealth.MaxHealth);
    }

    private void Unsubscribe()
    {
        if (_subscribedPlayerHealth == null)
        {
            return;
        }

        _subscribedPlayerHealth.HealthChanged -= HandleHealthChanged;
        _subscribedPlayerHealth = null;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, maxHealth);
        slider.value = Mathf.Clamp(currentHealth, 0f, slider.maxValue);
        UpdateSliderSize(maxHealth);
        UpdateVisibility();
    }

    private void RefreshImmediately()
    {
        if (slider == null)
        {
            return;
        }

        if (playerHealth != null)
        {
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            return;
        }

        UpdateVisibility();
    }

    private void ResolveFillImage()
    {
        if (!autoFindFillImage || slider == null)
        {
            return;
        }

        if (slider.fillRect != null)
        {
            fillImage = slider.fillRect.GetComponent<Image>();
        }
    }

    private void ResolveSlider()
    {
        if (slider != null && slider.fillRect != null)
        {
            return;
        }

        slider = GetComponent<Slider>();
        if (slider != null && slider.fillRect != null)
        {
            return;
        }

        var sliders = GetComponentsInChildren<Slider>(true);
        for (var index = 0; index < sliders.Length; index++)
        {
            var candidate = sliders[index];
            if (candidate != null && candidate.fillRect != null)
            {
                slider = candidate;
                return;
            }
        }

        if (sliders.Length > 0)
        {
            slider = sliders[0];
        }
    }

    private void CacheSliderRect()
    {
        if (slider == null)
        {
            return;
        }

        _sliderRectTransform = slider.transform as RectTransform;
        if (_sliderRectTransform != null && _baseSliderWidth <= 0f)
        {
            _baseSliderWidth = Mathf.Max(1f, _sliderRectTransform.sizeDelta.x);
        }
    }

    private void ApplyFillColor()
    {
        if (fillImage != null)
        {
            fillImage.color = fillColor;
            if (fillImage.type == Image.Type.Filled)
            {
                fillImage.type = Image.Type.Sliced;
            }
        }
    }

    private void NormalizeScale()
    {
        if (_rectTransform == null)
        {
            return;
        }

        var scale = _rectTransform.localScale;
        if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f))
        {
            _rectTransform.localScale = Vector3.one;
        }
    }

    private void UpdateSliderSize(float maxHealth)
    {
        if (!resizeByMaxHealth)
        {
            return;
        }

        if (_sliderRectTransform == null)
        {
            CacheSliderRect();
        }

        if (_sliderRectTransform == null || _baseSliderWidth <= 0f)
        {
            return;
        }

        var targetHealth = Mathf.Max(1f, maxHealth);
        var baseHealth = Mathf.Max(1f, baseMaxHealthForSize);
        var extraHealth = Mathf.Max(0f, targetHealth - baseHealth);
        var grownWidth = _baseSliderWidth + extraHealth * widthPerHealthPoint;
        var maxWidth = _baseSliderWidth * Mathf.Max(1f, maxWidthMultiplier);
        var finalWidth = Mathf.Clamp(grownWidth, _baseSliderWidth, maxWidth);

        var size = _sliderRectTransform.sizeDelta;
        size.x = finalWidth;
        _sliderRectTransform.sizeDelta = size;
    }

    private void UpdateVisibility()
    {
        if (slider == null)
        {
            return;
        }

        if (!hideWhenPlayerMissing)
        {
            if (!string.IsNullOrWhiteSpace(sliderObjectName) && gameObject.name != sliderObjectName)
            {
                gameObject.name = sliderObjectName;
            }

            return;
        }

        gameObject.SetActive(playerHealth != null);
    }
}