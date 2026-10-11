using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class ShowObjectOnClick : MonoBehaviour, IPointerClickHandler
{
    [Header("点击后显示")]
    [Tooltip("可选。点击后显示的UI物体。")]
    public GameObject targetUI;

    [Header("点击后切换场景")]
    [Tooltip("可选。填写后点击会加载该场景。")]
    public string targetSceneName;

    [Tooltip("可选。绑定后先播放视频，播放结束再切换场景。")]
    public VideoPlayer videoPlayer;

    private bool transitionStarted;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnPointerClick();
    }

    // 保留无参数入口，兼容 Inspector 中已有的 Button/Event 调用。
    public void OnPointerClick()
    {
        if (transitionStarted) return;

        if (targetUI != null)
            targetUI.SetActive(true);

        if (string.IsNullOrWhiteSpace(targetSceneName))
            return;

        transitionStarted = true;
        if (videoPlayer != null)
        {
            GameObject videoRoot = videoPlayer.transform.root.gameObject;
            videoRoot.SetActive(true);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.Play();
            return;
        }

        LoadTargetScene();
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        source.loopPointReached -= OnVideoFinished;
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            transitionStarted = false;
            Debug.LogError($"[ShowObjectOnClick] 场景未加入 Build Settings：{targetSceneName}", this);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}
