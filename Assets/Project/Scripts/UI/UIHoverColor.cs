using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHoverColor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Color hoverColor = Color.white;

    private Graphic graphic;
    private Color originalColor;

    void Awake()
    {
        graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            originalColor = graphic.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (graphic != null)
        {
            graphic.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (graphic != null)
        {
            graphic.color = originalColor;
        }
    }
}