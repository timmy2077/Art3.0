using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>仅由 Start → Index 触发的不可跳过序章，使用非缩放时间播放。</summary>
public class IndexPrologue : MonoBehaviour
{
    [SerializeField] private Text narrative;
    [SerializeField] private CanvasGroup curtain;
    [SerializeField, TextArea(5, 16)] private string story =
        "我是一名学习文物修复的学生。\n\n一次整理馆藏时，我的指尖触到一块残损的魏晋画像砖。\n砖上的墨线忽然流动，将我带入了画中的河西。\n\n这里的人与往事，正随着砖画的风化渐渐消散。\n我必须寻回散落的碎片，修复五块画像砖，\n让被尘封的生活重新完整。\n\n唯有读懂砖上的故事，将它们连成一幅长卷，\n我才能重新开启砖门，找到回到现世的路。";
    [SerializeField, Min(.01f)] private float characterInterval = .065f;
    [SerializeField, Min(0)] private float sectionHoldSeconds = 3f;
    [SerializeField, Min(0)] private float sectionGapSeconds = .5f;
    [SerializeField, Min(0)] private float finalHoldSeconds = 5f;
    [SerializeField, Min(.1f)] private float fadeSeconds = 1f;

    private static bool requested;
    public static bool IsPlaying { get; private set; }
    private readonly List<EventSystem> blockedSystems = new List<EventSystem>();
    private Coroutine playback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { requested = false; IsPlaying = false; }
    public static void Request() { requested = true; }
    public static bool ConsumeRequest() { bool value = requested; requested = false; return value; }

    public void Begin()
    {
        if (playback != null) return;
        IsPlaying = true;
        curtain.alpha = 1;
        curtain.blocksRaycasts = true;
        narrative.text = "";
        BlockInput();
        playback = StartCoroutine(Play());
    }

    private void BlockInput()
    {
        foreach (EventSystem system in FindObjectsOfType<EventSystem>())
        {
            if (!system.enabled) continue;
            blockedSystems.Add(system);
            system.SetSelectedGameObject(null);
            system.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (IsPlaying) BlockInput();
    }

    private IEnumerator Play()
    {
        yield return new WaitForSecondsRealtime(.6f);
        // 空行分节，单个换行仍属于同一节；兼容 Inspector 中的 Windows 换行。
        string[] sections = story.Replace("\r\n", "\n").Split(
            new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            string section = sections[sectionIndex].Trim();
            narrative.text = "";
            for (int i = 0; i < section.Length; i++)
            {
                narrative.text = section.Substring(0, i + 1);
                char character = section[i];
                float pause = character == '。' ? .65f : character == '，' ? .22f : character == '\n' ? .20f : characterInterval;
                yield return new WaitForSecondsRealtime(pause);
            }
            yield return new WaitForSecondsRealtime(sectionIndex == sections.Length - 1 ? finalHoldSeconds : sectionHoldSeconds);
            if (sectionIndex < sections.Length - 1)
            {
                narrative.text = "";
                yield return new WaitForSecondsRealtime(sectionGapSeconds);
            }
        }
        float elapsed = 0;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            curtain.alpha = 1 - Mathf.Clamp01(elapsed / fadeSeconds);
            yield return null;
        }
        // 避免播放结束同帧的点击/按键穿透至菜单。
        while (Input.anyKey || Input.touchCount > 0) yield return null;
        yield return null;
        ReleaseInput();
        Destroy(gameObject);
    }

    private void ReleaseInput()
    {
        foreach (EventSystem system in blockedSystems) if (system != null) system.enabled = true;
        blockedSystems.Clear();
        IsPlaying = false;
    }

    private void OnDestroy() { ReleaseInput(); }
    private void OnDisable()
    {
        if (playback != null) StopCoroutine(playback);
        playback = null;
        ReleaseInput();
    }
}
