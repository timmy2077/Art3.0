using UnityEngine;
using UnityEngine.UI;

/// <summary>第一章 · 农耕：观察、赶牛入栏、拾回耕具、复原画像砖。</summary>
public sealed class FarmingPuzzle : LevelBase
{
    public enum Interaction { None, TakeTool, RestoreBrick }

    // ==================== 农耕关特有字段 ====================
    public CowPen whitePen, blackPen;
    public Transform tool, farmer;
    private Text farmerName, toolName;
    private bool farmerMet;

    /// <summary>把基类的 texts 转成本关类型，访问 hints/interact/cow</summary>
    private FarmingPuzzleText T => (FarmingPuzzleText)texts;

    // ==================== 基类抽象成员实现 ====================
    protected override string LevelId => "farming";
    protected override string ResumeSceneName => "Level1";
    protected override string BrickKey => "gengzhong";
    protected override int NextLevelIndex => 2;

    protected override void LoadTexts()
    {
        // 必须显式指定泛型类型为 FarmingPuzzleText，否则 JsonUtility 只反序列化基类字段
        texts = LevelTextLoader.Load<FarmingPuzzleText>("farming", () => (FarmingPuzzleText)BuildDefaultText());
    }

    protected override LevelTextBase BuildDefaultText()
    {
        return new FarmingPuzzleText
        {
            opening = new[]
            {
                new OpeningNotice { text = "你意外闯入画像砖中的田野。完成这里的任务，找回缺失的拼图，复原这块画像砖。", seconds = 5f },
                new OpeningNotice { text = "先找到头顶标有「耕民」的人，靠近后按 E 交谈。", seconds = 4f },
                new OpeningNotice { text = "使用 WASD 或方向键移动。", seconds = 4f }
            },
            accepted = "已接到耕民的任务。请完成它们，再向耕民领取缺失的拼图。",
            hints = new HintsText
            {
                notAcceptedIntro = "找到头顶标有「耕民」的人，靠近后按 E 交谈。",
                notAcceptedFarmerMet = "继续与耕民交谈，选择接受他的任务。",
                brickSolved = "画像砖已复原。打开右上角「拼图」查看砖画，前往下一砖。",
                hasMissingPiece = "已获得缺失的拼图！点击右上角「拼图」，开始复原画像砖。",
                objectivesCompletedInRange = "按 E 向耕民交付任务，领取缺失的拼图。",
                objectivesCompletedFar = "任务已全部完成。回到耕民身边，按 E 交付，领取缺失的拼图。",
                takeTool = "E  拾起遗落的耕具",
                whiteCowFound = "发现白牛。",
                blackCowFound = "发现黑牛。",
                defaultHint = "请完成耕民交付的任务，完成后回去领取缺失的拼图。"
            },
            interact = new InteractText
            {
                notAcceptedBlock = "先问问耕民，他似乎正为春耕发愁。",
                toolPickupComplete = "「寻回遗落的耕具」任务已完成。",
                restoreBrickComplete = "交付任务已完成，获得缺失的拼图！\n拼图功能已解锁，点击右上角「拼图」复原画像砖。"
            },
            cow = new CowText
            {
                whiteHerded = "白牛已被赶入栏中。",
                blackHerded = "黑牛已被赶入栏中。",
                resetCows = "未归栏的牛已回到田边，已完成的任务保留。换个方向再试试。"
            },
            puzzle = new PuzzleText
            {
                storyWithPiece = "缺失的拼图已找回。将砖画接续，让断开的农耕图景重现。",
                storyWithoutPiece = "这幅砖画遗失了一块残片，完成耕民的任务可以找回它。",
                hintLocked = "拼图少了一块，拼图功能已锁住。\n请找到缺失的那块，解锁拼图功能。",
                hintUnlocked = "点击两块残片可交换位置，复原完整的画像砖。",
                hintSelected = "再点击一块残片，与选中的残片交换。",
                entrySolved = "拼图 · 已复原",
                entryUnlocked = "拼图 · 已解锁",
                entryMissing = "拼图 · 缺失一块",
                title = "田桑稼穑 · 拼合扬场图",
                victoryTitle = "扬场图 · 砖画已修复",
                victoryKnowledge = "砖画中的人物与农具，共同组成扬场劳作的图景。\n循着画面线索，让散落的片段重新相连。",
                missingLabel = "缺失",
                continueButton = "收录砖画 · 前往下一砖",
                returnButton = "返回田野  Esc",
                farmerName = "耕民",
                toolName = "遗落的耕具",
                puzzleButton = "拼图"
            }
        };
    }

