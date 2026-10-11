using System.Collections.Generic;
using UnityEngine;

///=============================================================================
/// 任务储存（ScriptableObject）
/// 以列表形式配置任务目标，由玩家身上的 PlayerTaskManager 持有，
/// TaskInfoPanel 读取并渲染到文本组件上。
///
/// 创建方式：Project 窗口右键 → Create → 尘封砖语 → 任务储存
///=============================================================================
[CreateAssetMenu(fileName = "NewTaskStorage", menuName = "尘封砖语/任务储存")]
public class TaskDataStorage : ScriptableObject
{
    [Header("任务标识")]
    [Tooltip("用于保存任务进度的唯一标识。不同任务必须使用不同值。")]
    public string questId = "quest_1";

    [System.Serializable]
    public class TaskEntry
    {
        [Tooltip("任务ID，用于区分是哪个任务（触发器填这个ID来完成对应任务）")]
        public int taskId = 0;

        [Tooltip("任务描述文字，会显示在信息面板对应的文本组件上")]
        [TextArea(2, 5)]
        public string taskText = "新的任务";
    }

    [Header("任务列表")]
    [Tooltip("配置1~3个任务，顺序对应信息面板上的文本组件顺序")]
    public List<TaskEntry> tasks = new List<TaskEntry>();

    [Header("终极任务")]
    [Tooltip("终极任务ID，触发器填这个ID来完成终极任务")]
    public int finalTaskId = 0;

    [Tooltip("所有小任务完成后，信息面板替换显示的终极任务文字（留空则不显示终极任务）")]
    [TextArea(2, 5)]
    public string finalTaskText = "";

    /// <summary>任务数量</summary>
    public int TaskCount => tasks != null ? tasks.Count : 0;

    /// <summary>获取指定索引的任务文字</summary>
    public string GetTaskText(int index)
    {
        if (tasks == null || index < 0 || index >= tasks.Count || tasks[index] == null)
            return "";
        return tasks[index].taskText;
    }

    /// <summary>获取指定索引的任务ID</summary>
    public int GetTaskId(int index)
    {
        if (tasks == null || index < 0 || index >= tasks.Count || tasks[index] == null)
            return -1;
        return tasks[index].taskId;
    }

    public bool ContainsTaskId(int taskId)
    {
        if (tasks == null) return false;

        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i] != null && tasks[i].taskId == taskId)
                return true;
        }
        return false;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(questId))
            Debug.LogWarning($"[TaskDataStorage] {name} 的 questId 不能为空。", this);

        var ids = new HashSet<int>();
        if (tasks != null)
        {
            foreach (TaskEntry task in tasks)
            {
                if (task == null) continue;
                if (!ids.Add(task.taskId))
                    Debug.LogWarning($"[TaskDataStorage] {name} 存在重复任务ID：{task.taskId}", this);
            }
        }

        if (ids.Contains(finalTaskId))
            Debug.LogWarning($"[TaskDataStorage] {name} 的终极任务ID与普通任务ID重复：{finalTaskId}", this);
    }
}
