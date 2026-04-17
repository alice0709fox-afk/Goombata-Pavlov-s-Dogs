using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Color normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.4f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.96f, 0.64f, 0.38f, 1f);

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

        ApplyNormal();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyNormal();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyPressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ApplyHover();
    }

    public void OnSelect(BaseEventData eventData)
    {
        ApplyHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
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
}