    // ==================== 存档恢复 ====================
    protected override void RestoreLevelProgress()
    {
        whitePen.Restore(tasks.IsTaskCompleted(1));
        blackPen.Restore(tasks.IsTaskCompleted(2));
        tool.gameObject.SetActive(!tasks.IsTaskCompleted(3));
        farmerMet = Accepted;
        // 隐藏耕具的世界标签（运行时由 BuildLevelUI 重建）
        string toolLabelText = T?.puzzle?.toolName ?? "遗落的耕具";
        foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
            if (label.text == toolLabelText) label.gameObject.SetActive(false);
    }

    // ==================== 每帧逻辑 ====================
    protected override void UpdateLevel()
    {
        bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        if (Accepted && !talking)
        {
            RecordArrival(whitePen);
            RecordArrival(blackPen);
        }
        if (!talking && Accepted && Input.GetKeyDown(KeyCode.R)) ResetLooseCows();
    }

    // ==================== 提示 ====================
    protected override string GetCurrentHint()
    {
        HintsText h = T?.hints;
        if (!Accepted) return farmerMet ? (h?.notAcceptedFarmerMet ?? "继续与耕民交谈，选择接受他的任务。") : (h?.notAcceptedIntro ?? "找到头顶标有「耕民」的人，靠近后按 E 交谈。");
        if (BrickSolved) return h?.brickSolved ?? "画像砖已复原。打开右上角「拼图」查看砖画，前往下一砖。";
        if (HasMissingPiece) return h?.hasMissingPiece ?? "已获得缺失的拼图！点击右上角「拼图」，开始复原画像砖。";
        if (tasks.CurrentState == PlayerTaskManager.QuestState.ObjectivesCompleted)
            return FarmerInRange() ? (h?.objectivesCompletedInRange ?? "按 E 向耕民交付任务，领取缺失的拼图。") : (h?.objectivesCompletedFar ?? "任务已全部完成。回到耕民身边，按 E 交付，领取缺失的拼图。");
        switch (FindNearby())
        {
            case Interaction.TakeTool: return h?.takeTool ?? "E  拾起遗落的耕具";
        }
        CowPen nearest = null;
        float closest = 5f;
        foreach (CowPen pen in new[] { whitePen, blackPen })
        {
            float distance = Vector2.Distance(player.transform.position, pen.cow.transform.position);
            if (!pen.Captured && distance < closest) { nearest = pen; closest = distance; }
        }
        if (nearest != null) return nearest == whitePen ? (h?.whiteCowFound ?? "发现白牛。") : (h?.blackCowFound ?? "发现黑牛。");
        return h?.defaultHint ?? "请完成耕民交付的任务，完成后回去领取缺失的拼图。";
    }

    // ==================== 交互 ====================
    protected override bool TryInteract()
    {
        return TryInteract(FindNearby());
    }

