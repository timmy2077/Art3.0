using UnityEngine;
using UnityEngine.EventSystems;

///=============================================================================
/// 通用按钮：点击后隐藏一个物体、显示一个物体
/// 挂在带 Button（或任意可点击 Graphic）的 UI 上即可。
/// 也可以在其他按钮的 OnClick 中绑定 OnClick() 方法。
///=============================================================================
public class ButtonHideShow : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("点击后要隐藏的物体（可为空）")]
    public GameObject objectToHide;

    [Tooltip("点击后要显示的物体（可为空）")]
    public GameObject objectToShow;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    /// <summary>点击触发的方法，也可在 Button 的 OnClick 中直接绑定</summary>
    public void OnClick()
    {
        if (objectToHide != null)
            objectToHide.SetActive(false);

        if (objectToShow != null)
            objectToShow.SetActive(true);
    }
}
