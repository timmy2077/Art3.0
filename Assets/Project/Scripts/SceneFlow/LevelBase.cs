using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 所有关卡的通用基类。
/// 封装了 5 关共用的逻辑：修砖拼图、打字机提示、对话 UI 适配、文字加载、存档、世界标签定位。
/// 子类（如 FarmingPuzzle、CookingPuzzle）只需实现本关特有的交互/提示/物体恢复逻辑。
/// </summary>
public abstract class LevelBase : MonoBehaviour
{
    // ==================== Inspector 配置（所有关卡共用） ====================
    public PlayerMovement player;
    public PlayerTaskManager tasks;
    public TaskDataStorage storage;
    public Font font;
    public Sprite restoredBrick;
    public NextLevelButton nextLevel;

    [Header("提示文字打字机")]
    [Tooltip("提示文字每个字出现的间隔（秒），越小越快")]
    public float noticeTypeInterval = 0.045f;

    // ==================== 公共状态 ====================
    public bool ModalOpen => modal != null && modal.activeSelf;
    public bool BrickSolved { get; protected set; }
    public bool HasMissingPiece => tasks.IsTaskCompleted(storage.finalTaskId);
    public int MissingPieceIndex { get; protected set; }
    public string CurrentHint { get; protected set; }
    public int[] PieceOrder => (int[])pieceOrder.Clone();
    public bool Accepted => tasks.CurrentState != PlayerTaskManager.QuestState.NotAccepted;

    // ==================== 子类需实现的抽象成员 ====================
    /// <summary>关卡 ID，用于从 Resources/LevelText/{id}.json 加载文字</summary>
    protected abstract string LevelId { get; }
    /// <summary>存档中记录的继续游戏场景名，如 "Level1"</summary>
    protected abstract string ResumeSceneName { get; }
    /// <summary>文字配置兜底（JSON 丢失时使用）</summary>
    protected abstract LevelTextBase BuildDefaultText();
    /// <summary>加载本关文字（子类在此调用 LevelTextLoader.Load 并赋值给 texts）</summary>
    protected abstract void LoadTexts();
    /// <summary>恢复本关特有的物体状态（如牛栏、耕具）</summary>
    protected abstract void RestoreLevelProgress();
    /// <summary>每帧本关特有的逻辑（如赶牛检测）</summary>
    protected abstract void UpdateLevel();
    /// <summary>当前顶部提示文字</summary>
    protected abstract string GetCurrentHint();
    /// <summary>处理 E 键交互，返回是否处理</summary>
    protected abstract bool TryInteract();
    /// <summary>定位本关的世界标签（NPC 头顶名等）</summary>
    protected abstract void PositionWorldLabels(Camera camera);
    /// <summary>构建本关特有的 UI（如 NPC 头顶名、道具名）</summary>
    protected abstract void BuildLevelUI(Transform canvasTransform);
    /// <summary>DataSystem 解锁画像砖用的 key，如 "gengzhong"</summary>
    protected abstract string BrickKey { get; }
    /// <summary>通关后解锁的下一关索引，如 2</summary>
    protected abstract int NextLevelIndex { get; }

    // ==================== 文字配置 ====================
    protected LevelTextBase texts;

    // ==================== 私有状态 ====================
    private string SaveKey(string suffix) => "Quest." + storage.questId + ".Puzzle." + suffix;
    private readonly int[] pieceOrder = new int[9];
    private Sprite[] brickPieces;
    private GameObject completionActions;
    private Image[] pieceImages;
    private Button[] pieceButtons;
    private Text[] missingLabels;
    private int selectedPiece = -1;
    private GameObject modal, puzzleContent, victoryContent;
    protected Text noticeText, puzzleHint, puzzleStory, puzzleEntry;
    private RectTransform canvasRect;
    private Camera fieldCamera;
    private readonly Queue<(string message, float seconds)> notices = new Queue<(string, float)>();
    private float noticeUntil, interactionReadyAt;
    private string activeNotice = "";
    private bool wasAccepted;
    private Coroutine noticeTypeCoroutine;
    private bool isTypingNotice;

