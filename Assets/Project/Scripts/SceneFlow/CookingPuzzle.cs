using UnityEngine;
using UnityEngine.UI;

/// <summary>第二砖：寻回食器、清理灶台、器物配对、修复庖厨图。</summary>
public sealed class CookingPuzzle : LevelBase
{
    public NPCInteraction chef, guest;
    public CookingRestaurant restaurant;
    public DialogueData[] chefDialogues;
    public Transform[] utensils;
    public Transform stove, banquet;
    public GameObject[] stoveDebris;
    public bool alwaysShowTouchControls;
    private readonly bool[] collected = new bool[3];
    private readonly bool[] placed = new bool[3];
    private readonly string[] utensilNames = { "陶耳杯", "食盘", "烤肉炉" };
    private readonly string[] slotNames = { "置酒案 · 陶耳杯", "食案 · 食盘", "炉台 · 烤肉炉" };
    private bool stoveBriefed, banquetBriefed;
    private int cleared, selected = -1, dialogueStage;
    private float nextAction;
    private GameObject matching;
    private Button[] itemButtons, slotButtons;
    private Text matchingHint;
    private Text[] worldLabels;
    private Transform levelCanvas;
    private bool touchControlsBuilt;
    protected override string LevelId => "cooking";
    protected override string ResumeSceneName => "Level2";
    protected override string BrickKey => "paochu";
    protected override int NextLevelIndex => 3;
    public override bool IsInteractionBlocked => base.IsInteractionBlocked || (matching != null && matching.activeSelf);
    public override bool AllowNPCDialogue(NPCInteraction npc) => !IsInteractionBlocked;
    public bool HasUtensil(int index) => index >= 0 && index < 3 && collected[index];
    public bool IsPlaced(int index) => index >= 0 && index < 3 && placed[index];
    public int ClearedDebris => cleared;
    public bool StoveBriefed => stoveBriefed;
    public bool BanquetBriefed => banquetBriefed;
    public bool IsInsideRestaurant => restaurant == null || restaurant.IsInside;

