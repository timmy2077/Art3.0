using UnityEngine;

///=============================================================================
/// 对话数据（ScriptableObject）
/// 每个 NPC 创建一份对话数据资产，在 Inspector 中填写对话内容即可。
///
/// 创建方式：Project 窗口右键 → Create → 尘封砖语 → 对话数据
///=============================================================================
[CreateAssetMenu(fileName = "NewDialogue", menuName = "尘封砖语/对话数据")]
public class DialogueData : ScriptableObject
{
    [Header("NPC 基本信息")]
    [Tooltip("NPC 的显示名称，会显示在对话面板顶部")]
    public string npcName = "NPC";

    [Tooltip("勾选 = 核心NPC（有任务线/科普线双选项）\n取消 = 次要NPC（纯科普，无选项）")]
    public bool isCoreNPC = true;

    // ------------------------------------------------------------------
    //  核心NPC 对话内容
    // ------------------------------------------------------------------
    [Header("—— 寒暄对话（进入交互后最先播放）——")]
    [Tooltip("走到 NPC 范围按E后，最先播放的寒暄内容，逐条显示，全部播完后弹出选项")]
    [TextArea(3, 8)]
    public string[] greetingLines =
    {
        "看你模样并非本地农人。",
        "如今春耕正忙，田间诸事一团乱，不知你可否愿意出手相助？"
    };

    [Header("—— 任务线 ——")]
    [Tooltip("任务线选项按钮上显示的文字")]
    public string taskChoiceText = "我愿意前来帮忙";

    [Tooltip("选择任务线后播放的对话内容，逐条显示")]
    [TextArea(3, 8)]
    public string[] taskLines =
    {
        "真是太感谢了！前几日一阵风沙吹过，一头耕牛受了惊吓，跑进东侧的桑林里不肯回来。",
        "劳烦你先去桑林把耕牛寻回吧。"
    };

    [Header("—— 科普线 ——")]
    [Tooltip("科普线选项按钮上显示的文字")]
    public string scienceChoiceText = "这片田地有什么来历？";

    [Tooltip("选择科普线后播放的对话内容，逐条显示")]
    [TextArea(3, 8)]
    public string[] scienceLines =
    {
        "这里是河西走廊一带的屯田农田，也是魏晋时期西北地区最主要的粮食产地之一。",
        "我们本地普遍使用二牛抬杠的耕作方式，两头耕牛合力拉犁，效率远超单人独犁。"
    };

    // ------------------------------------------------------------------
    //  次要NPC 对话内容（仅 isCoreNPC = false 时使用）
    // ------------------------------------------------------------------
    [Header("—— 次要NPC 固定科普对话（仅次要NPC使用）——")]
    [Tooltip("次要NPC交互时播放的固定科普内容，无选项，播完即结束")]
    [TextArea(3, 8)]
    public string[] minorNPCLines =
    {
        "春日桑叶鲜嫩，正是采摘养蚕的好时节。",
        "魏晋时期丝绸贸易十分繁盛，河西产出的丝绸远销各地。"
    };
}