    // ==================== 生命周期 ====================
    protected virtual void Start()
    {
        LoadTexts();
        if (DataSystem.instance != null && DataSystem.instance.enablePersistence)
        {
            SaveManager.Instance.SetString("Game.ResumeScene", ResumeSceneName);
            SaveManager.Instance.SetInt("Game.Started", 1);
            SaveManager.Instance.Save();
        }
        RestoreProgress();
        PreparePieces();
        BuildUI();
        foreach (DialogueManager dialogue in FindObjectsOfType<DialogueManager>(true)) ConfigureDialogueUI(dialogue);
        ShowOpening();
        RefreshPieces();
    }

    /// <summary>对话开始时调用：清掉打字机提示，避免与对话文字冲突。子类可覆写添加本关逻辑。</summary>
    public virtual void OnDialogueStarted(NPCInteraction npc)
    {
        activeNotice = "";
        noticeUntil = 0;
        notices.Clear();
        if (noticeTypeCoroutine != null) { StopCoroutine(noticeTypeCoroutine); noticeTypeCoroutine = null; }
        isTypingNotice = false;
    }

    // ==================== 存档恢复 ====================
    public void RestoreProgress()
    {
        tasks.RestoreTask(storage);
        BrickSolved = tasks.PersistenceEnabled && SaveManager.Instance.GetInt(SaveKey("Solved"), HasMissingPiece ? 1 : 0) == 1;
        MissingPieceIndex = tasks.PersistenceEnabled ? SaveManager.Instance.GetInt(SaveKey("MissingPiece"), -1) : -1;
        if (MissingPieceIndex < 0 || MissingPieceIndex >= 9)
        {
            MissingPieceIndex = Random.Range(0, 9);
            SavePuzzleValue("MissingPiece", MissingPieceIndex);
        }
        wasAccepted = Accepted;
        notices.Clear(); noticeUntil = 0; activeNotice = ""; selectedPiece = -1;
        RestoreLevelProgress();
        if (pieceImages != null) { ShowOpening(); RefreshPieces(); }
    }

    // ==================== 开场提示 ====================
    protected virtual void ShowOpening()
    {
        if (!Accepted && texts != null && texts.opening != null)
        {
            foreach (OpeningNotice line in texts.opening)
                Notice(line.text, line.seconds);
        }
    }

    // ==================== 对话 UI 适配 ====================
    public void ConfigureDialogueUI(DialogueManager dialogue)
    {
        if (dialogue == null || dialogue.dialogueUI == null) return;

        if (dialogue.dialogueUI.interactionHint != null)
        {
            GameObject legacyHint = dialogue.dialogueUI.interactionHint;
            HideLegacyOverlay(legacyHint);
            if (dialogue.dialogueUI.continueHint == legacyHint) dialogue.dialogueUI.continueHint = null;
            dialogue.dialogueUI.interactionHint = null;
        }

        if (dialogue.taskInfoPanel != null)
        {
            ConfigureTaskPanel(dialogue.taskInfoPanel);
            if (dialogue.taskInfoPanel.congratulationsPanel != null)
                dialogue.taskInfoPanel.congratulationsPanel.SetActive(false);
            dialogue.taskInfoPanel.congratulationsPanel = null;
        }
    }

