using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class LevelTwoWorkshop
{
    private const string QAScene="Assets/Project/Scenes/__Level2QA.unity";
    [MenuItem("尘封砖语/第二关/验证饭店进出与任务（不等待视频）")]
    public static void TestRestaurant()
    {
        SessionState.SetBool("Level2QA.SkipVideo",true);
        try { BeginTest(); }
        finally { SessionState.SetBool("Level2QA.SkipVideo",false); }
    }
    static partial void BeginRestaurantTest() { TestRestaurant(); }
    static partial void BeginTest()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new Exception("请先保存已打开场景并退出运行模式，再运行第二关验证。");
        SessionState.SetString("Level2QA.PreviousScene",SceneManager.GetActiveScene().path);
        foreach(string name in new[]{"runtime-error.txt","runtime-result.txt","runtime-progress.txt","video-state.txt","video-trace.txt"}) if(File.Exists(Output+"/"+name)) File.Delete(Output+"/"+name);
        if(!AssetDatabase.CopyAsset("Assets/Project/Scenes/Select.unity",QAScene)) throw new Exception("无法创建临时验证场景");
        Scene scene=EditorSceneManager.OpenScene(QAScene);
        var test=new GameObject("Level2QA").AddComponent<LevelTwoPlaythrough>();
        test.skipVideo=SessionState.GetBool("Level2QA.SkipVideo",false); EditorSceneManager.SaveScene(scene);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        SessionState.SetBool("Level2QA.Running",true); EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void HookTests() { EditorApplication.playModeStateChanged+=ReturnFromTest; }
    private static void ReturnFromTest(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Level2QA.Running",false)) return;
        SessionState.SetBool("Level2QA.Running",false);
        string previous=SessionState.GetString("Level2QA.PreviousScene","");
        if(!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.DeleteAsset(QAScene);
    }
}
