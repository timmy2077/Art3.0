using UnityEngine;
using UnityEngine.UI;

/// <summary>同一选关入口保留两种入场方式，只启用被选中的一套。</summary>
[DefaultExecutionOrder(-500)]
public sealed class LevelEntranceSelector : MonoBehaviour
{
    public enum EntranceMode { 视频, 序列帧 }

    [Header("入场方式（退出运行后修改并保存场景）")]
    public EntranceMode mode = EntranceMode.视频;
    [Header("视频播放")]
    public LevelVideoEntrance videoEntrance;
    [Header("保留的原序列帧配置")]
    public Animator sequenceAnimator;
    public Image sequenceImage;
    public HideAndInstantiate sequenceCompletion;

    private void OnEnable()
    {
        bool useVideo = mode == EntranceMode.视频;
        // 先关闭原动画及其生成事件，避免两种流程同时实例化关卡。
        if (sequenceAnimator != null) sequenceAnimator.enabled = !useVideo;
        if (sequenceImage != null) sequenceImage.enabled = !useVideo;
        if (sequenceCompletion != null) sequenceCompletion.enabled = !useVideo;
        if (videoEntrance != null) videoEntrance.enabled = useVideo;
        if (!useVideo && sequenceAnimator != null && sequenceAnimator.runtimeAnimatorController != null)
        {
            sequenceAnimator.Rebind();
            sequenceAnimator.Play(0, 0, 0);
        }
    }
}
