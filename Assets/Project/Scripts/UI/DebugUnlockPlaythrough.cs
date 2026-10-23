#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>临时场景验证。写盘始终被阻止，不覆盖测试者的真实进度。</summary>
[DefaultExecutionOrder(-2000)]
public sealed class DebugUnlockPlaythrough : MonoBehaviour
{
    private const string Output = "Artifacts/DebugUnlock";
    private readonly List<string> checks = new List<string>();
    private bool finished;
    private float started;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        typeof(SaveManager).GetField("writeBlocked", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(SaveManager.Instance, true);
        DataSystem.instance.enablePersistence = false;
        started = Time.realtimeSinceStartup;
        Application.logMessageReceived += Log;
    }
    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(message + "\n" + stack);
    }
    private void Update() { if (!finished && Time.realtimeSinceStartup - started > 40) Finish("验证超时"); }
    private void Check(bool condition, string description)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + description);
        if (!condition) throw new Exception(description);
    }
    private IEnumerator Start()
    {
        yield return null;
        IEnumerator flow = Flow();
        while (!finished)
        {
            bool next; object current;
            try { next = flow.MoveNext(); current = next ? flow.Current : null; }
            catch (Exception e) { Finish(e.ToString()); yield break; }
            if (!next) break;
            yield return current;
        }
        if (!finished) Finish(null);
    }
    private IEnumerator Flow()
    {
        var panel = FindObjectOfType<DebugUnlockPanel>();
        Check(panel != null && FindObjectsOfType<DebugUnlockPanel>().Length == 1, "进入选关场景自动生成单个测试工具");
        bool[] cache = (bool[])typeof(DataSystem).GetField("cachedLevelUnlocked", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DataSystem.instance);
        for (int i = 1; i < 5; i++) cache[i] = false;
        var bricks = FindObjectsOfType<LevelUnlock>(true);
        foreach (var brick in bricks) brick.RefreshLevelState();
        string originalBricks = string.Join(",", (HashSet<string>)typeof(DataSystem).GetField("unlockedBricks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DataSystem.instance));
        var player = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(PlayerMovement)).GetComponent<PlayerMovement>();
        player.GetComponent<Rigidbody2D>().gravityScale = 0;
        player.TouchMove = Vector2.right;
        Button launcher = panel.GetComponentsInChildren<Button>(true).First(b => b.name.Contains("测试工具"));
        launcher.onClick.Invoke();
        Check(DebugUnlockPanel.IsOpen && player.TouchMove == Vector2.zero, "点击入口打开面板并清除触屏移动");
        player.TouchMove = Vector2.right;
        yield return new WaitForFixedUpdate();
        Check(player.GetComponent<Rigidbody2D>().velocity == Vector2.zero, "打开面板时阻止角色移动");
        var text = panel.GetComponentsInChildren<Text>(true).First(t => t.text == "关卡测试工具");
        Check(text.font != null && text.font.HasCharacter('关'), "中文字体可用");
        Transform secondRow = panel.GetComponentsInChildren<Transform>(true).First(t => t.name == "Level2");
        secondRow.GetComponentInChildren<Button>().onClick.Invoke();
        Check(DataSystem.instance.IsLevelUnlocked(2) && !DataSystem.instance.IsLevelUnlocked(3), "单关按钮仅解锁指定第二关");
        foreach (var brick in bricks.Where(b => b.currentLevelId == 2))
            Check(brick.transform.Find("UnLocked").gameObject.activeSelf && !brick.transform.Find("Locked").gameObject.activeSelf, "第二关选关砖即时刷新");
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Output + "/panel.png");
        panel.GetComponentsInChildren<Button>().First(b => b.name == "解锁全部关卡").onClick.Invoke();
        Check(Enumerable.Range(1, 5).All(DataSystem.instance.IsLevelUnlocked), "全部解锁按钮开放五关");
        Check(originalBricks == string.Join(",", (HashSet<string>)typeof(DataSystem).GetField("unlockedBricks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DataSystem.instance)), "关卡解锁不发放收藏奖励");
        player.TouchMove = Vector2.zero;
        panel.Close();
        yield return null;
        Check(!DebugUnlockPanel.BlocksGameplayInput && !player.InputLocked, "关闭面板后恢复输入");
        player.InputLocked = true; panel.Open(); panel.Close();
        Check(player.InputLocked, "原先锁定的玩家保持锁定");
        DataSystem.instance.enablePersistence = true;
        panel.Open(); panel.UnlockLevel(4);
        Check(SaveManager.Instance.GetInt("LevelUnlocked_4", 0) == 1, "解锁接入现有存档键");
        Check(panel.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("存档写入失败")), "保存失败明确提示（验证期间阻止写盘）");
        DataSystem.instance.enablePersistence = false;
        yield return SceneManager.LoadSceneAsync("Index");
        yield return null;
        Check(panel != null && FindObjectsOfType<DebugUnlockPanel>().Length == 1 && !DebugUnlockPanel.IsOpen, "跨场景保留单例并关闭面板");
        panel.Open(); Check(DebugUnlockPanel.IsOpen, "回到主页后仍可打开"); panel.Close();
    }
    private void Finish(string error)
    {
        if (finished) return;
        finished = true;
        Application.logMessageReceived -= Log;
        File.WriteAllLines(Output + "/runtime-result.txt", checks);
        File.AppendAllText(Output + "/runtime-result.txt", error == null ? "\nPASS 验证完成。" : "\nFAIL " + error);
        EditorApplication.isPlaying = false;
    }
}
#endif
