using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Chapter one: observe, herd into one shared pen, retrieve, reassemble.</summary>
public sealed class FarmingPuzzle : MonoBehaviour
{
    public enum Interaction { None, TakeTool, RestoreBrick }
    public PlayerMovement player;
    public PlayerTaskManager tasks;
    public TaskDataStorage storage;
    public CowPen whitePen, blackPen;
    public Transform tool, farmer;
    public Font font;
    private Sprite[] brickPieces;
    public Sprite restoredBrick;
    public NextLevelButton nextLevel;
    public bool ModalOpen => modal != null && modal.activeSelf;
    public bool BrickSolved { get; private set; }
    public bool HasMissingPiece => tasks.IsTaskCompleted(storage.finalTaskId);
    public int MissingPieceIndex { get; private set; }
    public string CurrentHint { get; private set; }
    public int[] PieceOrder => (int[])pieceOrder.Clone();
    public bool Accepted => tasks.CurrentState != PlayerTaskManager.QuestState.NotAccepted;
    private readonly int[] pieceOrder = new int[9];
    private GameObject completionActions;
    private Image[] pieceImages;
    private Button[] pieceButtons;
    private Text[] missingLabels;
    private int selectedPiece = -1;
    private GameObject modal, puzzleContent, victoryContent;
    private Text noticeText, puzzleHint, puzzleStory, puzzleEntry, farmerName, toolName;
    private RectTransform canvasRect;
    private Camera fieldCamera;
    private readonly Queue<(string message, float seconds)> notices = new Queue<(string, float)>();
    private float noticeUntil, interactionReadyAt;
    private bool wasAccepted, farmerMet;
    private string activeNotice = "";
    private string SaveKey(string suffix) => "Quest." + storage.questId + ".Puzzle." + suffix;

