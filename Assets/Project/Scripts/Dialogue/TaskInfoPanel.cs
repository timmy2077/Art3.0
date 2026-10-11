using UnityEngine;
using UnityEngine.UI;

///=============================================================================
/// 任务信息面板
/// 接收任务后，从玩家身上的 PlayerTaskManager 读取任务列表，
/// 按顺序渲染到 taskText 数组中的文本组件上；
/// 某个任务完成后，对应的文本变灰。
///
/// UI 结构：
///   TaskInfoPanel (Image)        ← 挂载此脚本
///   ├─ TaskText_0 (Text)         ┐
///   ├─ TaskText_1 (Text)         ├─ 按顺序拖到 taskText 数组
///   └─ TaskText_2 (Text)         ┘
///=============================================================================
public class TaskInfoPanel : MonoBehaviour
{
    [Tooltip("任务储存（ScriptableObject），配置本面板要显示的任务，拖到这里")]
    public TaskDataStorage taskStorage;

    [Tooltip("任务文本组件数组，按顺序对应任务储存里的任务")]
    public Text[] taskText;

    [Tooltip("任务完成后文本变成的颜色")]
    public Color completedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Tooltip("终极任务完成后显示的恭喜面板（留空则不显示）")]
    public GameObject congratulationsPanel;

    // 缓存每个文本组件的原始颜色
    private Color[] originalColors;

    // 恭喜面板是否已显示（避免重复弹出）
    private bool congratulationShown = false;

    void Awake()
    {
        CacheOriginalColors();
        if (congratulationsPanel != null)
            congratulationsPanel.SetActive(false);
        Hide();
    }

    void OnEnable()
    {
        // 订阅任务状态变化事件，任务完成时自动刷新
        PlayerTaskManager.OnTaskStateChanged += HandleTaskStateChanged;
    }

    void OnDisable()
    {
        PlayerTaskManager.OnTaskStateChanged -= HandleTaskStateChanged;
    }

    /// <summary>缓存每个文本组件的原始颜色（用文本自身设置的颜色）</summary>
    private void CacheOriginalColors()
    {
        if (taskText == null) return;
        if (originalColors == null || originalColors.Length != taskText.Length)
            originalColors = new Color[taskText.Length];

        for (int i = 0; i < taskText.Length; i++)
        {
            if (taskText[i] != null)
                originalColors[i] = taskText[i].color;
        }
    }

    /// <summary>任务状态变化回调（接受任务 / 完成任务都会触发）</summary>
    private void HandleTaskStateChanged(int taskId)
    {
        // 接受新任务（-1）：重置恭喜面板状态
        if (taskId == -1)
        {
            congratulationShown = false;
            if (congratulationsPanel != null)
                congratulationsPanel.SetActive(false);
        }

        if (gameObject.activeSelf)
            RenderTasks();
    }

    /// <summary>显示任务信息面板，并把任务储存中的任务渲染到文本组件</summary>
    public void Show()
    {
        // 配置检查，给出明确提示
        if (taskStorage == null)
        {
            Debug.LogWarning("[TaskInfoPanel] taskStorage 为空，请把任务储存资产拖到面板的 taskStorage 字段！");
        }
        if (taskText == null || taskText.Length == 0)
        {
            Debug.LogWarning("[TaskInfoPanel] taskText 数组为空，请把任务文本组件拖到面板的 taskText 字段！");
        }
        if (PlayerTaskManager.Instance == null)
        {
            Debug.LogWarning("[TaskInfoPanel] 场景中未找到 PlayerTaskManager，请确认玩家身上挂了该脚本！");
        }

        gameObject.SetActive(true);
        RenderTasks();
    }

    /// <summary>隐藏任务信息面板</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>把任务储存中的任务文字渲染到对应文本组件，已完成任务变灰，多余文本隐藏</summary>
    private void RenderTasks()
    {
        if (taskText == null || taskText.Length == 0) return;

        // 任务储存为空：不改动文本，避免把内容清空
        if (taskStorage == null)
        {
            Debug.LogWarning("[TaskInfoPanel] RenderTasks 时 taskStorage 为空，跳过渲染。");
            return;
        }

        TaskDataStorage storage = taskStorage;
        int taskCount = storage.TaskCount;

        bool finalCompleted = PlayerTaskManager.Instance != null
            && PlayerTaskManager.Instance.IsTaskCompleted(storage.finalTaskId);
        if (!finalCompleted)
        {
            congratulationShown = false;
            if (congratulationsPanel != null)
                congratulationsPanel.SetActive(false);
        }

        // 确保原始颜色已缓存（兜底，防止事件回调时还没缓存）
        if (originalColors == null)
            CacheOriginalColors();

        // 判断所有小任务是否都已完成
        bool allCompleted = AreAllTasksCompleted(storage, taskCount);

        if (allCompleted)
        {
            // 小任务全部完成：隐藏所有小任务文本，用第一个文本显示终极任务
            bool hasFinalTask = !string.IsNullOrEmpty(storage.finalTaskText);

            // 终极任务是否已完成（与小任务相同逻辑，按 finalTaskId 查询）
            // 终极任务完成 → 显示恭喜面板（只弹一次）
            if (finalCompleted && !congratulationShown)
            {
                congratulationShown = true;
                if (congratulationsPanel != null)
                    congratulationsPanel.SetActive(true);
            }
            else if (!finalCompleted && congratulationsPanel != null)
            {
                // 终极任务未完成时确保恭喜面板隐藏
                congratulationsPanel.SetActive(false);
            }

            for (int i = 0; i < taskText.Length; i++)
            {
                if (taskText[i] == null) continue;

                if (i == 0 && hasFinalTask)
                {
                    taskText[i].text = storage.finalTaskText;
                    // 未完成：原始颜色；已完成：变灰
                    taskText[i].color = finalCompleted
                        ? completedColor
                        : originalColors[i];
                    taskText[i].gameObject.SetActive(true);
                }
                else
                {
                    // 小任务文本删除（清空并隐藏）
                    taskText[i].text = "";
                    taskText[i].gameObject.SetActive(false);
                }
            }
            return;
        }

        // 还有小任务未完成：正常渲染小任务
        for (int i = 0; i < taskText.Length; i++)
        {
            if (taskText[i] == null) continue;

            if (i < taskCount)
            {
                int taskId = storage.GetTaskId(i);
                bool completed = PlayerTaskManager.Instance != null
                    && PlayerTaskManager.Instance.IsTaskCompleted(taskId);

                taskText[i].text = storage.GetTaskText(i);
                // 未完成：恢复文本自身的原始颜色；已完成：变灰
                taskText[i].color = completed
                    ? completedColor
                    : originalColors[i];
                taskText[i].gameObject.SetActive(true);
            }
            else
            {
                // 任务数量不足，多余的文本组件隐藏
                taskText[i].text = "";
                taskText[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>判断任务储存里的所有小任务是否都已完成</summary>
    private bool AreAllTasksCompleted(TaskDataStorage storage, int taskCount)
    {
        if (taskCount == 0) return false;
        if (PlayerTaskManager.Instance == null) return false;

        for (int i = 0; i < taskCount; i++)
        {
            int taskId = storage.GetTaskId(i);
            if (!PlayerTaskManager.Instance.IsTaskCompleted(taskId))
                return false;
        }
        return true;
    }
}
