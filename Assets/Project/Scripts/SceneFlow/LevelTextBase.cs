using System;

/// <summary>
/// 所有关卡通用的开场提示文字：一条文字 + 停留秒数。
/// </summary>
[Serializable]
public class OpeningNotice
{
    public string text;
    public float seconds;
}

/// <summary>
/// 所有关卡通用的拼图面板文字。
/// 每关的 JSON 中都有 puzzle 分组，字段名必须与此一致。
/// </summary>
[Serializable]
public class PuzzleText
{
    public string storyWithPiece;
    public string storyWithoutPiece;
    public string hintLocked;
    public string hintUnlocked;
    public string hintSelected;
    public string entrySolved;
    public string entryUnlocked;
    public string entryMissing;
    public string title;
    public string victoryTitle;
    public string victoryKnowledge;
    public string missingLabel;
    public string continueButton;
    public string returnButton;
    public string farmerName;
    public string toolName;
    public string puzzleButton;
}

/// <summary>
/// 关卡文字基类。所有关卡通用的字段（开场、任务接受提示、拼图面板）放在这里。
/// 各关卡的文字类继承本类，再添加本关特有的字段（如农耕关的 hints/interact/cow）。
/// 这样 JSON 中 opening/accepted/puzzle 三组字段对所有关卡通用，其余字段各关自定义。
/// </summary>
[Serializable]
public class LevelTextBase
{
    public OpeningNotice[] opening;
    public string accepted;
    public PuzzleText puzzle;
}
