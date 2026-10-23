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

/// <summary>验证同一第一关入口的视频与原动画两条路径，禁止存档写盘。</summary>
[DefaultExecutionOrder(-2000)]
public sealed class LevelOneEntrancePlaythrough : MonoBehaviour
{
    private const string Output = "Artifacts/Level1Entrance";
    private readonly List<string> checks = new List<string>();
    private string originalSave, originalBackup;
    private bool done;
    private float started;
    private void Awake()
    {
        Application.runInBackground = true;
        var save = SaveManager.Instance;
        originalSave = Read(save.SavePath); originalBackup = Read(save.SavePath + ".bak");
        typeof(SaveManager).GetField("writeBlocked", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(save, true);
        DataSystem.instance.enablePersistence = false;
        started = Time.realtimeSinceStartup;
        Application.logMessageReceived += Log;
    }
    private static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
    private void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(text + "\n" + stack);
    }
    private void Update() { if (!done && Time.realtimeSinceStartup - started > 55) Finish("验证超时"); }
    private void Check(bool condition, string description)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + description);
        File.WriteAllLines(Output + "/runtime-progress.txt", checks);
        if (!condition) throw new Exception(description);
    }
    private IEnumerator Start()
    {
        yield return null;
        var flow = Flow();
        while (!done)
        {
            bool next; object current;
            try { next = flow.MoveNext(); current = next ? flow.Current : null; }
            catch (Exception e) { Finish(e.ToString()); yield break; }
            if (!next) break;
            yield return current;
        }
        if (!done) Finish(null);
    }
    private IEnumerator Flow()
    {
        var selector = FindObjectsOfType<LevelEntranceSelector>(true).Single();
        var video = selector.videoEntrance;
        var click = FindObjectsOfType<UIHoverShow>(true).Single(c => c.targetUI == selector.gameObject);
        Check(selector.mode == LevelEntranceSelector.EntranceMode.视频, "第一关默认选择视频");
        Check(video.clip != null && video.clip.name == "扬场图入场" && video.levelPrefab.GetComponent<FarmingPuzzle>() != null, "指定视频连接原 Level1 预制体");
        Check(selector.sequenceAnimator.runtimeAnimatorController != null && selector.sequenceCompletion.prefabToInstantiate == video.levelPrefab, "保留原序列帧及 Level1 引用");
        click.OnPointerClick(null);
        Check(video.enabled && !selector.sequenceAnimator.enabled && !selector.sequenceImage.enabled && !selector.sequenceCompletion.enabled, "视频模式自动停用原动画及其生成事件");
        Check(!video.IsPictureVisible, "准备时保留选关砖图");
        selector.sequenceCompletion.OnAnimationFrame();
        Check(FindObjectsOfType<FarmingPuzzle>().Length == 0 && selector.gameObject.activeSelf, "旧动画事件不会提前生成或关闭视频入口");
        video.Cancel();
        yield return null;
        var menu = video.selectionUI.GetComponentInParent<CanvasGroup>(true);
        Check(menu == null || (menu.alpha == 1 && menu.interactable && menu.blocksRaycasts), "准备阶段返回可恢复选关操作");
        click.OnPointerClick(null); click.OnPointerClick(null);
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        bool sawPicture = false;
        while (!video.Completed && Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (video.IsPictureVisible && !sawPicture)
            {
                sawPicture = true;
                Check(video.FirstFrameReceived, "首帧就绪后才切换视频画面");
            }
            yield return null;
        }
        Check(video.Completed && sawPicture, "视频正常播放完成");
        yield return new WaitForSecondsRealtime(.3f);
        var farming = FindObjectsOfType<FarmingPuzzle>();
        Check(farming.Length == 1 && FindObjectsOfType<CookingPuzzle>().Length == 0, "视频结束只生成一份第一关，不生成第二关");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Output + "/视频进入第一关.png");
        Destroy(farming[0].gameObject);
        yield return null;
        if (menu != null) { menu.alpha = 1; menu.interactable = true; menu.blocksRaycasts = true; }
        else video.selectionUI.SetActive(true);
        selector.mode = LevelEntranceSelector.EntranceMode.序列帧;
        click.OnPointerClick(null);
        Check(!video.enabled && selector.sequenceAnimator.enabled && selector.sequenceImage.enabled && selector.sequenceCompletion.enabled, "改为序列帧后自动恢复原组件，视频关闭");
        yield return new WaitForSecondsRealtime(.25f);
        Check(!video.IsPlaying && FindObjectsOfType<FarmingPuzzle>().Length == 0 && selector.gameObject.activeSelf, "序列帧实际播放，未提前加载关卡");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Output + "/切回序列帧.png");
        deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (selector.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        yield return null;
        Check(!selector.gameObject.activeSelf && FindObjectsOfType<FarmingPuzzle>().Length == 1, "原动画末帧事件正常生成一份 Level1");
        selector.sequenceCompletion.OnAnimationFrame();
        Check(FindObjectsOfType<FarmingPuzzle>().Length == 1, "重复末帧回调不会重复生成关卡");
        Check(FindObjectsOfType<LevelVideoEntrance>(true).Any(v => v.levelPrefab != null && v.levelPrefab.GetComponent<CookingPuzzle>() != null), "第二关独立视频入口保留");
    }
    private void Finish(string error)
    {
        if (done) return;
        done = true;
        Application.logMessageReceived -= Log;
        var save = SaveManager.Instance;
        checks.Add(originalSave == Read(save.SavePath) && originalBackup == Read(save.SavePath + ".bak") ? "PASS 玩家存档及备份未改变" : "FAIL 玩家存档发生改变");
        File.WriteAllLines(Output + "/runtime-result.txt", checks);
        File.AppendAllText(Output + "/runtime-result.txt", error == null ? "\nPASS 验证完成" : "\nFAIL " + error);
        EditorApplication.isPlaying = false;
    }
    private void OnDestroy() { Application.logMessageReceived -= Log; }
}
#endif