    protected override void LoadTexts() { texts = LevelTextLoader.Load<LevelTextBase>(LevelId, BuildDefaultText); }
    protected override LevelTextBase BuildDefaultText() => new LevelTextBase
    {
        opening = new[] { new OpeningNotice { text = "庖厨宴饮 · 食礼篇\n靠近饭店后按 E 进入，靠近庖长按 E 交谈。\n手机使用右下角「交互」。", seconds = 5 } },
        accepted = "先在庖厨作坊与传菜廊道寻找陶耳杯、食盘和烤肉炉。",
        puzzle = new PuzzleText { title = "复原庖厨图", storyWithoutPiece = "食器散落、炉道堵塞，砖上的庖厨生活还不完整。\n完成备餐与器物配对，再向庖长领取缺失的砖画碎片。",
            storyWithPiece = "食器已归位，灶台重新通畅。\n交换碎片，复原这幅记录庖厨劳作的砖画。", hintLocked = "先完成庖长的委托，领取缺失碎片。", hintUnlocked = "依次选择两块碎片，交换位置。", hintSelected = "请选择另一块碎片交换。",
            entrySolved = "庖厨图 · 已修复", entryUnlocked = "修复庖厨图", entryMissing = "庖厨图 · 缺片", victoryTitle = "庖厨图 · 修复完成", victoryKnowledge = "从庖厨到厅堂，器物与劳作共同留下千年前的生活图景。", missingLabel = "缺片", continueButton = "收录砖画 · 返回选关", returnButton = "返回宅院 Esc", puzzleButton = "拼图" }
    };
    private string Key(string suffix) => "Quest." + storage.questId + ".Cooking." + suffix;
    private int Read(string suffix, int fallback = 0) => tasks.PersistenceEnabled ? SaveManager.Instance.GetInt(Key(suffix), fallback) : fallback;
    private void Write(string suffix, int value) { if (!tasks.PersistenceEnabled) return; SaveManager.Instance.SetInt(Key(suffix), value); SaveManager.Instance.Save(); }
    protected override void RestoreLevelProgress()
    {
        for (int i = 0; i < 3; i++)
        {
            collected[i] = tasks.IsTaskCompleted(1) || Read("Item" + i) == 1;
            placed[i] = tasks.IsTaskCompleted(3) || Read("Placed" + i) == 1;
            utensils[i].gameObject.SetActive(!collected[i]);
        }
        cleared = tasks.IsTaskCompleted(2) ? 3 : Mathf.Clamp(Read("Cleared"), 0, 3);
        stoveBriefed = Read("StoveBriefed") == 1 || tasks.IsTaskCompleted(2);
        banquetBriefed = Read("BanquetBriefed") == 1 || tasks.IsTaskCompleted(3);
        RefreshDebris();
        if (restaurant != null) restaurant.RestoreArea();
    }
    private void RefreshDebris() { for (int i=0;i<stoveDebris.Length;i++) if (stoveDebris[i] != null) stoveDebris[i].SetActive(i >= cleared); }
    protected override void UpdateLevel()
    {
        if (!touchControlsBuilt && (Application.isMobilePlatform || alwaysShowTouchControls)) BuildTouchControls();
        if (matching != null && matching.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseMatching();
    }
    protected override string GetCurrentHint()
    {
        if (!IsInsideRestaurant) return "靠近饭店按 E 进入，靠近庖长按 E 交谈（手机点击「交互」）。";
        if (!Accepted) return "庖厨作坊：靠近庖长，按 E 交谈（手机点击「交互」）。";
        if (BrickSolved) return "庖厨图已修复，可打开拼图回看成果，或返回选关。";
        if (HasMissingPiece) return "获得缺片！点击右上角「拼图」复原庖厨图。";
        if (!tasks.IsTaskCompleted(1)) return "寻找散落的陶耳杯、食盘、烤肉炉，靠近后按 E 拾取。";
        if (!stoveBriefed) return "器具已找齐，回到庖长身边交谈，了解灶台情况。";
        if (!tasks.IsTaskCompleted(2)) return $"清理灶台炉口：靠近后按 E 移走杂物（{cleared}/3）。";
        if (!banquetBriefed) return "炉道已通畅，与庖长交谈，接取厅堂摆席安排。";
        if (!tasks.IsTaskCompleted(3)) return "前往主宴厅堂，靠近宴席按 E，将三件器物配对归位。";
        return "宴席布置完毕，与庖长交谈，领取砖画缺片。";
    }
    public override void OnDialogueStarted(NPCInteraction npc)
    {
        base.OnDialogueStarted(npc);
        if (npc != chef) return;
        dialogueStage = !Accepted ? 0 : HasMissingPiece ? 5 : tasks.IsTaskCompleted(3) ? 4 : tasks.IsTaskCompleted(2) ? 3 : tasks.IsTaskCompleted(1) ? 2 : 1;
        chef.dialogueData = chefDialogues[dialogueStage];
    }
    public override void OnDialogueFinished(NPCInteraction npc)
    {
        if (npc != chef) return;
        if (dialogueStage == 2) { stoveBriefed = true; Write("StoveBriefed", 1); }
        if (dialogueStage == 3) { banquetBriefed = true; Write("BanquetBriefed", 1); }
        if (dialogueStage == 4 && !HasMissingPiece)
        {
            if (tasks.PersistenceEnabled) { SaveManager.Instance.SetInt("Quest."+storage.questId+".Puzzle.Solved",0); SaveManager.Instance.Save(); }
            if (tasks.CompleteTask(storage.finalTaskId)) { RefreshPieces(); Notice("获得庖厨图缺失碎片！点击「拼图」开始修复。", 7); }
        }
    }
    private bool Near(Transform t) => t != null && t.gameObject.activeInHierarchy && Vector2.Distance(player.transform.position, t.position) <= 2.7f;
    protected override bool TryInteract() => Interact();
    public bool Interact()
    {
        if (DebugUnlockPanel.BlocksGameplayInput || IsInteractionBlocked || Time.unscaledTime < nextAction ||
            (restaurant != null && !restaurant.IsReady) || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)) return false;
        nextAction = Time.unscaledTime + .25f;
        if (restaurant != null && restaurant.TryUseDoor()) return true;
        if (chef != null && Near(chef.transform)) { DialogueManager.Instance.StartDialogue(chef); return true; }
        if (guest != null && Near(guest.transform)) { DialogueManager.Instance.StartDialogue(guest); return true; }
        if (!IsInsideRestaurant) return false;
        if (!Accepted) { Notice("先与庖长交谈，了解需要帮忙的事情。"); return false; }
        for (int i=0;i<3;i++) if (!collected[i] && Near(utensils[i])) return CollectUtensil(i);
        if (Near(stove)) return ClearStove();
        if (Near(banquet)) return OpenMatching();
        return false;
    }
    public bool CollectUtensil(int index)
    {
        if (!IsInsideRestaurant || !Accepted || IsInteractionBlocked || index<0 || index>=3 || collected[index] || !Near(utensils[index])) return false;
        collected[index] = true; Write("Item"+index, 1); utensils[index].gameObject.SetActive(false);
        int count = 0; foreach (bool value in collected) if (value) count++;
        if (count == 3) tasks.CompleteTask(1);
        Notice("已找回" + utensilNames[index] + $"（{count}/3）" + (count==3 ? "，回去向庖长交付。" : "。")); return true;
    }
    public bool ClearStove()
    {
        if (!IsInsideRestaurant || !Accepted || IsInteractionBlocked || !Near(stove) || tasks.IsTaskCompleted(2)) return false;
        if (!tasks.IsTaskCompleted(1) || !stoveBriefed) { Notice("先找齐食器，并向庖长了解清理灶台的安排。"); return false; }
        cleared = Mathf.Min(3,cleared+1); Write("Cleared",cleared); RefreshDebris();
        if (cleared==3) tasks.CompleteTask(2);
        Notice(cleared==3 ? "炉道已通畅，回到庖长身边交谈。" : $"已移走炉口杂物（{cleared}/3）。"); return true;
    }
    public bool OpenMatching()
    {
        if (!IsInsideRestaurant || !Accepted || IsInteractionBlocked || !Near(banquet) || tasks.IsTaskCompleted(3)) return false;
        if (!tasks.IsTaskCompleted(2) || !banquetBriefed) { Notice("先清理灶台，并向庖长接取摆席安排。"); return false; }
        matching.SetActive(true); selected = -1; player.InputLocked = true; player.TouchMove = Vector2.zero;
        player.GetComponent<Rigidbody2D>().velocity = Vector2.zero; RefreshMatching(); return true;
    }
    public void CloseMatching() { matching.SetActive(false); player.InputLocked = ModalOpen; selected = -1; }
    public void SelectUtensil(int index) { if (!matching.activeSelf || index<0 || index>=3 || placed[index]) return; selected=index; RefreshMatching(); }
    public bool MatchSlot(int slot)
    {
        if (!matching.activeSelf || slot<0 || slot>=3 || selected<0 || placed[slot]) return false;
        if (selected != slot) { matchingHint.text = "位置不对应，再看看器物用途和案几标记。"; return false; }
        placed[slot]=true; Write("Placed"+slot,1); selected=-1; RefreshMatching();
        if (placed[0] && placed[1] && placed[2]) { tasks.CompleteTask(3); CloseMatching(); Notice("宴席器物已全部归位，向庖长领取砖画缺片。",7); }
        return true;
    }
    private void RefreshMatching()
    {
        matchingHint.text = selected<0 ? "先选左侧器物，再点击右侧对应位置。" : "已选："+utensilNames[selected]+"，请选择它的摆放位置。";
        for (int i=0;i<3;i++)
        {
            itemButtons[i].interactable = !placed[i]; slotButtons[i].interactable = !placed[i];
            itemButtons[i].GetComponent<Image>().color = selected==i ? new Color(.75f,.52f,.3f) : new Color(.91f,.83f,.67f);
            slotButtons[i].GetComponentInChildren<Text>().text = placed[i] ? "已归位 · "+utensilNames[i] : slotNames[i];
        }
    }
    public void OnRestaurantAreaChanged()
    {
        ClearNotices();
        Notice(IsInsideRestaurant ? "已进入饭店。靠近庖长按 E 交谈，按顶部提示完成备餐；靠近出口按 E 离开。" : "已离开饭店。靠近饭店按 E 可再次进入。", 5);
    }
    protected override void PositionWorldLabels(Camera camera)
    {
        if (worldLabels == null) return;
        Transform[] targets = { chef != null ? chef.transform : null, guest != null ? guest.transform : null, utensils[0], utensils[1], utensils[2], stove, banquet,
            restaurant != null ? restaurant.restaurantName : null, restaurant != null ? restaurant.exit : null };
        TaskInfoPanel taskPanel=DialogueManager.Instance!=null ? DialogueManager.Instance.taskInfoPanel : null;
        for (int i=0;i<targets.Length;i++)
        {
            if (targets[i] == null) { worldLabels[i].gameObject.SetActive(false); continue; }
            PositionWorldLabel(worldLabels[i],targets[i].position+Vector3.up*1.4f,camera, targets[i].gameObject.activeInHierarchy && !IsInteractionBlocked);
            if(taskPanel!=null && taskPanel.gameObject.activeInHierarchy)
            {
                var corners=new Vector3[4]; taskPanel.GetComponent<RectTransform>().GetWorldCorners(corners);
                Rect panelArea=Rect.MinMaxRect(corners[0].x-180,corners[0].y-25,corners[2].x+180,corners[2].y+25);
                if(panelArea.Contains(worldLabels[i].rectTransform.position)) worldLabels[i].gameObject.SetActive(false);
            }
        }
    }
    protected override void BuildLevelUI(Transform canvas)
    {
        levelCanvas=canvas;
        string[] names = { "庖长", "庖厨", "陶耳杯", "食盘", "烤肉炉", "堵塞的灶台", "宴席案几", "饭店", "出口" };
        worldLabels = new Text[names.Length];
        for (int i=0;i<names.Length;i++) { worldLabels[i]=Label(canvas,"WorldLabel"+i,names[i],Vector2.one*.5f,Vector2.zero,new Vector2(360,48),26); worldLabels[i].gameObject.AddComponent<Outline>().effectColor = new Color(.97f,.91f,.8f); }
        matching = new GameObject("器物配对",typeof(RectTransform),typeof(Image)); matching.transform.SetParent(canvas,false);
        RectTransform rect=matching.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.sizeDelta=Vector2.zero;
        matching.GetComponent<Image>().color=new Color(.93f,.86f,.72f);
        Label(matching.transform,"Title","食器归位 · 用途配对",Vector2.one*.5f,new Vector2(0,360),new Vector2(1400,80),44);
        matchingHint=Label(matching.transform,"Hint","",Vector2.one*.5f,new Vector2(0,-290),new Vector2(1450,80),30);
        itemButtons=new Button[3]; slotButtons=new Button[3];
        for (int i=0;i<3;i++) { int index=i; float y=190-i*170; itemButtons[i]=CardButton(matching.transform,"器物"+i,utensilNames[i],new Vector2(-370,y),()=>SelectUtensil(index)); slotButtons[i]=CardButton(matching.transform,"位置"+i,slotNames[i],new Vector2(370,y),()=>MatchSlot(index)); }
        MakeButton(matching.transform,"Close","返回宅院",new Vector2(0,-410),CloseMatching);
        matching.SetActive(false);
        if (Application.isMobilePlatform || alwaysShowTouchControls) BuildTouchControls();
    }
    private void BuildTouchControls()
    {
        if(levelCanvas==null || touchControlsBuilt) return;
        touchControlsBuilt=true;
        Vector2[] directions={Vector2.left,Vector2.right,Vector2.up,Vector2.down}; string[] arrows={"←","→","↑","↓"};
        for(int i=0;i<4;i++)
        {
            Button b=CardButton(levelCanvas,"TouchMove"+i,arrows[i],Vector2.zero,()=>{}); b.transform.SetAsFirstSibling();
            Place(b.GetComponent<RectTransform>(),Vector2.zero,new Vector2(110+i*130,100),new Vector2(110,100));
            CookingTouchMove hold=b.gameObject.AddComponent<CookingTouchMove>(); hold.player=player; hold.direction=directions[i];
        }
        Button interact=CardButton(levelCanvas,"TouchInteract","交互",Vector2.zero,()=>Interact());
        Place(interact.GetComponent<RectTransform>(),Vector2.right,new Vector2(-150,100),new Vector2(220,110)); interact.transform.SetAsFirstSibling();
    }
    private Button CardButton(Transform parent,string name,string value,Vector2 position,UnityEngine.Events.UnityAction action)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
        Place(go.GetComponent<RectTransform>(),Vector2.one*.5f,position,new Vector2(530,110)); go.GetComponent<Image>().color=new Color(.91f,.83f,.67f);
        Text label=Label(go.transform,"Label",value,Vector2.one*.5f,Vector2.zero,new Vector2(510,100),32);
        label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one; label.rectTransform.sizeDelta=new Vector2(-16,-10);
        Button button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>(); button.onClick.AddListener(action); return button;
    }
}
