using System.Collections;
using UnityEngine;
using UnityEngine.UI;

///=============================================================================
/// 对话 UI 控制器
/// 挂在 Canvas 下的对话面板根节点上，负责显示/隐藏面板、打字机效果、选项按钮。
///
/// 需要的 UI 结构（在 Canvas 下搭建）：
///
///   DialoguePanel (GameObject)        ← 拖到 dialoguePanel
///   ├─ NPCNameText (Text)             ← 拖到 npcNameText
///   ├─ DialogueText  (Text)           ← 拖到 dialogueText
///   └─ ContinueHint  (GameObject)     ← 拖到 continueHint（"点击继续"提示，打字完成后显示）
///
///   ChoicePanel (GameObject)          ← 拖到 choicePanel
///   ├─ TaskButton    (Button+Text)    ← 拖到 taskButton / taskButtonText
///   └─ ScienceButton (Button+Text)    ← 拖到 scienceButton / scienceButtonText
///
///   InteractionHint (GameObject)      ← 拖到 interactionHint（"按E交互"提示）
///
/// 注：如果你用 TextMeshPro，把 Text 全部替换为 TMP_Text 即可。
///=============================================================================
public class DialogueUI : MonoBehaviour
{
    [Header("对话面板")]
    [Tooltip("对话面板根节点（包含名称、文本、继续提示）")]
    public GameObject dialoguePanel;

    [Tooltip("NPC 名称文本组件")]
    public Text npcNameText;

    [Tooltip("对话内容文本组件")]
    public Text dialogueText;

    [Tooltip("'点击继续'提示物体，打字完成后显示")]
    public GameObject continueHint;

    [Header("选项面板")]
    [Tooltip("选项面板根节点（包含任务线/科普线按钮）")]
    public GameObject choicePanel;

    [Tooltip("任务线选项按钮")]
    public Button taskButton;

    [Tooltip("任务线按钮上的文字")]
    public Text taskButtonText;

    [Tooltip("科普线选项按钮")]
    public Button scienceButton;

    [Tooltip("科普线按钮上的文字")]
    public Text scienceButtonText;

    [Header("交互提示")]
    [Tooltip("'按E交互'提示物体，玩家进入NPC范围时显示")]
    public GameObject interactionHint;

    [Header("打字机效果")]
    [Tooltip("每个字的间隔时间（秒），越小越快")]
    public float typeSpeed = 0.04f;

    // 内部状态
    private Coroutine typeCoroutine;
    private string currentFullText;
    private bool isTyping = false;

    /// <summary>当前是否正在打字</summary>
    public bool IsTyping => isTyping;

    void Awake()
    {
        // 自动绑定选项按钮的点击事件
        if (taskButton != null)
            taskButton.onClick.AddListener(() =>
                DialogueManager.Instance?.OnChoiceSelected(true));

        if (scienceButton != null)
            scienceButton.onClick.AddListener(() =>
                DialogueManager.Instance?.OnChoiceSelected(false));

        // 初始隐藏所有面板
        HidePanel();
        HideChoices();
        HideInteractionHint();
    }

    // ==================== 对话面板 ====================

    /// <summary>显示对话面板</summary>
    public void ShowPanel()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
    }

    /// <summary>隐藏对话面板</summary>
    public void HidePanel()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    /// <summary>设置NPC名称</summary>
    public void SetNPCName(string name)
    {
        if (npcNameText != null) npcNameText.text = name;
    }

    /// <summary>开始用打字机效果显示一行对话</summary>
    public void ShowDialogueLine(string text)
    {
        currentFullText = text;
        if (continueHint != null) continueHint.SetActive(false);

        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        typeCoroutine = StartCoroutine(TypeTextCoroutine(text));
    }

    /// <summary>跳过打字效果，立即显示完整文本</summary>
    public void ShowFullText()
    {
        if (typeCoroutine != null)
        {
            StopCoroutine(typeCoroutine);
            typeCoroutine = null;
        }
        isTyping = false;
        if (dialogueText != null) dialogueText.text = currentFullText;
        if (continueHint != null) continueHint.SetActive(true);
    }

    private IEnumerator TypeTextCoroutine(string text)
    {
        isTyping = true;
        if (dialogueText != null) dialogueText.text = "";

        foreach (char c in text)
        {
            if (dialogueText != null) dialogueText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
        typeCoroutine = null;
        if (continueHint != null) continueHint.SetActive(true);
    }

    // ==================== 选项面板 ====================

    /// <summary>
    /// 显示选项面板
    /// </summary>
    /// <param name="taskText">任务线按钮文字</param>
    /// <param name="showTask">是否显示任务线按钮</param>
    /// <param name="scienceText">科普线按钮文字</param>
    /// <param name="showScience">是否显示科普线按钮</param>
    public void ShowChoices(string taskText, bool showTask, string scienceText, bool showScience)
    {
        if (continueHint != null) continueHint.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(true);

        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(showTask);
            if (showTask && taskButtonText != null) taskButtonText.text = taskText;
        }

        if (scienceButton != null)
        {
            scienceButton.gameObject.SetActive(showScience);
            if (showScience && scienceButtonText != null) scienceButtonText.text = scienceText;
        }
    }

    /// <summary>隐藏选项面板</summary>
    public void HideChoices()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    // ==================== 交互提示 ====================

    /// <summary>显示"按E交互"提示</summary>
    public void ShowInteractionHint()
    {
        if (interactionHint != null) interactionHint.SetActive(true);
    }

    /// <summary>隐藏"按E交互"提示</summary>
    public void HideInteractionHint()
    {
        if (interactionHint != null) interactionHint.SetActive(false);
    }
}
