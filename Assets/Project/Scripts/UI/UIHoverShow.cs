using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverShow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public GameObject targetText;
    public GameObject targetUI;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetText != null)
        {
            targetText.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetText != null)
        {
            targetText.SetActive(false);
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetUI != null)
        {
            targetUI.SetActive(true);
        }
    }
}