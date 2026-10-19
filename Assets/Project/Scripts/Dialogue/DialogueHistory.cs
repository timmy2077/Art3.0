using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum DialogueSection { Greeting, Task, Science, Minor }

/// <summary>SO 保存原文，玩家已读标记保存到统一存档，不修改 SO 资产。</summary>
public static class DialogueHistory
{
    private static readonly HashSet<string> sessionReads = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        sessionReads.Clear();
        SaveManager.ResetOccurred -= ClearSession;
        SaveManager.ResetOccurred += ClearSession;
    }

    private static void ClearSession() { sessionReads.Clear(); }

    public static string[] Lines(DialogueData data, DialogueSection section)
    {
        if (data == null) return null;
        switch (section)
        {
            case DialogueSection.Greeting: return data.greetingLines;
            case DialogueSection.Task: return data.taskLines;
            case DialogueSection.Science: return data.scienceLines;
            default: return data.minorNPCLines;
        }
    }

    public static string Key(DialogueData data, DialogueSection section, int index)
    {
        string[] lines = Lines(data, section);
        if (data == null || string.IsNullOrWhiteSpace(data.historyId) || lines == null ||
            index < 0 || index >= lines.Length || string.IsNullOrWhiteSpace(lines[index])) return null;
        // 原文变更后不将新文本误认为读过；同名 SO 使用不同 historyId。
        using (SHA256 hash = SHA256.Create())
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(lines[index]));
            return "Dialogue.Read." + data.historyId + "." + section + "." + index + "." +
                System.BitConverter.ToString(bytes).Replace("-", "");
        }
    }

    public static bool HasRead(DialogueData data, DialogueSection section, int index)
    {
        string key = Key(data, section, index);
        if (key == null) return false;
        if (sessionReads.Contains(key)) return true;
        return DataSystem.instance != null && DataSystem.instance.enablePersistence &&
            SaveManager.Instance.GetInt(key, 0) == 1;
    }

    public static void MarkRead(DialogueData data, DialogueSection section, int index)
    {
        string key = Key(data, section, index);
        if (key == null || !sessionReads.Add(key)) return;
        if (DataSystem.instance == null || !DataSystem.instance.enablePersistence) return;
        SaveManager.Instance.SetInt(key, 1);
        SaveManager.Instance.Save();
    }
}
