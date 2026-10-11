using UnityEngine;

///=============================================================================
/// 对话管理器（全局单例）
/// 管理所有 NPC 的对话流程状态机，控制 DialogueUI 的显示与切换。
///
/// 挂载方式：在场景中创建一个空物体，挂上此脚本和 DialogueUI。
/// 或者挂在 Canvas 上（和 DialogueUI 同一物体）。
///
/// 对话流程（核心NPC）：
///   1. 按E交互 → 寒暄对话（greetingLines）
///   2. 寒暄完毕 → 弹出【任务线 / 科普线】选项
///   3. 选任务线 → 播放任务对话 → 完成后弹出科普线选项
///   4. 选科普线 → 播放科普对话 → 完成后弹出任务线选项
///   5. 任务线完成后，离开NPC再交互 → 直接播放科普对话
///
/// 对话流程（次要NPC）：
///   按E交互 → 直接播放科普对话 → 结束
///=============================================================================
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("对话UI引用")]
    [Tooltip("拖入场景中的 DialogueUI 组件")]
    public DialogueUI dialogueUI;

    [Header("任务信息面板")]
    [Tooltip("拖入场景中的 TaskInfoPanel 组件")]
    public TaskInfoPanel taskInfoPanel;

    [Header("按键设置")]
    [Tooltip("继续对话的按键（打字中按下=跳过打字，打字完成后按下=下一条）")]
    public KeyCode continueKey = KeyCode.E;

    [Tooltip("备选继续按键")]
    public KeyCode continueKeyAlt = KeyCode.Space;

    [Tooltip("鼠标左键是否也能继续对话")]
    public bool mouseClickToContinue = true;

    // ==================== 对话流程状态 ====================
    private enum FlowState
    {
        None,               // 无对话进行中
        Greeting,           // 寒暄中
        WaitingChoice,      // 等待选择（任务/科普）
        TaskLine,           // 任务线对话中
        ScienceLine,        // 科普线对话中
        AfterTaskChoice,    // 任务线结束后，弹出科普选项
        AfterScienceChoice, // 科普线结束后，弹出任务选项
        MinorNPC            // 次要NPC对话中
    }

    private FlowState state = FlowState.None;
    private NPCInteraction currentNPC;
    private string[] currentLines;
    private int currentLineIndex;

    /// <summary>当前是否有对话正在进行</summary>
    public bool IsDialogueActive => state != FlowState.None;

    // ==================== 事件（可选订阅） ====================

    /// <summary>对话开始时触发（可用于禁用玩家移动）</summary>
    public static event System.Action OnDialogueStart;

    /// <summary>对话结束时触发（可用于恢复玩家移动）</summary>
    public static event System.Action OnDialogueEnd;

    // ==================== 生命周期 ====================

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        TaskDataStorage storage = taskInfoPanel != null ? taskInfoPanel.taskStorage : null;
        if (PlayerTaskManager.Instance == null || storage == null) return;

        PlayerTaskManager.Instance.RestoreTask(storage);
        if (PlayerTaskManager.Instance.CurrentState != PlayerTaskManager.QuestState.NotAccepted)
            taskInfoPanel.Show();
    }

    void Update()
    {
        if (state == FlowState.None) return;

        // 选项面板显示时，不响应继续键
        if (state == FlowState.WaitingChoice ||
            state == FlowState.AfterTaskChoice ||
            state == FlowState.AfterScienceChoice)
            return;

        // 检测继续按键
        if (Input.GetKeyDown(continueKey) || Input.GetKeyDown(continueKeyAlt))
        {
            OnContinue();
        }
        else if (mouseClickToContinue && Input.GetMouseButtonDown(0))
        {
            OnContinue();
        }
    }

    // ==================== 开始对话 ====================

    /// <summary>由 NPCInteraction 调用，开始一段对话</summary>
    public void StartDialogue(NPCInteraction npc)
    {
        if (state != FlowState.None) return; // 已有对话进行中

        currentNPC = npc;
        DialogueData data = npc.dialogueData;

        dialogueUI.ShowPanel();
        dialogueUI.SetNPCName(data.npcName);

        OnDialogueStart?.Invoke();

        if (!data.isCoreNPC)
        {
            // -------- 次要NPC：直接科普 --------
            state = FlowState.MinorNPC;
            currentLines = data.minorNPCLines;
        }
        else if (IsCurrentTaskAccepted())
        {
            // -------- 核心NPC，任务已完成：直接科普 --------
            state = FlowState.ScienceLine;
            currentLines = data.scienceLines;
        }
        else
        {
            // -------- 核心NPC，首次交互：寒暄 --------
            state = FlowState.Greeting;
            currentLines = data.greetingLines;
        }

        currentLineIndex = 0;
        ShowCurrentLine();
    }

    // ==================== 继续对话 ====================

    /// <summary>继续下一条对话（按键或点击触发）</summary>
    public void OnContinue()
    {
        if (state == FlowState.None) return;

        // 打字中 → 跳过打字，显示完整文本
        if (dialogueUI != null && dialogueUI.IsTyping)
        {
            dialogueUI.ShowFullText();
            return;
        }

        // 下一条
        currentLineIndex++;
        if (currentLineIndex < currentLines.Length)
        {
            ShowCurrentLine();
        }
        else
        {
            // 当前阶段对话全部播完
            OnPhaseComplete();
        }
    }

    // ==================== 选项选择 ====================

    /// <summary>玩家点击了选项按钮（由 DialogueUI 自动调用）</summary>
    /// <param name="isTask">true=任务线，false=科普线</param>
    public void OnChoiceSelected(bool isTask)
    {
        // 只在等待选择的状态下响应
        if (state != FlowState.WaitingChoice &&
            state != FlowState.AfterTaskChoice &&
            state != FlowState.AfterScienceChoice)
            return;

        if (currentNPC == null) return;

        dialogueUI.HideChoices();

        if (isTask && !IsCurrentTaskAccepted())
        {
            // 进入任务线
            state = FlowState.TaskLine;
            currentLines = currentNPC.dialogueData.taskLines;
            currentLineIndex = 0;
            ShowCurrentLine();
        }
        else if (!isTask && !currentNPC.IsScienceCompleted)
        {
            // 进入科普线
            state = FlowState.ScienceLine;
            currentLines = currentNPC.dialogueData.scienceLines;
            currentLineIndex = 0;
            ShowCurrentLine();
        }
    }

    // ==================== 内部流程 ====================

    private void ShowCurrentLine()
    {
        if (currentLines == null || currentLines.Length == 0)
        {
            OnPhaseComplete();
            return;
        }
        dialogueUI.ShowDialogueLine(currentLines[currentLineIndex]);
    }

    /// <summary>当前阶段（寒暄/任务/科普）对话全部播完后的处理</summary>
    private void OnPhaseComplete()
    {
        switch (state)
        {
            case FlowState.Greeting:
                // 寒暄完毕 → 弹出选项
                state = FlowState.WaitingChoice;
                ShowChoices();
                break;

            case FlowState.TaskLine:
            {
                // 任务线完毕即接取任务，不再依赖玩家离开NPC范围。
                TaskDataStorage storage = taskInfoPanel != null ? taskInfoPanel.taskStorage : null;
                if (PlayerTaskManager.Instance != null && storage != null)
                {
                    PlayerTaskManager.Instance.AcceptTask(storage);
                    taskInfoPanel.Show();
                }
                else
                {
                    Debug.LogWarning("[DialogueManager] 缺少 PlayerTaskManager 或任务配置，无法接取任务。", this);
                }
                EndDialogue();
                break;
            }

            case FlowState.ScienceLine:
                // 科普线完毕 → 标记完成
                currentNPC.IsScienceCompleted = true;
                if (!IsCurrentTaskAccepted())
                {
                    // 任务未完成 → 弹出任务选项
                    state = FlowState.AfterScienceChoice;
                    ShowChoices();
                }
                else
                {
                    // 任务已接取，科普播放完毕后结束对话。
                    EndDialogue();
                }
                break;

            case FlowState.MinorNPC:
                // 次要NPC对话完毕 → 结束
                EndDialogue();
                break;
        }
    }

    /// <summary>显示选项面板，根据完成状态决定显示哪些按钮</summary>
    private void ShowChoices()
    {
        DialogueData data = currentNPC.dialogueData;
        bool showTask = !IsCurrentTaskAccepted();
        bool showScience = !currentNPC.IsScienceCompleted;

        dialogueUI.ShowChoices(
            data.taskChoiceText, showTask,
            data.scienceChoiceText, showScience
        );
    }

    /// <summary>结束对话</summary>
    private void EndDialogue()
    {
        state = FlowState.None;
        dialogueUI.HideChoices();
        dialogueUI.HidePanel();

        currentNPC?.OnDialogueEnded();
        currentNPC = null;

        OnDialogueEnd?.Invoke();
    }

    /// <summary>玩家离开NPC范围时由 NPCInteraction 调用</summary>
    public void OnPlayerLeftNPC()
    {
        // 离开范围只结束交互提示；任务已在任务线对话结束时接取。
    }

    /// <summary>完成当前任务，隐藏任务信息面板</summary>
    public void CompleteTask()
    {
        if (taskInfoPanel != null)
            taskInfoPanel.Hide();
    }

    private bool IsCurrentTaskAccepted()
    {
        TaskDataStorage storage = taskInfoPanel != null ? taskInfoPanel.taskStorage : null;
        return PlayerTaskManager.Instance != null
            && storage != null
            && PlayerTaskManager.Instance.IsQuestAccepted(storage);
    }
}