    private static void HideLegacyOverlay(GameObject target)
    {
        // 不能用 ?? 操作符——Unity 的"假空"组件不会被 CLR 的 ?? 识别
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
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
            if (text == null) continue;
            Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -47 - i * 76), new Vector2(350, 72));
            text.font = font; text.fontSize = 28; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.19f, 0.12f, 0.07f); text.raycastTarget = false;
        }
    }

    // ==================== 每帧更新 ====================
    private void Update()
    {
        if (ModalOpen)
        {
            noticeUntil += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Escape)) CloseModal();
            return;
        }
        bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        if (Accepted && !wasAccepted)
            Notice(texts != null ? texts.accepted : "", 7f);
        wasAccepted = Accepted;

        UpdateLevel();

        CurrentHint = GetCurrentHint();

        if (talking)
        {
            noticeUntil += Time.unscaledDeltaTime;
            noticeText.text = "";
            if (isTypingNotice)
            {
                if (noticeTypeCoroutine != null) { StopCoroutine(noticeTypeCoroutine); noticeTypeCoroutine = null; }
                isTypingNotice = false;
            }
        }
        else
        {
            if (Time.unscaledTime >= noticeUntil && notices.Count > 0)
            {
                var notice = notices.Dequeue();
                ShowNotice(notice.message, notice.seconds);
            }
            if (!isTypingNotice)
                noticeText.text = Time.unscaledTime < noticeUntil ? activeNotice : CurrentHint;
        }

        if (!talking && Time.unscaledTime >= interactionReadyAt && Input.GetKeyDown(KeyCode.E))
            TryInteract();
    }

    private void LateUpdate()
    {
        if (fieldCamera == null)
            foreach (Camera candidate in Camera.allCameras)
                if (candidate.gameObject.scene == gameObject.scene && candidate.targetTexture == null &&
                    (fieldCamera == null || candidate.depth > fieldCamera.depth)) fieldCamera = candidate;
        if (fieldCamera != null)
            PositionWorldLabels(fieldCamera);
    }

    /// <summary>把世界坐标的标签定位到屏幕对应位置（供子类 PositionWorldLabels 调用）</summary>
    protected void PositionWorldLabel(Text label, Vector3 head, Camera camera, bool available)
    {
        if (label == null) return;
        Vector3 screen = camera.WorldToScreenPoint(head);
        bool visible = available && !ModalOpen && screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height;
        label.gameObject.SetActive(visible);
        if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            label.rectTransform.anchoredPosition = local + Vector2.up * 34;
    }

    // ==================== 打字机提示 ====================
    public void Notice(string message, float seconds = 5f)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (Time.unscaledTime < noticeUntil) { notices.Enqueue((message, seconds)); return; }
        ShowNotice(message, seconds);
    }

    private void ShowNotice(string message, float seconds)
    {
        activeNotice = message;
        if (noticeTypeCoroutine != null) { StopCoroutine(noticeTypeCoroutine); noticeTypeCoroutine = null; }
        isTypingNotice = false;

        if (noticeText == null)
        {
            noticeUntil = Time.unscaledTime + seconds;
            return;
        }
        noticeUntil = float.MaxValue;
        noticeTypeCoroutine = StartCoroutine(TypeNotice(message, seconds));
    }

    private IEnumerator TypeNotice(string message, float seconds)
    {
        isTypingNotice = true;
        noticeText.text = "";
        foreach (char c in message)
        {
            if (noticeText == null) break;
            noticeText.text += c;
            yield return new WaitForSecondsRealtime(noticeTypeInterval);
        }
        isTypingNotice = false;
        noticeTypeCoroutine = null;
        noticeUntil = Time.unscaledTime + seconds;
    }

    // ==================== 拼图面板 ====================
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
                DataSystem.instance?.UnlockBrick(BrickKey);
                DataSystem.instance?.UnlockLevel(NextLevelIndex);
                if (nextLevel != null) nextLevel.GrantBrickRewards();
                puzzleContent.SetActive(false); victoryContent.SetActive(true);
            }
        }
        RefreshPieces();
    }

    protected void RefreshPieces()
    {
        for (int i = 0; i < pieceImages.Length; i++)
        {
            bool missing = !HasMissingPiece && pieceOrder[i] == MissingPieceIndex;
            pieceImages[i].sprite = missing ? null : brickPieces[pieceOrder[i]];
            pieceImages[i].color = missing ? new Color(0.27f, 0.22f, 0.16f, 0.25f) : selectedPiece == i ? new Color(1f, 0.76f, 0.5f) : Color.white;
            pieceButtons[i].interactable = HasMissingPiece && !BrickSolved;
            missingLabels[i].gameObject.SetActive(missing);
        }
        PuzzleText p = texts != null ? texts.puzzle : null;
        puzzleStory.text = HasMissingPiece ? (p?.storyWithPiece ?? "") : (p?.storyWithoutPiece ?? "");
        puzzleHint.text = !HasMissingPiece ? (p?.hintLocked ?? "") : selectedPiece < 0 ? (p?.hintUnlocked ?? "") : (p?.hintSelected ?? "");
        puzzleEntry.text = BrickSolved ? (p?.entrySolved ?? "") : HasMissingPiece ? (p?.entryUnlocked ?? "") : (p?.entryMissing ?? "");
    }

    protected void SavePuzzleValue(string suffix, int value)
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

    // ==================== UI 构建 ====================
    private void BuildUI()
    {
        GameObject ui = new GameObject("LevelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; ui.GetComponent<Canvas>().sortingOrder = 80;
        CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasRect = ui.GetComponent<RectTransform>();

        noticeText = Label(ui.transform, "Discovery", "", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 145), 32);
        noticeText.gameObject.AddComponent<Outline>().effectColor = new Color(0.98f, 0.92f, 0.8f);

        PuzzleText p = texts != null ? texts.puzzle : null;
        MakeButton(ui.transform, "PuzzleButton", p?.puzzleButton ?? "拼图", new Vector2(-210, -55), OpenRestoration, 320);
        puzzleEntry = ui.transform.Find("PuzzleButton").GetComponent<Text>();
        Place(puzzleEntry.rectTransform, new Vector2(1, 1), new Vector2(-210, -55), new Vector2(340, 66));

        modal = new GameObject("BrickRestoration", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(ui.transform, false);
        RectTransform rect = modal.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        modal.GetComponent<Image>().color = new Color(0.94f, 0.87f, 0.72f, 0.99f);

        puzzleContent = Group("ArrangeFragments");
        Label(puzzleContent.transform, "Title", p?.title ?? "", new Vector2(0.5f, 0.5f), new Vector2(0, 425), new Vector2(1500, 80), 46);
        puzzleStory = Label(puzzleContent.transform, "Story", "", new Vector2(0.5f, 0.5f), new Vector2(0, 350), new Vector2(1500, 70), 30);

        pieceImages = new Image[9];
        pieceButtons = new Button[9];
        missingLabels = new Text[9];
        float ratio = restoredBrick.rect.width / restoredBrick.rect.height;
        Vector2 paintingSize = new Vector2(Mathf.Min(1120, 570 * ratio), Mathf.Min(570, 1120 / ratio));
        Vector2 tile = paintingSize / 3;
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            GameObject slot = new GameObject("FragmentSlot" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(puzzleContent.transform, false);
            Place(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2((i % 3 - 1) * tile.x, (1 - i / 3) * tile.y), tile - Vector2.one * 3);
            slot.GetComponent<Image>().color = new Color(0.65f, 0.51f, 0.33f, 0.12f);
            slot.GetComponent<Image>().raycastTarget = false;
            GameObject part = new GameObject("Fragment" + i, typeof(RectTransform), typeof(Image), typeof(Button)); part.transform.SetParent(puzzleContent.transform, false);
            Place(part.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2((i % 3 - 1) * tile.x, (1 - i / 3) * tile.y), tile - Vector2.one * 3);
            pieceImages[i] = part.GetComponent<Image>();
            pieceButtons[i] = part.GetComponent<Button>();
            pieceButtons[i].transition = Selectable.Transition.None;
            pieceButtons[i].onClick.AddListener(() => SelectPiece(index));
            missingLabels[i] = Label(part.transform, "MissingLabel", p?.missingLabel ?? "缺失", new Vector2(0.5f, 0.5f), Vector2.zero, tile - Vector2.one * 12, 32);
        }
        puzzleHint = Label(puzzleContent.transform, "Hint", "", new Vector2(0.5f, 0.5f), new Vector2(0, -355), new Vector2(1600, 110), 29);

        victoryContent = Group("RestoredBrick");
        Label(victoryContent.transform, "Title", p?.victoryTitle ?? "", new Vector2(0.5f, 0.5f), new Vector2(0, 375), new Vector2(1500, 80), 46);
        GameObject picture = new GameObject("RestoredPainting", typeof(RectTransform), typeof(Image)); picture.transform.SetParent(victoryContent.transform, false);
        Place(picture.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 55), paintingSize);
        picture.GetComponent<Image>().color = Color.white; picture.GetComponent<Image>().sprite = restoredBrick; picture.GetComponent<Image>().preserveAspect = true;
        picture.SetActive(false);
        BrickRewardView.Show(victoryContent.transform, nextLevel, new Vector2(1500, 560));
        completionActions = new GameObject("CompletionActions", typeof(RectTransform)); completionActions.transform.SetParent(victoryContent.transform, false);
        Label(completionActions.transform, "Knowledge", p?.victoryKnowledge ?? "", new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(1530, 100), 29);
        MakeButton(completionActions.transform, "ContinueButton", p?.continueButton ?? "收录砖画 · 前往下一砖", new Vector2(0, -405), ContinueChapter);
        MakeButton(modal.transform, "ReturnButton", p?.returnButton ?? "返回 Esc", new Vector2(680, -455), CloseModal, 300);

        BuildLevelUI(ui.transform);

        modal.SetActive(false);
    }

    private void PreparePieces()
    {
        brickPieces = new Sprite[9];
        Rect source = restoredBrick.rect;
        for (int i = 0; i < 9; i++)
        {
            float x0 = Mathf.Floor(source.width * (i % 3) / 3), x1 = Mathf.Floor(source.width * (i % 3 + 1) / 3);
            float y0 = Mathf.Floor(source.height * (2 - i / 3) / 3), y1 = Mathf.Floor(source.height * (3 - i / 3) / 3);
            brickPieces[i] = Sprite.Create(restoredBrick.texture, new Rect(source.x + x0, source.y + y0, x1 - x0, y1 - y0), Vector2.one * 0.5f, 100);
            pieceOrder[i] = i;
        }
        do
        {
            for (int i = 8; i > 0; i--) { int j = Random.Range(0, i + 1); int value = pieceOrder[i]; pieceOrder[i] = pieceOrder[j]; pieceOrder[j] = value; }
        } while (IsArrangementCorrect());
    }

    private bool IsArrangementCorrect()
    {
        for (int i = 0; i < pieceOrder.Length; i++) if (pieceOrder[i] != i) return false;
        return true;
    }

    private void OnDestroy()
    {
        if (brickPieces != null) foreach (Sprite piece in brickPieces) if (piece != null) Destroy(piece);
    }

    // ==================== UI 辅助方法（子类可调用） ====================
    protected GameObject Group(string name)
    {
        GameObject group = new GameObject(name, typeof(RectTransform)); group.transform.SetParent(modal.transform, false);
        RectTransform rect = group.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; return group;
    }

    protected Text Label(Transform parent, string name, string content, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        Place(obj.GetComponent<RectTransform>(), anchor, position, size);
        Text text = obj.GetComponent<Text>(); text.font = font; text.text = content; text.fontSize = fontSize;
        text.color = new Color(0.19f, 0.12f, 0.07f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
    }

    protected void MakeButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action, float width = 460)
    {
        Text text = Label(parent, name, label, new Vector2(0.5f, 0.5f), position, new Vector2(width, 66), 29); text.raycastTarget = true;
        Button button = text.gameObject.AddComponent<Button>(); button.targetGraphic = text; button.onClick.AddListener(action);
    }

    protected static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
