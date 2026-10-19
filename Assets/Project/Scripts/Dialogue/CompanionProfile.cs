using UnityEngine;

[CreateAssetMenu(fileName = "CompanionProfile", menuName = "尘封砖语/人物档案")]
public class CompanionProfile : ScriptableObject
{
    public string displayName;
    public Sprite portrait;
    [Min(1), Tooltip("通关此关后相逢，沿用当前项目解锁下一关的进度判定。")]
    public int completedLevel = 1;
    [TextArea(3, 10)] public string identity;
    [TextArea(3, 14)] public string sharedExperience;
    [Tooltip("关联的对话数据；回顾只展示已读完的句子，不提前展示未读分支。")]
    public DialogueData[] relatedDialogues;
}
