using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverHide : MonoBehaviour, IPointerClickHandler
{
    public Animator animator;
    public void OnPointerClick(PointerEventData eventData)
    {
        animator.SetTrigger("Start");
        gameObject.SetActive(false);
    }
}