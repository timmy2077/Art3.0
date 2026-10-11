using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>记录当前任务、目标完成状态，并负责持久化。</summary>
public class PlayerTaskManager : MonoBehaviour
{
    public enum QuestState
    {
        NotAccepted = 0,
        Active = 1,
        ObjectivesCompleted = 2,
        Completed = 3
    }

    public static PlayerTaskManager Instance { get; private set; }
    public static event Action<int> OnTaskStateChanged;

    [Header("存档设置")]
    [Tooltip("关闭后不读取或写入任务存档，适合在编辑器中反复测试任务流程。")]
    [SerializeField] private bool enablePersistence = true;

    private readonly HashSet<int> completedTaskIds = new HashSet<int>();
    private TaskDataStorage activeTaskStorage;
    private QuestState currentState = QuestState.NotAccepted;

    public TaskDataStorage ActiveTaskStorage => activeTaskStorage;
    public QuestState CurrentState => currentState;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RestoreTask(TaskDataStorage storage)
    {
        if (!IsValidStorage(storage)) return;
        activeTaskStorage = storage;
        LoadProgress();
    }

    public void AcceptTask(TaskDataStorage storage)
    {
        if (!IsValidStorage(storage)) return;

        activeTaskStorage = storage;
        LoadProgress();

        if (currentState == QuestState.NotAccepted)
        {
            completedTaskIds.Clear();
            currentState = QuestState.Active;
            SaveProgress();
        }

        OnTaskStateChanged?.Invoke(-1);
    }

    // 保留旧入口，避免已有 Button/Event 引用失效。
    public void AcceptTask()
    {
        if (activeTaskStorage == null)
        {
            Debug.LogWarning("[PlayerTaskManager] 未指定要接取的任务。");
            return;
        }
        AcceptTask(activeTaskStorage);
    }

    public bool CompleteTask(int taskId)
    {
        if (activeTaskStorage == null || currentState == QuestState.NotAccepted)
        {
            Debug.LogWarning($"[PlayerTaskManager] 任务尚未接取，忽略目标 {taskId}。", this);
            return false;
        }

        bool isFinalTask = taskId == activeTaskStorage.finalTaskId;
        if (!isFinalTask && !activeTaskStorage.ContainsTaskId(taskId))
        {
            Debug.LogWarning($"[PlayerTaskManager] 目标 {taskId} 不属于当前任务。", this);
            return false;
        }

        if (isFinalTask && !AreAllObjectivesCompleted())
        {
            Debug.LogWarning("[PlayerTaskManager] 普通目标尚未全部完成，不能完成终极任务。", this);
            return false;
        }

        if (!completedTaskIds.Add(taskId)) return false;

        currentState = isFinalTask
            ? QuestState.Completed
            : (AreAllObjectivesCompleted() ? QuestState.ObjectivesCompleted : QuestState.Active);

        SaveProgress();
        Debug.Log($"[PlayerTaskManager] 目标已完成，taskId = {taskId}");
        OnTaskStateChanged?.Invoke(taskId);
        return true;
    }

    public bool IsTaskCompleted(int taskId)
    {
        return completedTaskIds.Contains(taskId);
    }

    public bool IsQuestAccepted(TaskDataStorage storage)
    {
        return ReadSavedState(storage) != QuestState.NotAccepted;
    }

    public bool IsQuestCompleted(TaskDataStorage storage)
    {
        return ReadSavedState(storage) == QuestState.Completed;
    }

    private bool AreAllObjectivesCompleted()
    {
        if (activeTaskStorage == null || activeTaskStorage.TaskCount == 0) return false;

        for (int i = 0; i < activeTaskStorage.TaskCount; i++)
        {
            if (!completedTaskIds.Contains(activeTaskStorage.GetTaskId(i)))
                return false;
        }
        return true;
    }

    private void LoadProgress()
    {
        completedTaskIds.Clear();
        currentState = ReadSavedState(activeTaskStorage);

        if (!enablePersistence)
            return;

        string savedIds = PlayerPrefs.GetString(CompletedIdsKey(activeTaskStorage), "");
        foreach (string value in savedIds.Split(','))
        {
            if (int.TryParse(value, out int taskId))
                completedTaskIds.Add(taskId);
        }
    }

    private void SaveProgress()
    {
        if (!enablePersistence)
            return;

        PlayerPrefs.SetInt(StateKey(activeTaskStorage), (int)currentState);
        PlayerPrefs.SetString(CompletedIdsKey(activeTaskStorage), string.Join(",", completedTaskIds));
        PlayerPrefs.Save();
    }

    private QuestState ReadSavedState(TaskDataStorage storage)
    {
        if (!IsValidStorage(storage)) return QuestState.NotAccepted;
        if (!enablePersistence) return QuestState.NotAccepted;
        return (QuestState)PlayerPrefs.GetInt(StateKey(storage), (int)QuestState.NotAccepted);
    }

    private static bool IsValidStorage(TaskDataStorage storage)
    {
        if (storage != null && !string.IsNullOrWhiteSpace(storage.questId)) return true;
        Debug.LogWarning("[PlayerTaskManager] 任务配置为空或 questId 未设置。");
        return false;
    }

    private static string StateKey(TaskDataStorage storage) => $"Quest.{storage.questId}.State";
    private static string CompletedIdsKey(TaskDataStorage storage) => $"Quest.{storage.questId}.CompletedIds";
}
