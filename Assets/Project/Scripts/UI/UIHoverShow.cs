using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverShow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public GameObject targetText;
    public GameObject targetUI;
    [Min(0), Tooltip("0 不检查；大于0时必须已解锁此关卡才能进入。")]
    public int requiredLevelId;

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
        if (requiredLevelId>0 && (DataSystem.instance==null || !DataSystem.instance.IsLevelUnlocked(requiredLevelId))) return;
        if (targetUI != null)
        {
            targetUI.SetActive(true);
        }
    }
}
