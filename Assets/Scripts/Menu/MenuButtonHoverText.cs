using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Color normalColor = new Color(0.7169812f, 0.7169812f, 0.7169812f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.81960785f, 0.4f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.95686275f, 0.63529414f, 0.38039216f, 1f);

    public static readonly Color DefaultNormalColor = new Color(0.7169812f, 0.7169812f, 0.7169812f, 1f);
    public static readonly Color DefaultHoverColor = new Color(1f, 0.81960785f, 0.4f, 1f);
    public static readonly Color DefaultPressedColor = new Color(0.95686275f, 0.63529414f, 0.38039216f, 1f);

    private UnityEngine.UI.Selectable _selectable;
    private bool _isPointerOver;
    private bool _isPressed;
    private bool _isLocked;
    private Color _lockedColor = Color.red;

    private void Reset()
    {
        targetText = GetComponentInChildren<TMP_Text>();
    }

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>();
        }

        ResolveSelectable();

        ApplyNormal();
    }

    private void Update()
    {
        if (targetText == null)
        {
            return;
        }

        ResolveSelectable();
        if (_selectable == null)
        {
            return;
        }

        if (_isPressed)
        {
            ApplyPressed();
            return;
        }

        if (_isLocked)
        {
            ApplyLocked();
            return;
        }

        var isSelected = IsSelectedByEventSystem();

        if (_isPointerOver || isSelected)
        {
            ApplyHover();
            return;
        }

        ApplyNormal();
    }

    public void Configure(TMP_Text textComponent, Color normal, Color hover, Color pressed)
    {
        targetText = textComponent;
        normalColor = normal;
        hoverColor = hover;
        pressedColor = pressed;
        ResolveSelectable();
        ApplyNormal();
    }

    public void SetLockedState(bool isLocked, Color lockedStateColor)
    {
        _isLocked = isLocked;
        _lockedColor = lockedStateColor;

        if (_isLocked)
        {
            ApplyLocked();
            return;
        }

        ApplyNormal();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        ApplyHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        _isPressed = false;
        ApplyNormal();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        ApplyPressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        ApplyHover();
    }

    public void OnSelect(BaseEventData eventData)
    {
        ApplyHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isPressed = false;
        ApplyNormal();
    }

    private void ApplyNormal()
    {
        if (targetText != null)
        {
            targetText.color = normalColor;
        }
    }

    private void ApplyHover()
    {
        if (targetText != null)
        {
            targetText.color = hoverColor;
        }
    }

    private void ApplyPressed()
    {
        if (targetText != null)
        {
            targetText.color = pressedColor;
        }
    }

    private void ApplyLocked()
    {
        if (targetText != null)
        {
            targetText.color = _lockedColor;
        }
    }

    private void ResolveSelectable()
    {
        if (_selectable != null)
        {
            return;
        }

        _selectable = GetComponent<UnityEngine.UI.Selectable>();
        if (_selectable == null)
        {
            _selectable = GetComponentInParent<UnityEngine.UI.Selectable>();
        }
    }

    private bool IsSelectedByEventSystem()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        ResolveSelectable();
        if (_selectable == null)
        {
            return false;
        }

        var selectedTransform = EventSystem.current.currentSelectedGameObject.transform;
        var selectableTransform = _selectable.transform;

        return EventSystem.current.currentSelectedGameObject == _selectable.gameObject ||
            selectedTransform.IsChildOf(selectableTransform) ||
            selectableTransform.IsChildOf(selectedTransform);
    }
}