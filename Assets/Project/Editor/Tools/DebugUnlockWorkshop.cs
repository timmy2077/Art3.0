using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>测试工具资源生成及编辑器验证入口，不修改正式场景。</summary>
[InitializeOnLoad]
public static class DebugUnlockWorkshop
{
    private const string Output = "Artifacts/DebugUnlock";
    private const string QAScene = "Assets/Project/Scenes/__DebugUnlockQA.unity";
    static DebugUnlockWorkshop()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += ReturnFromTest;
    }
    private static void Poll()
    {
        string path = Output + "/command.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        string action = File.ReadAllText(path).Trim(); File.Delete(path);
        try
        {
            if (action == "build") Build();
            else if (action == "test") Test();
            else if (action == "status") File.WriteAllText(Output + "/status.txt", $"play={EditorApplication.isPlaying} scene={SceneManager.GetActiveScene().path}");
        }
        catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/尘封砖语/生成关卡测试工具")]
    public static void Build()
    {
        var root = new GameObject("DebugUnlockPanel", typeof(RectTransform), typeof(DebugUnlockPanel));
        try
        {
            var so = new SerializedObject(root.GetComponent<DebugUnlockPanel>());
            so.FindProperty("chineseFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
            if (so.FindProperty("chineseFont").objectReferenceValue == null) throw new Exception("缺少中文字体");
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Project/Resources/DebugUnlockPanel.prefab");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/build-result.txt", "PASS 中文字体和测试工具预制体已生成。正式场景无需挂载组件。");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/尘封砖语/验证关卡测试工具")]
    public static void Test()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new Exception("请先保存打开的场景并退出运行模式。");
        Directory.CreateDirectory(Output);
        SessionState.SetString("DebugUnlockQA.Previous", SceneManager.GetActiveScene().path);
        SessionState.SetString("DebugUnlockQA.Save", ReadSave(""));
        SessionState.SetString("DebugUnlockQA.Backup", ReadSave(".bak"));
        if (!AssetDatabase.CopyAsset("Assets/Project/Scenes/Select.unity", QAScene)) throw new Exception("临时验证场景已存在，未覆盖。");
        var scene = EditorSceneManager.OpenScene(QAScene);
        new GameObject("DebugUnlockQA").AddComponent<DebugUnlockPlaythrough>();
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool("DebugUnlockQA.Running", true);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.isPlaying = true;
    }
    private static string ReadSave(string suffix)
    {
        string path = Path.Combine(Application.persistentDataPath, "game-save.json") + suffix;
        return File.Exists(path) ? File.ReadAllText(path) : "<missing>";
    }
    private static void ReturnFromTest(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("DebugUnlockQA.Running", false)) return;
        SessionState.SetBool("DebugUnlockQA.Running", false);
        bool unchanged = SessionState.GetString("DebugUnlockQA.Save", "") == ReadSave("") && SessionState.GetString("DebugUnlockQA.Backup", "") == ReadSave(".bak");
        File.AppendAllText(Output + "/runtime-result.txt", "\n" + (unchanged ? "PASS" : "FAIL") + " 原玩家存档与备份未改变。");
        string previous = SessionState.GetString("DebugUnlockQA.Previous", "");
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.DeleteAsset(QAScene);
    }
}