    private void Start()
    {
        if (DataSystem.instance != null && DataSystem.instance.enablePersistence)
        {
            SaveManager.Instance.SetString("Game.ResumeScene", "Level1");
            SaveManager.Instance.SetInt("Game.Started", 1);
            SaveManager.Instance.Save();
        }
        RestoreProgress();
        PreparePieces();
        BuildUI();
        foreach (DialogueManager dialogue in FindObjectsOfType<DialogueManager>(true)) ConfigureDialogueUI(dialogue);
        ShowOpening();
        RefreshPieces();
        foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
            if (label.text == "遗落的耕具") label.gameObject.SetActive(false);
    }
    public void ConfigureDialogueUI(DialogueManager dialogue)
    {
            if (dialogue.dialogueUI.interactionHint != null)
            {
                GameObject legacyHint = dialogue.dialogueUI.interactionHint;
                HideLegacyOverlay(legacyHint);
                if (dialogue.dialogueUI.continueHint == legacyHint) dialogue.dialogueUI.continueHint = null;
                dialogue.dialogueUI.interactionHint = null;
            }
            ConfigureTaskPanel(dialogue.taskInfoPanel);
            // Restoration now owns the completion screen.
            if (dialogue.taskInfoPanel.congratulationsPanel != null)
                dialogue.taskInfoPanel.congratulationsPanel.SetActive(false);
            dialogue.taskInfoPanel.congratulationsPanel = null;
    }
    public void RestoreProgress()
    {
        tasks.RestoreTask(storage);
        whitePen.Restore(tasks.IsTaskCompleted(1));
        blackPen.Restore(tasks.IsTaskCompleted(2));
        tool.gameObject.SetActive(!tasks.IsTaskCompleted(3));
        // Older completed saves already include the restored brick.
        BrickSolved = tasks.PersistenceEnabled && SaveManager.Instance.GetInt(SaveKey("Solved"), HasMissingPiece ? 1 : 0) == 1;
        MissingPieceIndex = tasks.PersistenceEnabled ? SaveManager.Instance.GetInt(SaveKey("MissingPiece"), -1) : -1;
        if (MissingPieceIndex < 0 || MissingPieceIndex >= 9)
        {
            MissingPieceIndex = Random.Range(0, 9);
            SavePuzzleValue("MissingPiece", MissingPieceIndex);
        }
        wasAccepted = Accepted;
        farmerMet = Accepted;
        notices.Clear(); noticeUntil = 0; activeNotice = ""; selectedPiece = -1;
        if (pieceImages != null) { ShowOpening(); RefreshPieces(); }
    }
    private void ShowOpening()
    {
        if (!Accepted) Notice("你意外闯入画像砖中的田野。完成这里的任务，找回缺失的拼图，复原这块画像砖。\n先找到头顶标有「耕民」的人，靠近后按 E 交谈。WASD / 方向键移动。", 12f);
    }
    private static void HideLegacyOverlay(GameObject target) {
        CanvasGroup group = target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
        group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
        target.SetActive(false);
    }
    private void ConfigureTaskPanel(TaskInfoPanel panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        Place(rect, new Vector2(0, 1), new Vector2(225, -385), new Vector2(390, 250));
        Image background = panel.GetComponent<Image>();
        if (background != null) { background.color = new Color(0.96f, 0.89f, 0.75f, 0.94f); background.raycastTarget = false; }
        for (int i = 0; i < panel.taskText.Length; i++)
        {
            Text text = panel.taskText[i];
            Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -47-i*76), new Vector2(350, 72));
            text.font = font; text.fontSize = 28; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.19f, 0.12f, 0.07f); text.raycastTarget = false;
        }
    }
    private void Update()
    {
        if (ModalOpen) { noticeUntil += Time.unscaledDeltaTime; if (Input.GetKeyDown(KeyCode.Escape)) CloseModal(); return; }
        bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        if (Accepted && !wasAccepted)
            Notice("已接到耕民的任务。请完成它们，再向耕民领取缺失的拼图。", 7f);
        wasAccepted = Accepted;
        if (Accepted && !talking)
        {
            RecordArrival(whitePen);
            RecordArrival(blackPen);
        }
        Interaction nearby = talking ? Interaction.None : FindNearby();
        CurrentHint = HintFor(nearby);
        if (talking) { noticeUntil += Time.unscaledDeltaTime; noticeText.text = ""; }
        else
        {
            if (Time.unscaledTime >= noticeUntil && notices.Count > 0)
            {
                var notice = notices.Dequeue();
                ShowNotice(notice.message, notice.seconds);
            }
            noticeText.text = Time.unscaledTime < noticeUntil ? activeNotice : CurrentHint;
        }
        if (!talking && Time.unscaledTime >= interactionReadyAt && Input.GetKeyDown(KeyCode.E)) TryInteract(nearby);
        if (!talking && Accepted && Input.GetKeyDown(KeyCode.R)) ResetLooseCows();
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
    private void LateUpdate()
    {
        if (fieldCamera == null)
            foreach (Camera candidate in Camera.allCameras)
                if (candidate.gameObject.scene == gameObject.scene && candidate.targetTexture == null &&
                    (fieldCamera == null || candidate.depth > fieldCamera.depth)) fieldCamera = candidate;
        Camera camera = fieldCamera;
        if (camera == null || farmerName == null) return;
        // The portrait textures contain large transparent margins; the body collider follows the visible person.
        CapsuleCollider2D body = farmer.GetComponent<CapsuleCollider2D>();
        Vector3 head = body != null ? new Vector3(body.bounds.center.x, body.bounds.max.y, farmer.position.z) : farmer.position + Vector3.up * 2;
        PositionWorldLabel(farmerName, head, camera, true);
        SpriteRenderer toolSprite = tool.GetComponent<SpriteRenderer>();
        Vector3 toolHead = toolSprite != null ? new Vector3(toolSprite.bounds.center.x, toolSprite.bounds.max.y, tool.position.z) : tool.position;
        PositionWorldLabel(toolName, toolHead, camera, tool.gameObject.activeSelf);
    }
    private void PositionWorldLabel(Text label, Vector3 head, Camera camera, bool available)
    {
        Vector3 screen = camera.WorldToScreenPoint(head);
        bool visible = available && !ModalOpen && screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height;
        label.gameObject.SetActive(visible);
        if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            label.rectTransform.anchoredPosition = local + Vector2.up * 34;
    }
    private string HintFor(Interaction action)
    {
        if (!Accepted) return farmerMet ? "继续与耕民交谈，选择接受他的任务。" : "找到头顶标有「耕民」的人，靠近后按 E 交谈。";
        if (BrickSolved) return "画像砖已复原。打开右上角「拼图」查看砖画，前往下一砖。";
        if (HasMissingPiece) return "已获得缺失的拼图！点击右上角「拼图」，开始复原画像砖。";
        if (tasks.CurrentState == PlayerTaskManager.QuestState.ObjectivesCompleted)
            return FarmerInRange() ? "按 E 向耕民交付任务，领取缺失的拼图。" : "任务已全部完成。回到耕民身边，按 E 交付，领取缺失的拼图。";
        switch (action)
        {
            case Interaction.TakeTool: return "E  拾起遗落的耕具";
        }
        CowPen nearest = null;
        float closest = 5f;
        foreach (CowPen pen in new[] { whitePen, blackPen })
        {
            float distance = Vector2.Distance(player.transform.position, pen.cow.transform.position);
            if (!pen.Captured && distance < closest) { nearest = pen; closest = distance; }
        }
        if (nearest != null) return nearest == whitePen ? "发现白牛！请将它赶回牛栏。" : "发现黑牛！请将它赶回牛栏。";
        return "请完成耕民交付的任务，完成后回去领取缺失的拼图。";
    }
    public bool TryInteract(Interaction action)
    {
        if (ModalOpen || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)) return false;
        if (action == Interaction.None || action != FindNearby()) return false;
        if (!Accepted) { Notice("先问问耕民，他似乎正为春耕发愁。"); return false; }
        switch (action)
        {
            case Interaction.TakeTool:
                if (!tasks.CompleteTask(3)) return false;
                tool.gameObject.SetActive(false);
                Notice("「寻回遗落的耕具」任务已完成。"); return true;
            case Interaction.RestoreBrick:
                if (tasks.CurrentState < PlayerTaskManager.QuestState.ObjectivesCompleted) return false;
                if (HasMissingPiece) return false;
                SavePuzzleValue("Solved", 0);
                if (!tasks.CompleteTask(storage.finalTaskId)) return false;
                Notice("交付任务已完成，获得缺失的拼图！\n拼图功能已解锁，点击右上角「拼图」复原画像砖。", 8f);
                RefreshPieces(); return true;
        }
        return false;
    }
    public bool RecordArrival(CowPen pen)
    {
        if (!Accepted || pen.Captured || !pen.CowFullyInside()) return false;
        if (!pen.TryRecordArrival()) return false;
        tasks.CompleteTask(pen.objectiveId);
        Notice(pen == whitePen ? "白牛归栏，赶白牛任务已完成。" : "黑牛归栏，赶黑牛任务已完成。");
        return true;
    }
    public void ResetLooseCows()
    {
        whitePen.ResetLooseCow(); blackPen.ResetLooseCow();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.velocity = Vector2.zero; body.position = new Vector2(3.3f, 0f);
        Notice("未归栏的牛已回到田边，已完成的任务保留。换个方向再试试。");
    }
    public void Notice(string message, float seconds = 5f)
    {
        if (Time.unscaledTime < noticeUntil) { notices.Enqueue((message, seconds)); return; }
        ShowNotice(message, seconds);
    }
    public void OnFarmerDialogueStarted()
    {
        farmerMet = true;
        activeNotice = "";
        noticeUntil = 0;
        notices.Clear();
    }
    private void ShowNotice(string message, float seconds)
    {
        activeNotice = message;
        if (noticeText != null) noticeText.text = message;
        noticeUntil = Time.unscaledTime + seconds;
    }
    public void OpenRestoration()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return;
        modal.SetActive(true); player.InputLocked = true;
        player.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        puzzleContent.SetActive(!BrickSolved); victoryContent.SetActive(BrickSolved); RefreshPieces();
    }
    public void CloseModal()
    {
        modal.SetActive(false); player.InputLocked = false;
        selectedPiece = -1;
        interactionReadyAt = Time.unscaledTime + 0.2f;
    }
    public void SelectPiece(int index)
    {
        if (!ModalOpen || !HasMissingPiece || BrickSolved || index < 0 || index >= pieceOrder.Length) return;
        if (selectedPiece < 0) selectedPiece = index;
        else
        {
            int value = pieceOrder[selectedPiece]; pieceOrder[selectedPiece] = pieceOrder[index]; pieceOrder[index] = value;
            selectedPiece = -1;
            if (IsArrangementCorrect())
            {
                BrickSolved = true;
                SavePuzzleValue("Solved", 1);
                if (BrickSolved)
                {
                    DataSystem.instance?.UnlockBrick("gengzhong"); DataSystem.instance?.UnlockLevel(2);
                    puzzleContent.SetActive(false); victoryContent.SetActive(true);
                }
            }
        }
        RefreshPieces();
    }
    private void RefreshPieces()
    {
        for (int i = 0; i < pieceImages.Length; i++)
        {
            bool missing = !HasMissingPiece && pieceOrder[i] == MissingPieceIndex;
            pieceImages[i].sprite = missing ? null : brickPieces[pieceOrder[i]];
            pieceImages[i].color = missing ? new Color(0.27f, 0.22f, 0.16f, 0.25f) : selectedPiece == i ? new Color(1f, 0.76f, 0.5f) : Color.white;
            pieceButtons[i].interactable = HasMissingPiece && !BrickSolved;
            missingLabels[i].gameObject.SetActive(missing);
        }
        puzzleStory.text = HasMissingPiece ? "缺失的拼图已找回。将砖画接续，让断开的农耕图景重现。" : "这幅砖画遗失了一块残片，完成耕民的任务可以找回它。";
        puzzleHint.text = !HasMissingPiece ? "拼图少了一块，拼图功能已锁住。\n请找到缺失的那块，解锁拼图功能。" : selectedPiece < 0 ? "点击两块残片可交换位置，复原完整的画像砖。" : "再点击一块残片，与选中的残片交换。";
        puzzleEntry.text = BrickSolved ? "拼图 · 已复原" : HasMissingPiece ? "拼图 · 已解锁" : "拼图 · 缺失一块";
    }
    private void SavePuzzleValue(string suffix, int value)
    {
        if (!tasks.PersistenceEnabled) return;
        SaveManager.Instance.SetInt(SaveKey(suffix), value);
        SaveManager.Instance.Save();
    }
    public void ContinueChapter()
    {
        if (!BrickSolved) return;
        CloseModal(); nextLevel.UnlockAndShowNextLevel();
    }
    private void BuildUI()
    {
        GameObject ui = new GameObject("FarmingPuzzleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; ui.GetComponent<Canvas>().sortingOrder = 80;
        CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasRect = ui.GetComponent<RectTransform>();
        noticeText = Label(ui.transform, "Discovery", "", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 145), 32);
        noticeText.gameObject.AddComponent<Outline>().effectColor = new Color(0.98f, 0.92f, 0.8f);
        farmerName = Label(ui.transform, "FarmerName", "耕民", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(126, 48), 32);
        farmerName.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.94f, 0.8f);
        toolName = Label(ui.transform, "ToolName", "遗落的耕具", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 48), 32);
        toolName.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.94f, 0.8f);
        MakeButton(ui.transform, "拼图", new Vector2(-210, -55), OpenRestoration, 320);
        puzzleEntry = ui.transform.Find("拼图").GetComponent<Text>();
        Place(puzzleEntry.rectTransform, new Vector2(1, 1), new Vector2(-210, -55), new Vector2(340, 66));
        modal = new GameObject("BrickRestoration", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(ui.transform, false);
        RectTransform rect = modal.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        modal.GetComponent<Image>().color = new Color(0.94f, 0.87f, 0.72f, 0.99f);
        puzzleContent = Group("ArrangeFragments");
        Label(puzzleContent.transform, "Title", "田桑稼穑 · 拼合扬场图", new Vector2(0.5f, 0.5f), new Vector2(0, 425), new Vector2(1500, 80), 46);
        puzzleStory = Label(puzzleContent.transform, "Story", "", new Vector2(0.5f, 0.5f), new Vector2(0, 350), new Vector2(1500, 70), 30);
        pieceImages = new Image[9];
        pieceButtons = new Button[9];
        missingLabels = new Text[9];
        float ratio = restoredBrick.rect.width / restoredBrick.rect.height;
        Vector2 paintingSize = new Vector2(Mathf.Min(1120, 570 * ratio), Mathf.Min(570,1120 / ratio));
        Vector2 tile = paintingSize / 3;
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            GameObject slot = new GameObject("FragmentSlot" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(puzzleContent.transform, false);
            Place(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2((i%3-1)*tile.x,(1-i/3)*tile.y), tile-Vector2.one*3);
            slot.GetComponent<Image>().color = new Color(0.65f, 0.51f, 0.33f, 0.12f);
            slot.GetComponent<Image>().raycastTarget = false;
            GameObject part = new GameObject("Fragment" + i, typeof(RectTransform), typeof(Image), typeof(Button)); part.transform.SetParent(puzzleContent.transform, false);
            Place(part.GetComponent<RectTransform>(), new Vector2(0.5f,0.5f), new Vector2((i%3-1)*tile.x,(1-i/3)*tile.y),tile-Vector2.one*3);
            pieceImages[i] = part.GetComponent<Image>();
            pieceButtons[i] = part.GetComponent<Button>();
            pieceButtons[i].transition = Selectable.Transition.None;
            pieceButtons[i].onClick.AddListener(() => SelectPiece(index));
            missingLabels[i] = Label(part.transform, "MissingLabel", "缺失", new Vector2(0.5f, 0.5f), Vector2.zero, tile-Vector2.one*12, 32);
        }
        puzzleHint = Label(puzzleContent.transform, "Hint", "", new Vector2(0.5f, 0.5f), new Vector2(0, -355), new Vector2(1600, 110), 29);
        victoryContent = Group("RestoredBrick");
        Label(victoryContent.transform, "Title", "扬场图 · 砖画已修复", new Vector2(0.5f, 0.5f), new Vector2(0, 375), new Vector2(1500, 80), 46);
        GameObject picture = new GameObject("RestoredPainting", typeof(RectTransform), typeof(Image)); picture.transform.SetParent(victoryContent.transform, false);
        Place(picture.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 55), paintingSize);
        picture.GetComponent<Image>().color = Color.white; picture.GetComponent<Image>().sprite = restoredBrick; picture.GetComponent<Image>().preserveAspect = true;
        completionActions = new GameObject("CompletionActions",typeof(RectTransform)); completionActions.transform.SetParent(victoryContent.transform,false);
        Label(completionActions.transform, "Knowledge", "砖画中的人物与农具，共同组成扬场劳作的图景。\n循着画面线索，让散落的片段重新相连。", new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(1530, 100), 29);
        MakeButton(completionActions.transform, "收录砖画 · 前往下一砖", new Vector2(0, -405), ContinueChapter);
        MakeButton(modal.transform, "返回田野  Esc", new Vector2(680, -455), CloseModal, 300);
        modal.SetActive(false);
    }
    private void PreparePieces()
    {
        brickPieces = new Sprite[9];
        Rect source = restoredBrick.rect;
        for (int i=0;i<9;i++) {
            float x0 = Mathf.Floor(source.width*(i%3)/3), x1 = Mathf.Floor(source.width*(i%3+1)/3);
            float y0 = Mathf.Floor(source.height*(2-i/3)/3), y1 = Mathf.Floor(source.height*(3-i/3)/3);
            brickPieces[i] = Sprite.Create(restoredBrick.texture,new Rect(source.x+x0,source.y+y0,x1-x0,y1-y0),Vector2.one*0.5f,100);
            pieceOrder[i]=i;
        }
        do {
            for(int i=8;i>0;i--) { int j=Random.Range(0,i+1); int value=pieceOrder[i];pieceOrder[i]=pieceOrder[j];pieceOrder[j]=value; }
        } while(IsArrangementCorrect());
    }
    private bool IsArrangementCorrect()
    {
        for(int i=0;i<pieceOrder.Length;i++) if(pieceOrder[i]!=i) return false;
        return true;
    }
    private void OnDestroy()
    {
        if(brickPieces!=null) foreach(Sprite piece in brickPieces) if(piece!=null) Destroy(piece);
    }
    private GameObject Group(string name)
    {
        GameObject group = new GameObject(name, typeof(RectTransform)); group.transform.SetParent(modal.transform, false);
        RectTransform rect = group.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; return group;
    }
    private Text Label(Transform parent, string name, string content, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        Place(obj.GetComponent<RectTransform>(), anchor, position, size);
        Text text = obj.GetComponent<Text>(); text.font = font; text.text = content; text.fontSize = fontSize;
        text.color = new Color(0.19f, 0.12f, 0.07f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
    }
    private void MakeButton(Transform parent, string title, Vector2 position, UnityEngine.Events.UnityAction action, float width = 460)
    {
        Text text = Label(parent, title, title, new Vector2(0.5f, 0.5f), position, new Vector2(width, 66), 29); text.raycastTarget = true;
        Button button = text.gameObject.AddComponent<Button>(); button.targetGraphic = text; button.onClick.AddListener(action);
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}



