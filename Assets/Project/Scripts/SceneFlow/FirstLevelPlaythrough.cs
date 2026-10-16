#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editor-only integration checks using real 2D physics and gameplay interaction gates.</summary>
public sealed class FirstLevelPlaythrough : MonoBehaviour
{
    private FarmingPuzzle puzzle;
    private Rigidbody2D playerBody;
    private readonly List<string> checks = new List<string>();
    private readonly List<string> errors = new List<string>();
    private float deadline;
    private string testQuest;
    private void Awake()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        puzzle = FindObjectOfType<FarmingPuzzle>();
        FirstLevelWorkshop.Set(puzzle.tasks, "enablePersistence", false);
        TaskDataStorage clone = Instantiate(puzzle.storage);
        testQuest = "qa_" + Guid.NewGuid().ToString("N"); clone.questId = testQuest;
        puzzle.storage = clone;
        puzzle.tasks.RestoreTask(clone);
        puzzle.tool.gameObject.SetActive(true);
        typeof(FarmingPuzzle).GetField("<BrickSolved>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(puzzle,false);
        foreach (CowPen pen in new[] { puzzle.whitePen, puzzle.blackPen }) {
            pen.Restore(false);
            pen.cowBody.attachedRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            pen.ResetLooseCow();
        }
        FindObjectOfType<DialogueManager>().taskInfoPanel.taskStorage = clone;
        if (DataSystem.instance != null) DataSystem.instance.enablePersistence = false;
        Application.logMessageReceived += Log;
        deadline = Time.realtimeSinceStartup + 180;
    }
    private void Log(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition + "\n" + stack);
    }
    private void Update()
    {
        if (Time.realtimeSinceStartup > deadline) Finish("Timed out");
    }
    private void Check(bool value, string name)
    {
        checks.Add((value ? "PASS " : "FAIL ") + name);
        File.WriteAllLines(Path.Combine(FirstLevelWorkshop.Output, "test-progress.txt"), checks);
        if (!value) throw new InvalidOperationException(name);
    }
    private IEnumerator Start()
    {
        yield return null;
        puzzle.RestoreProgress();
        playerBody = puzzle.player.GetComponent<Rigidbody2D>();
        puzzle.player.enabled = false;
        yield return RunProtected();
    }
    private IEnumerator RunProtected()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Flow());
        while (stack.Count > 0)
        {
            object next;
            try {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception exception) { Finish(exception.ToString()); yield break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        Finish(null);
    }
    private IEnumerator Flow()
    {
        Check(!puzzle.Accepted, "Fresh quest starts unaccepted");
        Transform ui = puzzle.transform.Find("FarmingPuzzleCanvas");
        Check(ui.Find("ContextHint") == null && ui.Find("ChapterStatus") == null, "Removed duplicate status and solution hints");
        Check(ui.Find("FarmerName").GetComponent<Text>().text == "耕民", "Farmer has a visible name marker");
        Check(ui.Find("ToolName").GetComponent<Text>().fontSize == ui.Find("FarmerName").GetComponent<Text>().fontSize, "Tool and farmer labels use the same font size");
        Check(ui.Find("拼图").GetComponent<Button>() != null, "Independent puzzle entry exists");
        Check(!puzzle.HasMissingPiece && !puzzle.BrickSolved, "Puzzle starts locked without missing fragment");
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "tutorial-opening.png"));
        yield return null;
        puzzle.OpenRestoration();
        int[] lockedOrder = puzzle.PieceOrder;
        puzzle.SelectPiece(0); puzzle.SelectPiece(1);
        Check(string.Join(",", lockedOrder) == string.Join(",", puzzle.PieceOrder), "Locked puzzle rejects fragment swaps");
        Transform grid = ui.Find("BrickRestoration/ArrangeFragments");
        int hidden = 0;
        for (int i = 0; i < 9; i++)
        {
            Image tile = grid.Find("Fragment" + i).GetComponent<Image>();
            if (tile.sprite == null) { hidden++; Check(lockedOrder[i] == puzzle.MissingPieceIndex, "Random missing fragment matches hidden tile"); }
            Check(!tile.GetComponent<Button>().interactable, "Locked tile " + i + " is disabled");
        }
        Check(hidden == 1 && grid.Find("Hint").GetComponent<Text>().text.Contains("拼图功能已锁住"), "Exactly one tile missing with lock explanation");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "puzzle-locked.png"));
        yield return null;
        puzzle.CloseModal(); puzzle.OpenRestoration();
        Check(string.Join(",", lockedOrder) == string.Join(",", puzzle.PieceOrder), "Reopening keeps the same missing tile and arrangement");
        puzzle.CloseModal();
        puzzle.blackPen.cow.enabled = false;
        yield return CheckTeleport(puzzle.whitePen, new Vector2(-15.1f,5), new Vector2(-13.2f,5), false);
        yield return CheckTeleport(puzzle.whitePen, new Vector2(-15.1f,-1.1f), new Vector2(-13.2f,0.3f), true);
        puzzle.whitePen.cow.enabled = false;
        yield return CheckTeleport(puzzle.blackPen, new Vector2(-15.1f,5), new Vector2(-13.2f,5), false);
        puzzle.ResetLooseCows();
        TeleportPlayer(new Vector2(3.3f,0));
        TeleportPlayer(puzzle.whitePen.spawnPosition);
        Check(!puzzle.RecordArrival(puzzle.whitePen), "Cannot capture before accepting quest");
        DialogueManager dm = FindObjectOfType<DialogueManager>();
        NPCInteraction npc = puzzle.farmer.GetComponent<NPCInteraction>();
        TeleportPlayer(puzzle.farmer.position + Vector3.right * 1.3f);
        dm.StartDialogue(npc);
        Check(dm.dialogueUI.interactionHint == null, "Legacy interaction overlay is removed from active dialogue UI");
        Check(dm.taskInfoPanel.taskText[0].fontSize == 28, "Active task panel uses readable wrapping text");
        for (int i = 0; i < 8 && !dm.dialogueUI.choicePanel.activeSelf; i++) dm.OnContinue();
        Check(dm.dialogueUI.choicePanel.activeSelf, "Farmer offers task and history branches");
        dm.OnChoiceSelected(true);
        for (int i = 0; i < 12 && dm.IsDialogueActive; i++) dm.OnContinue();
        Check(puzzle.Accepted && !dm.IsDialogueActive, "Task accepted through real dialogue flow");
        yield return null;
        yield return new WaitForEndOfFrame();
        Check(ui.Find("Discovery").GetComponent<Text>().text.Contains("已接到") && !puzzle.HasMissingPiece, "Tutorial advances only after farmer dialogue and acceptance");
        Check(!puzzle.RecordArrival(puzzle.whitePen), "Cow outside pen does not complete target");
        Check(puzzle.whitePen.interior == puzzle.blackPen.interior, "Both cows share exactly one pen interior");
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "field.png"));
        puzzle.blackPen.cow.enabled = false;
        yield return Herd(puzzle.whitePen);
        yield return null;
        Check(puzzle.whitePen.CowFullyInside(), "White cow can be driven fully into pen using fear movement");
        TeleportPlayer(puzzle.whitePen.spawnPosition);
        Check(puzzle.whitePen.Captured && puzzle.tasks.IsTaskCompleted(1) && !puzzle.tasks.IsTaskCompleted(2), "White cow arrival independently completes only white cow objective");
        Vector2 resting = puzzle.whitePen.cowBody.attachedRigidbody.position;
        puzzle.ResetLooseCows();
        Check(puzzle.whitePen.Captured && puzzle.whitePen.cowBody.attachedRigidbody.position == resting, "Reset preserves captured cow and progress");
        yield return Herd(puzzle.blackPen);
        yield return null;
        Check(puzzle.blackPen.CowFullyInside(), "Black cow can be driven into shared pen");
        yield return new WaitForSeconds(1f);
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "shared-pen.png"));
        TeleportPlayer(puzzle.blackPen.spawnPosition);
        Check(puzzle.blackPen.Captured && puzzle.tasks.IsTaskCompleted(2), "Black cow arrival automatically completes black cow objective");
        TeleportPlayer((Vector2)puzzle.tool.position + ((Vector2)(puzzle.tool.position-puzzle.farmer.position)).normalized*2f);
        Check(puzzle.TryInteract(FarmingPuzzle.Interaction.TakeTool), "Retrieve farming tool");
        Check(puzzle.tasks.CurrentState == PlayerTaskManager.QuestState.ObjectivesCompleted, "All three objectives lead to return stage");
        Check(!puzzle.HasMissingPiece && !puzzle.BrickSolved, "Objectives alone do not grant missing piece or restore brick");
        TeleportPlayer(puzzle.farmer.position + Vector3.right * 1.2f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Check(puzzle.TryInteract(FarmingPuzzle.Interaction.RestoreBrick) && !puzzle.ModalOpen, "Final hand-in grants reward without opening puzzle");
        Check(puzzle.HasMissingPiece && !puzzle.BrickSolved && puzzle.tasks.IsTaskCompleted(puzzle.storage.finalTaskId), "Final task awards missing fragment before restoration");
        Check(!puzzle.TryInteract(FarmingPuzzle.Interaction.RestoreBrick), "Final reward cannot be claimed twice");
        puzzle.OpenRestoration();
        for (int i = 0; i < 9; i++)
        {
            var tile = grid.Find("Fragment" + i).GetComponent<Image>();
            Check(tile.sprite != null && tile.GetComponent<Button>().interactable, "Reward reveals and unlocks tile " + i);
        }
        Check(puzzle.player.InputLocked, "Restoration locks player movement");
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "restoration.png"));
        Check(puzzle.PieceOrder.Length == 9, "Nine fragments created");
        puzzle.SelectPiece(0); puzzle.SelectPiece(0);
        Check(!puzzle.BrickSolved, "Wrong fragment order does not restore brick");
        puzzle.CloseModal();
        Check(!puzzle.player.InputLocked, "Closing restoration restores movement");
        puzzle.OpenRestoration();
        int[] order = puzzle.PieceOrder;
        for (int i = 0; i < order.Length; i++)
        {
            order = puzzle.PieceOrder;
            if (order[i] == i) continue;
            int other = Array.IndexOf(order, i);
            puzzle.SelectPiece(i); puzzle.SelectPiece(other);
        }
        Check(puzzle.BrickSolved && puzzle.tasks.CurrentState == PlayerTaskManager.QuestState.Completed, "Correct fragment order completes chapter");
        yield return new WaitForSecondsRealtime(2f);
        Check(DataSystem.instance.IsLevelUnlocked(2), "Completion unlocks second brick");
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(FirstLevelWorkshop.Output, "completed.png"));

        // Persist to a unique disposable quest namespace; never overwrite real player progress.
        FirstLevelWorkshop.Set(puzzle.tasks, "enablePersistence", true);
        if (DataSystem.instance != null) DataSystem.instance.enablePersistence = true;
        TaskDataStorage persistent = Instantiate(puzzle.storage); persistent.questId = testQuest + "_persist";
        puzzle.tasks.AcceptTask(persistent); puzzle.tasks.CompleteTask(1);
        puzzle.tasks.RestoreTask(persistent);
        Check(puzzle.tasks.IsTaskCompleted(1) && puzzle.tasks.CurrentState == PlayerTaskManager.QuestState.Active, "Quest objective survives persistence reload");
        SaveManager.Instance.DeleteKey("Quest." + persistent.questId + ".State");
        SaveManager.Instance.DeleteKey("Quest." + persistent.questId + ".CompletedIds"); SaveManager.Instance.Save();
        FirstLevelWorkshop.Set(puzzle.tasks, "enablePersistence", false);
        if (DataSystem.instance != null) DataSystem.instance.enablePersistence = false;
        Check(errors.Count == 0, "No runtime errors during playthrough");
    }
    private IEnumerator CheckTeleport(CowPen pen, Vector2 start, Vector2 playerPosition, bool blockRear)
    {
        pen.cow.enabled = true;
        Rigidbody2D cowBody = pen.cowBody.attachedRigidbody;
        cowBody.position = start; pen.cow.transform.position = start;
        TeleportPlayer(playerPosition);
        Vector2 rear = (playerPosition-start).normalized;
        GameObject obstacle = null;
        if (blockRear) {
            obstacle = new GameObject("QA blocked rear landing",typeof(BoxCollider2D));
            obstacle.transform.position = playerPosition+rear*5;
            obstacle.GetComponent<BoxCollider2D>().size = new Vector2(2.5f,2.5f);
        }
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        Vector2 landed = pen.cowBody.bounds.center;
        Check(Vector2.Distance(landed,start)>4f, pen.cow.name+" teleports before contact at wall/corner");
        Check(Vector2.Dot(landed-playerPosition,rear)>0, pen.cow.name+" prefers landing behind player");
        Check(!pen.cowBody.IsTouching(puzzle.player.GetComponent<Collider2D>()), "Teleport avoids player body");
        if (obstacle != null) {
            Check(!pen.cowBody.IsTouching(obstacle.GetComponent<Collider2D>()), "Blocked rear landing chooses clear alternative");
            Destroy(obstacle);
        }
        pen.ResetLooseCow();
    }
    private IEnumerator Herd(CowPen pen)
    {
        Rigidbody2D cowBody = pen.cowBody.attachedRigidbody;
        // Route around the south wall to the user's east-facing entrance.
        Vector2[] route = { new Vector2(0.5f,5), new Vector2(0.5f,14), new Vector2(-7.4f,14) };
        foreach (Vector2 waypoint in route) {
            TeleportPlayer(cowBody.position - (waypoint-cowBody.position).normalized*5.5f);
            float stopAt = Time.realtimeSinceStartup + 25;
            while (!pen.Captured && Vector2.Distance(cowBody.position,waypoint) > 0.7f && Time.realtimeSinceStartup < stopAt) {
                Vector2 direction = (waypoint-cowBody.position).normalized;
                Vector2 behind = cowBody.position-direction*3.1f;
                // Start beyond fear range, then approach physically from the required side.
                if (Vector2.Distance(playerBody.position,behind)>5f) TeleportPlayer(cowBody.position-direction*5.5f);
                playerBody.MovePosition(Vector2.MoveTowards(playerBody.position,behind,3.8f*Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
            File.AppendAllText(Path.Combine(FirstLevelWorkshop.Output,"herd-route.txt"), pen.cow.name+" waypoint="+waypoint+" position="+cowBody.position+" body="+pen.cowBody.bounds+" interior="+pen.interior.bounds+"\n");
        }
    }

    private void TeleportPlayer(Vector2 position)
    {
        playerBody.velocity = Vector2.zero; playerBody.position = position; puzzle.player.transform.position = position; Physics2D.SyncTransforms();
    }
    private void Finish(string failure)
    {
        Application.logMessageReceived -= Log;
        string result = failure == null && errors.Count == 0 ? "PASS" : "FAIL";
        File.WriteAllText(Path.Combine(FirstLevelWorkshop.Output, "test-result.txt"), result + "\n" + string.Join("\n", checks) + "\n" + failure + "\n" + string.Join("\n", errors));
        EditorApplication.isPlaying = false;
        enabled = false;
    }
}

#endif










