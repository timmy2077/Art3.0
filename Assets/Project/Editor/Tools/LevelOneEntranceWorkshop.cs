using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>仅改第一关入口，保留序列帧及第二关手工配置。</summary>
[InitializeOnLoad]
public static class LevelOneEntranceWorkshop
{
    private const string Output = "Artifacts/Level1Entrance";
    private const string SelectPath = "Assets/Project/Scenes/Select.unity";
    private const string QAScene = "Assets/Project/Scenes/__Level1EntranceQA.unity";
    static LevelOneEntranceWorkshop()
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
            if (action == "configure") Configure();
            else if (action == "test") Test();
        }
        catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("尘封砖语/第一关/配置可切换视频入口")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先退出运行模式。");
        Scene scene = SceneManager.GetSceneByPath(SelectPath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty) throw new Exception("Select 场景有未保存修改，请先保存。");
        if (opened) scene = EditorSceneManager.OpenScene(SelectPath, OpenSceneMode.Additive);
        try
        {
            var entrance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HideAndInstantiate>(true)).Single(e => e.name == "耕种图动画");
            if (entrance.gameObject.activeSelf) throw new Exception("第一关入场对象必须默认隐藏。");
            var selector = entrance.GetComponent<LevelEntranceSelector>();
            if (selector == null) selector = entrance.gameObject.AddComponent<LevelEntranceSelector>();
            var video = entrance.GetComponent<LevelVideoEntrance>();
            if (video == null) video = entrance.gameObject.AddComponent<LevelVideoEntrance>();
            video.enabled = false; // 激活入口时由 Selector 选择性启动，避免 OnEnable 重复执行。
            video.clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Project/Video/扬场图入场.mp4");
            video.levelPrefab = entrance.prefabToInstantiate;
            video.selectionUI = entrance.uiToHide;
            video.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
            if (video.clip == null || video.levelPrefab == null || video.selectionUI == null || video.font == null) throw new Exception("第一关视频引用不完整。");
            selector.mode = LevelEntranceSelector.EntranceMode.视频;
            selector.videoEntrance = video;
            selector.sequenceAnimator = entrance.GetComponent<Animator>();
            selector.sequenceImage = entrance.GetComponent<Image>();
            selector.sequenceCompletion = entrance;
            if (selector.sequenceAnimator == null || selector.sequenceAnimator.runtimeAnimatorController == null || selector.sequenceImage == null)
                throw new Exception("原序列帧配置缺失，未保存修改。");
            selector.sequenceAnimator.enabled = false;
            selector.sequenceImage.enabled = false;
            entrance.enabled = false;
            var click = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIHoverShow>(true)).Single(c => c.targetUI == entrance.gameObject);
            click.requiredLevelId = 1;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/configuration.txt", "PASS 第一关已使用扬场图入场.mp4，原 Animator、Image、动画事件、Level1 和选关引用均保留，可切回序列帧。第二关入口未修改。");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    [MenuItem("尘封砖语/第一关/验证视频与序列帧入口")]
    public static void Test()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new Exception("请先保存场景并退出运行模式。");
        Directory.CreateDirectory(Output);
        SessionState.SetString("Level1EntranceQA.Previous", SceneManager.GetActiveScene().path);
        foreach (string file in new[] { "runtime-result.txt", "runtime-progress.txt", "error.txt" })
            if (File.Exists(Output + "/" + file)) File.Delete(Output + "/" + file);
        if (!AssetDatabase.CopyAsset(SelectPath, QAScene)) throw new Exception("临时场景已存在，未覆盖。");
        var scene = EditorSceneManager.OpenScene(QAScene);
        new GameObject("Level1EntranceQA").AddComponent<LevelOneEntrancePlaythrough>();
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool("Level1EntranceQA.Running", true);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.isPlaying = true;
    }
    private static void ReturnFromTest(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Level1EntranceQA.Running", false)) return;
        SessionState.SetBool("Level1EntranceQA.Running", false);
        string previous = SessionState.GetString("Level1EntranceQA.Previous", "");
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.DeleteAsset(QAScene);
    }
}
