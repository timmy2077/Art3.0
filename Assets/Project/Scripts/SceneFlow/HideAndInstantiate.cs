using UnityEngine;

public class HideAndInstantiate : MonoBehaviour
{
    public GameObject uiToHide;
    public GameObject prefabToInstantiate;

    public void OnAnimationFrame()
    {
        // 视频模式下旧动画事件不可再生成一份关卡。
        if (!isActiveAndEnabled) return;
        if (uiToHide != null)
        {
            CanvasGroup canvasGroup = uiToHide.GetComponentInParent<CanvasGroup>(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                uiToHide.SetActive(false);
            }
        }

        if (prefabToInstantiate != null)
        {
            GameObject instance = Instantiate(prefabToInstantiate, Vector3.zero, Quaternion.identity);
            UiClickSoundInstaller.ScanGameObject(instance);
        }

        gameObject.SetActive(false);
    }
}