    public bool TryInteract(Interaction action)
    {
        if (ModalOpen || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)) return false;
        if (action == Interaction.None || action != FindNearby()) return false;
        if (!Accepted) { Notice(T?.interact?.notAcceptedBlock ?? "先问问耕民，他似乎正为春耕发愁。"); return false; }
        switch (action)
        {
            case Interaction.TakeTool:
                if (!tasks.CompleteTask(3)) return false;
                tool.gameObject.SetActive(false);
                Notice(T?.interact?.toolPickupComplete ?? "「寻回遗落的耕具」任务已完成。"); return true;
            case Interaction.RestoreBrick:
                if (tasks.CurrentState < PlayerTaskManager.QuestState.ObjectivesCompleted) return false;
                if (HasMissingPiece) return false;
                SavePuzzleValue("Solved", 0);
                if (!tasks.CompleteTask(storage.finalTaskId)) return false;
                Notice(T?.interact?.restoreBrickComplete ?? "交付任务已完成，获得缺失的拼图！\n拼图功能已解锁，点击右上角「拼图」复原画像砖。", 8f);
                RefreshPieces(); return true;
        }
        return false;
    }

    public Interaction FindNearby()
    {
        if (FarmerInRange())
            return Accepted && tasks.CurrentState >= PlayerTaskManager.QuestState.ObjectivesCompleted ? Interaction.RestoreBrick : Interaction.None;
        float closest = 2.3f;
        Interaction result = Interaction.None;
        if (tool.gameObject.activeSelf) Consider(tool, Interaction.TakeTool, ref closest, ref result);
        return result;
    }

    private void Consider(Transform target, Interaction action, ref float closest, ref Interaction result)
    {
        float distance = Vector2.Distance(player.transform.position, target.position);
        if (distance < closest) { closest = distance; result = action; }
    }

    private bool FarmerInRange()
    {
        NPCInteraction npc = farmer.GetComponent<NPCInteraction>();
        return npc != null ? npc.PlayerInRange : Vector2.Distance(player.transform.position, farmer.position) < 2.7f;
    }

    // ==================== 赶牛 ====================
    public bool RecordArrival(CowPen pen)
    {
        if (!Accepted || pen.Captured || !pen.CowFullyInside()) return false;
        if (!pen.TryRecordArrival()) return false;
        tasks.CompleteTask(pen.objectiveId);
        CowText cow = T?.cow;
        Notice(pen == whitePen ? (cow?.whiteHerded ?? "白牛已被赶入栏中。") : (cow?.blackHerded ?? "黑牛已被赶入栏中。"));
        return true;
    }

    public void ResetLooseCows()
    {
        whitePen.ResetLooseCow(); blackPen.ResetLooseCow();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.velocity = Vector2.zero; body.position = new Vector2(3.3f, 0f);
        Notice(T?.cow?.resetCows ?? "未归栏的牛已回到田边，已完成的任务保留。换个方向再试试。");
    }

    // ==================== 对话回调 ====================
    public override void OnDialogueStarted(NPCInteraction npc)
    {
        base.OnDialogueStarted(npc);
        if (npc != null && npc.transform == farmer) farmerMet = true;
    }

    // ==================== 世界标签 ====================
    protected override void PositionWorldLabels(Camera camera)
    {
        if (farmerName == null || farmer == null) return;
        CapsuleCollider2D body = farmer.GetComponent<CapsuleCollider2D>();
        Vector3 head = body != null ? new Vector3(body.bounds.center.x, body.bounds.max.y, farmer.position.z) : farmer.position + Vector3.up * 2;
        PositionWorldLabel(farmerName, head, camera, true);
        SpriteRenderer toolSprite = tool.GetComponent<SpriteRenderer>();
        Vector3 toolHead = toolSprite != null ? new Vector3(toolSprite.bounds.center.x, toolSprite.bounds.max.y, tool.position.z) : tool.position;
        PositionWorldLabel(toolName, toolHead, camera, tool.gameObject.activeSelf);
    }

    // ==================== 关卡 UI ====================
    protected override void BuildLevelUI(Transform canvasTransform)
    {
        PuzzleText p = T?.puzzle;
        farmerName = Label(canvasTransform, "FarmerName", p?.farmerName ?? "耕民", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(126, 48), 32);
        farmerName.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.94f, 0.8f);
        toolName = Label(canvasTransform, "ToolName", p?.toolName ?? "遗落的耕具", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 48), 32);
        toolName.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.94f, 0.8f);
    }
}
