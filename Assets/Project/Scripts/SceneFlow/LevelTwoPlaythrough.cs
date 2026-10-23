#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>仅编辑器验证，阻止 SaveManager 写盘，使用临时任务命名空间。</summary>
[DefaultExecutionOrder(-2000)]
public sealed class LevelTwoPlaythrough : MonoBehaviour
{
    private const string Output="Artifacts/Level2";
    public bool skipVideo;
    private readonly List<string> checks=new List<string>();
    private readonly List<string> errors=new List<string>();
    private string originalSave;
    private CookingPuzzle puzzle;
    private float started;
    private bool done;
    private void Awake()
    {
        Application.runInBackground=true; Time.timeScale=1;
        SaveManager save=SaveManager.Instance;
        originalSave=File.Exists(save.SavePath) ? File.ReadAllText(save.SavePath) : null;
        typeof(SaveManager).GetField("writeBlocked",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(save,true);
        started=Time.realtimeSinceStartup;
        Application.logMessageReceived+=Log;
    }
    private void Log(string text,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(text+"\n"+stack); }
    private void Update() { if(!done && Time.realtimeSinceStartup-started>120) Finish("验证超时"); }
    private void Check(bool value,string name) { checks.Add((value?"PASS ":"FAIL ")+name); File.WriteAllLines(Output+"/runtime-progress.txt",checks); if(!value) throw new Exception(name); }
    private IEnumerator Start()
    {
        yield return null;
        var stack=new Stack<IEnumerator>(); stack.Push(Flow());
        while(stack.Count>0 && !done)
        {
            bool next=false; object current=null;
            try { next=stack.Peek().MoveNext(); if(next)current=stack.Peek().Current; }
            catch(Exception e) { Finish(e.ToString()); yield break; }
            if(!next) { stack.Pop(); continue; }
            if(current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        if(!done) Finish(null);
    }
    private IEnumerator Flow()
    {
        var entry=Resources.FindObjectsOfTypeAll<LevelVideoEntrance>().First(e=>e.gameObject.scene==gameObject.scene && e.levelPrefab!=null && e.levelPrefab.GetComponent<CookingPuzzle>()!=null);
        UIHoverShow click=FindObjectsOfType<UIHoverShow>(true).First(c=>c.targetUI==entry.gameObject);
        UIPageSwitch pages=FindObjectOfType<UIPageSwitch>(); pages.SwitchToPage(1);
        var cache=(bool[])typeof(DataSystem).GetField("cachedLevelUnlocked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(DataSystem.instance);
        cache[1]=false; click.OnPointerClick(null); Check(!entry.gameObject.activeSelf,"Locked second brick cannot enter");
        DataSystem.instance.UnlockLevel(2);
        if(skipVideo)
        {
            Instantiate(entry.levelPrefab,Vector3.zero,Quaternion.identity);
            var selection=entry.selectionUI.GetComponentInParent<CanvasGroup>(true);
            if(selection!=null){ selection.alpha=0; selection.interactable=false; selection.blocksRaycasts=false; }
            else entry.selectionUI.SetActive(false);
            Check(true,"Restaurant verification instantiates the same Level2 prefab without waiting for native video");
        }
        else
        {
        UnityEngine.Video.VideoClip clip=entry.clip; entry.clip=null; click.OnPointerClick(null);
        yield return null;
        Check(entry.LastError!=null && !entry.Completed,"Missing video fails without spawning a level"); entry.Cancel(); yield return null;
        Check(!entry.gameObject.activeSelf,"Cancel failure returns to selection");
        entry.clip=clip; click.OnPointerClick(null); click.OnPointerClick(null);
        var overlay=(GameObject)typeof(LevelVideoEntrance).GetField("overlay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(entry);
        Check(!entry.IsPictureVisible && overlay.GetComponent<Image>().color.a==0 && overlay.GetComponent<Image>().raycastTarget,"Preparing retains selection image while blocking clicks");
        Check(overlay.GetComponentInChildren<RawImage>().color.a==0,"Uninitialized video texture stays invisible");
        yield return Capture("视频准备保留砖图");
        double deadline=Time.realtimeSinceStartupAsDouble+30;
        bool paused=false;
        double traceTime=0;
        while(!entry.Completed && Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(Time.realtimeSinceStartupAsDouble>traceTime)
            {
                traceTime=Time.realtimeSinceStartupAsDouble+.5;
                var trace=entry.GetComponent<UnityEngine.Video.VideoPlayer>();
                File.AppendAllText(Output+"/video-trace.txt",$"t={Time.realtimeSinceStartup} dsp={AudioSettings.dspTime} delta={Time.unscaledDeltaTime} focus={Application.isFocused} native={(trace!=null ? $"enabled={trace.isActiveAndEnabled} prepared={trace.isPrepared} playing={trace.isPlaying} paused={trace.isPaused} frame={trace.frame} time={trace.time} speed={trace.playbackSpeed} listenerPause={AudioListener.pause} editorPause={EditorApplication.isPaused} appPaused={typeof(LevelVideoEntrance).GetField("appPaused",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(entry)}" : "null")}\n");
            }
            if(entry.IsPictureVisible && !paused && entry.GetComponent<UnityEngine.Video.VideoPlayer>().frame>=3)
            {
                Check(entry.IsPlaying,"Video reaches a decoded first frame");
                Check(entry.FirstFrameReceived && overlay.GetComponentInChildren<RawImage>().color.a==1,"Video is revealed only after a decoded frame");
                yield return Capture("视频入场");
                var pause=typeof(LevelVideoEntrance).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic);
                pause.Invoke(entry,new object[]{true}); yield return new WaitForSecondsRealtime(.3f); pause.Invoke(entry,new object[]{false}); paused=true;
            }
            yield return null;
        }
        var nativeVideo=entry.GetComponent<UnityEngine.Video.VideoPlayer>();
        File.WriteAllText(Output+"/video-state.txt",$"completed={entry.Completed}, firstFrame={entry.FirstFrameReceived}, active={entry.gameObject.activeInHierarchy}, playing={entry.IsPlaying}, error={entry.LastError}, native={nativeVideo}, prepared={(nativeVideo!=null && nativeVideo.isPrepared)}, frame={(nativeVideo!=null ? nativeVideo.frame : -1)}, time={(nativeVideo!=null ? nativeVideo.time : -1)}");
        Check(paused && entry.Completed && entry.FirstFrameReceived,"Video resumes after pause and completes");
        }
        yield return new WaitForSecondsRealtime(.3f);
        Check(FindObjectsOfType<CookingPuzzle>().Length==1 && FindObjectsOfType<FarmingPuzzle>().Length==0,"Second brick creates exactly one Level2 and no Level1");
        puzzle=FindObjectOfType<CookingPuzzle>();
        Check(!puzzle.IsInsideRestaurant, "New level instance starts outdoors regardless of saved indoor position");
        puzzle.player.enabled=false; puzzle.alwaysShowTouchControls=true;
        TaskDataStorage test=Instantiate(puzzle.storage); test.questId="qa_cooking_"+Guid.NewGuid().ToString("N"); puzzle.storage=test;
        DialogueManager.Instance.taskInfoPanel.taskStorage=test; puzzle.RestoreProgress();
        yield return null;
        CookingRestaurant restaurant=puzzle.restaurant;
        Check(restaurant!=null && !puzzle.IsInsideRestaurant && restaurant.exterior.activeSelf && !restaurant.interior.activeSelf,"Second chapter starts outside the restaurant");
        Check(puzzle.GetComponentsInChildren<PlayerMovement>(true).Length==1 && puzzle.GetComponentsInChildren<NPCInteraction>(true).Length==2 && puzzle.guest!=null && !puzzle.guest.gameObject.activeInHierarchy,"One shared player and chef; guest only indoors");
        Check(puzzle.guest.name == "庖厨" && puzzle.guest.GetComponent<Animator>() != null && puzzle.guest.dialogueData.npcName == "庖厨", "Original animated cook restored instead of banquet guest");
        var backCanvas = FindObjectsOfType<SwitchSceneOnFrame>(true).First(b => b.gameObject.name == "BackButton").GetComponent<Canvas>();
        Check(backCanvas != null && backCanvas.overrideSorting && backCanvas.sortingOrder > 80, "Return button renders above level labels");
        int playerId=puzzle.player.GetInstanceID(), chefId=puzzle.chef.GetInstanceID();
        Teleport(restaurant.exteriorSpawn.position + Vector3.left * 12);
        Check(!restaurant.TryUseDoor() && !puzzle.CollectUtensil(0),"Cannot enter from afar or use indoor props outside");
        Teleport(restaurant.exteriorSpawn.position);
        yield return new WaitForSecondsRealtime(.5f);
        Check(puzzle.transform.Find("LevelCanvas/WorldLabel7").gameObject.activeSelf,"Restaurant name is visible above exterior building");
        yield return new WaitForSecondsRealtime(.9f);
        yield return Capture("饭店外场");
        Check(puzzle.transform.Find("LevelCanvas/WorldLabel7").GetComponent<Text>().text=="饭店" && puzzle.transform.Find("LevelCanvas/WorldLabel0").GetComponent<Text>().text=="庖长","World labels show names without E prompts");
        Check(restaurant.buildingCollider != null && !restaurant.buildingCollider.isTrigger, "Restaurant has a solid BoxCollider2D");
        Check(puzzle.transform.Find("LevelCanvas/BackToSelect") == null, "No duplicate return-to-selection button");
        var buildingBounds = restaurant.buildingCollider.bounds;
        Teleport(new Vector3(buildingBounds.min.x - 1.5f, buildingBounds.center.y, 0));
        Check(Vector2.Distance(puzzle.player.transform.position, restaurant.entrance.position) > restaurant.interactionDistance, "Near-building test is outside old invisible-anchor range");
        Check(puzzle.Interact() && puzzle.IsInsideRestaurant,"Same E interaction enters restaurant from accessible building edge");
        Check(!restaurant.TryUseDoor(),"Door cannot switch repeatedly in same frame");
        Check(puzzle.player.GetInstanceID()==playerId && puzzle.chef.GetInstanceID()==chefId && !restaurant.exterior.activeSelf,"Entering reuses player and chef and hides exterior");
        Check(SaveManager.Instance.GetInt("Quest."+test.questId+".Cooking.InRestaurant",0)==1,"Current restaurant area is saved under second-chapter quest");
        yield return new WaitForSecondsRealtime(.4f);
        CookingTouchMove touch=puzzle.GetComponentsInChildren<CookingTouchMove>().First(c=>c.direction==Vector2.right);
        touch.OnPointerDown(null); Check(puzzle.player.TouchMove==Vector2.right,"Mobile direction press sets movement");
        touch.OnPointerUp(null); Check(puzzle.player.TouchMove==Vector2.zero,"Mobile direction release stops movement");
        Check(puzzle.GetComponentsInChildren<Button>().Any(b=>b.name=="TouchInteract"),"Mobile interaction button exists");
        Check(!puzzle.CollectUtensil(0) && !puzzle.ClearStove(),"Cannot collect or clear before accepting quest");
        puzzle.OpenRestoration(); Check(!puzzle.HasMissingPiece,"Puzzle remains locked before final delivery"); puzzle.CloseModal();
        Teleport(puzzle.chef.transform.position+Vector3.down); DialogueManager.Instance.StartDialogue(puzzle.chef); Drain();
        Check(DialogueManager.Instance.dialogueUI.choicePanel.activeSelf,"Chef first dialogue offers task/science choices");
        DialogueManager.Instance.OnChoiceSelected(false); Drain(); Check(!DialogueManager.Instance.IsDialogueActive && !puzzle.Accepted,"Science branch ends without extra choices or accepting quest");
        DialogueManager.Instance.StartDialogue(puzzle.chef); Drain(); DialogueManager.Instance.OnChoiceSelected(true); Drain();
        Check(puzzle.Accepted,"Task branch accepts independent cooking quest");
        yield return new WaitForSecondsRealtime(1);
        yield return Capture("庖厨作坊");
        for(int i=0;i<3;i++) { Teleport(puzzle.utensils[i].position); Check(puzzle.CollectUtensil(i),"Collect utensil "+i); Check(!puzzle.CollectUtensil(i),"Cannot collect duplicate utensil "+i); }
        Check(puzzle.tasks.IsTaskCompleted(1),"All three utensils complete objective 1");
        Teleport(restaurant.exit.position);
        puzzle.OpenRestoration(); Check(!restaurant.TryUseDoor(),"Open puzzle blocks exit interaction"); puzzle.CloseModal();
        puzzle.player.TouchMove=Vector2.right;
        Check(restaurant.TryUseDoor() && !puzzle.IsInsideRestaurant && puzzle.player.TouchMove==Vector2.zero,"Restaurant exit returns outdoors and clears held movement");
        Check(!puzzle.CollectUtensil(1) && !restaurant.interior.activeSelf && !restaurant.interiorBounds.gameObject.activeSelf,"Interior props and collision bounds are inactive outdoors");
        yield return new WaitForSecondsRealtime(.4f);
        Teleport(restaurant.entrance.position); Check(puzzle.Interact() && puzzle.IsInsideRestaurant,"Can enter restaurant again after leaving");
        Check(puzzle.tasks.IsTaskCompleted(1) && puzzle.HasUtensil(0) && puzzle.player.GetInstanceID()==playerId,"Reentry preserves collection progress and same player");
        typeof(CookingRestaurant).GetMethod("ChangeArea",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(restaurant,new object[]{false,false});
        puzzle.RestoreProgress(); Check(puzzle.IsInsideRestaurant && puzzle.HasUtensil(0),"Continue/restoration restores saved indoor area and quest progress");
        yield return new WaitForSecondsRealtime(.4f);
        Teleport(puzzle.stove.position); Check(!puzzle.ClearStove(),"Stove cleaning waits for chef briefing");
        TalkChef(); Check(puzzle.StoveBriefed,"Collected-stage chef dialogue unlocks cleaning");
        Teleport(puzzle.stove.position); Check(puzzle.ClearStove(),"First debris cleared");
        puzzle.RestoreProgress(); Check(puzzle.ClearedDebris==1 && puzzle.HasUtensil(0),"Partial cleanup and collected items restore from save values");
        Check(puzzle.ClearStove() && puzzle.ClearStove() && puzzle.tasks.IsTaskCompleted(2),"Three cleanups complete stove objective");
        Teleport(puzzle.banquet.position); Check(!puzzle.OpenMatching(),"Banquet waits for chef briefing");
        TalkChef(); Teleport(puzzle.banquet.position); Check(puzzle.OpenMatching() && puzzle.player.InputLocked,"Matching opens and locks movement");
        puzzle.SelectUtensil(0); Check(!puzzle.MatchSlot(1) && !puzzle.IsPlaced(1),"Wrong match does not advance objective");
        Check(puzzle.MatchSlot(0),"Correct cup match accepted");
        puzzle.CloseMatching(); puzzle.RestoreProgress(); Check(puzzle.IsPlaced(0),"Partial placement survives reopening/restoration");
        puzzle.OpenMatching();
        yield return null;
        Check(puzzle.transform.Find("LevelCanvas/TouchInteract").GetSiblingIndex()<puzzle.transform.Find("LevelCanvas/器物配对").GetSiblingIndex(),"Matching panel blocks touch controls behind it");
        yield return Capture("器物配对");
        for(int i=1;i<3;i++) { puzzle.SelectUtensil(i); Check(puzzle.MatchSlot(i),"Place utensil "+i); }
        Check(puzzle.tasks.IsTaskCompleted(3) && !puzzle.HasMissingPiece && !puzzle.player.InputLocked,"Placement finishes without premature puzzle reward");
        TalkChef(); Check(puzzle.HasMissingPiece && !puzzle.BrickSolved,"Chef grants missing fragment, not puzzle completion");
        puzzle.RestoreProgress(); Check(puzzle.HasMissingPiece && !puzzle.BrickSolved,"Unsolved puzzle remains unsolved after restoring fragment reward");
        puzzle.OpenRestoration();
        yield return null;
        yield return Capture("庖厨图拼图");
        for(int i=0;i<9;i++) { int[] order=puzzle.PieceOrder; if(order[i]==i)continue; int other=Array.IndexOf(order,i); puzzle.SelectPiece(i); puzzle.SelectPiece(other); }
        Check(puzzle.BrickSolved && DataSystem.instance.IsBrickUnlocked("paochu") && DataSystem.instance.IsLevelUnlocked(3),"Solving paochu puzzle grants collection and unlocks third brick");
        yield return new WaitForSecondsRealtime(.5f);
        yield return Capture("庖厨图通关");
        puzzle.CloseModal();
        Check(puzzle.guest!=null && puzzle.guest.gameObject.activeInHierarchy && puzzle.GetComponentsInChildren<NPCInteraction>(true).Length==2,"Chef and guest remain in restaurant at completion");
        puzzle.ContinueChapter(); yield return new WaitForSecondsRealtime(.4f);
        Check(FindObjectOfType<CookingPuzzle>()==null && pages.gameObject.activeInHierarchy,"Continue removes Level2 and restores selection");
        Check(UnityEngine.EventSystems.EventSystem.current!=null,"Selection EventSystem remains usable");
        // 第一关入口和预制体保持可用。
        var first=FindObjectsOfType<HideAndInstantiate>(true).First(e=>e.name=="耕种图动画");
        var selector=first.GetComponent<LevelEntranceSelector>();
        if(selector!=null) selector.mode=LevelEntranceSelector.EntranceMode.序列帧;
        first.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2.2f);
        yield return new WaitForSecondsRealtime(.4f);
        Check(FindObjectsOfType<FarmingPuzzle>().Length==1,"First chapter still instantiates original farming prefab");
        var firstLevel = FindObjectOfType<FarmingPuzzle>();
        var back = FindObjectsOfType<SwitchSceneOnFrame>(true).First(b => b.gameObject.name == "BackButton");
        back.OnAnimationFrame(); yield return null;
        Check(firstLevel == null && pages.gameObject.activeInHierarchy && (pages.GetComponentInParent<CanvasGroup>(true) == null || pages.GetComponentInParent<CanvasGroup>(true).blocksRaycasts), "Back destroys current level and restores selection without loading Index");
        Check(errors.Count==0,"No runtime errors");
    }
    private IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Output+"/"+name+".png"); yield return null; }
    private void Teleport(Vector3 position) { puzzle.player.transform.position=position; var body=puzzle.player.GetComponent<Rigidbody2D>(); body.position=position; body.velocity=Vector2.zero; Physics2D.SyncTransforms(); }
    private void TalkChef() { Teleport(puzzle.chef.transform.position+Vector3.down); DialogueManager.Instance.StartDialogue(puzzle.chef); Drain(); }
    private void Drain()
    {
        DialogueManager manager=DialogueManager.Instance;
        for(int i=0;i<60 && manager.IsDialogueActive;i++) { if(manager.dialogueUI.choicePanel.activeSelf)break; manager.dialogueUI.ShowFullText(); manager.OnContinue(); }
    }
    private void Finish(string failure)
    {
        if(done)return; done=true;
        string current=File.Exists(SaveManager.Instance.SavePath)?File.ReadAllText(SaveManager.Instance.SavePath):null;
        checks.Add(current==originalSave ? "PASS Player save file unchanged" : "FAIL Player save file changed");
        File.WriteAllLines(Output+"/runtime-validation.txt",checks);
        if(failure!=null || errors.Count>0) File.WriteAllText(Output+"/runtime-error.txt",failure+"\n"+string.Join("\n",errors));
        else File.WriteAllText(Output+"/runtime-result.txt",skipVideo ? "PASS: restaurant exterior/interior, shared player/chef, door interaction, saved area, complete cooking chapter, collection/unlock, return and first-level regression. Video not rerun. Player save file unchanged.\n" : "PASS: real Select entry, H264 video decode/pause/resume, restaurant exterior/interior, complete chapter flow, save-value restoration, collection/unlock, return and first-level regression. Player save file unchanged.\n");
        EditorApplication.isPlaying=false;
    }
    private void OnDestroy() { Application.logMessageReceived-=Log; }
}
#endif



