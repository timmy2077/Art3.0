using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 统一的 UI 点击发声组件。
/// 由 UiClickSoundInstaller 自动补挂到所有 Button / IPointerClickHandler 物体上，
/// 一般不需要手动添加。
/// 规则：
/// 1. 物体上有 Button：监听 onClick 发声；
/// 2. 物体上没有 Button（纯图形 + IPointerClickHandler）：通过指针点击发声；
/// 3. 同一物体只挂一个本组件，避免多个点击处理器导致一次点击响多声。
/// </summary>
[DisallowMultipleComponent]
public class UiClickSound : MonoBehaviour, IPointerClickHandler
{
    /// <summary>音频库 Sound Effects 中默认的按钮点击音效 ID。</summary>
    public const string DefaultSoundId = "ui_click";

    [Tooltip("音频库 Sound Effects 中的音效 ID，留空则使用默认点击音效。")]
    [SerializeField] private string soundId = DefaultSoundId;

    private Button cachedButton;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
        if (cachedButton != null)
            cachedButton.onClick.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (cachedButton != null)
            cachedButton.onClick.RemoveListener(Play);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 有 Button 时由 onClick 负责发声，这里跳过以防双响。
        if (cachedButton == null)
            Play();
    }

    /// <summary>播放点击音效，也可以在其他事件中手动调用。</summary>
    public void Play()
    {
        if (GameAudio.Instance == null) return;

        string id = string.IsNullOrWhiteSpace(soundId) ? DefaultSoundId : soundId;
        GameAudio.Instance.PlaySound(id);
    }
}
