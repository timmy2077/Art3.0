using System;

/// <summary>
/// 农耕关卡（FarmingPuzzle）特有的文字字段。
/// 通用字段（opening/accepted/puzzle）已在基类 LevelTextBase 中定义，此处继承即可。
/// 对应 JSON：Assets/Project/Resources/LevelText/farming.json
/// 改文字直接编辑 JSON 即可，无需改代码、无需重新编译。
/// </summary>

[Serializable]
public class HintsText
{
    public string notAcceptedIntro;
    public string notAcceptedFarmerMet;
    public string brickSolved;
    public string hasMissingPiece;
    public string objectivesCompletedInRange;
    public string objectivesCompletedFar;
    public string takeTool;
    public string whiteCowFound;
    public string blackCowFound;
    public string defaultHint;
}

[Serializable]
public class InteractText
{
    public string notAcceptedBlock;
    public string toolPickupComplete;
    public string restoreBrickComplete;
}

[Serializable]
public class CowText
{
    public string whiteHerded;
    public string blackHerded;
    public string resetCows;
}

[Serializable]
public class FarmingPuzzleText : LevelTextBase
{
    public HintsText hints;
    public InteractText interact;
    public CowText cow;
}
