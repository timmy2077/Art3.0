#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reproducible chapter authoring and local QA, restricted to named operations.</summary>
[InitializeOnLoad]
public static class FirstLevelWorkshop
{
    private const string PrefabPath = "Assets/Project/Prefabs/Level1.prefab";
    private const string ScenePath = "Assets/Project/Scenes/Level1.unity";
    public static string Output => Path.GetFullPath("Artifacts/FirstLevel");
    static FirstLevelWorkshop()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += PlayState;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "editor-ready.txt"), Application.dataPath);
    }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string commandPath = Path.Combine(Output, "command.txt");
        if (!File.Exists(commandPath)) return;
        string command = File.ReadAllText(commandPath).Trim(); File.Delete(commandPath);
        try
        {
            switch (command)
            {
                case "refresh": AssetDatabase.Refresh(); break;
                case "inspect": Inspect(); break;
                case "repair": RepairCurrentPen(); break;
                case "test": BeginTest(); break;
                case "preview":
                    if (!EditorApplication.isPlaying && !SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene(ScenePath);
                    break;
            }
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(Output, "error.txt"), error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("尘封砖语/第一关/检查当前围栏")]
    public static void Inspect()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            var lines = new System.Collections.Generic.List<string>();
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (r.transform.IsChildOf(root.transform.Find("FarmingGameplay/SharedCowPen")))
                    lines.Add(r.transform.parent.name + "/" + r.name + " center=" + r.bounds.center + " size=" + r.bounds.size);
            foreach (var c in root.transform.Find("FarmingGameplay/SharedCowPen").GetComponentsInChildren<BoxCollider2D>(true))
                lines.Add("COLLIDER " + c.name + " center=" + c.bounds.center + " size=" + c.bounds.size + " trigger=" + c.isTrigger);
            File.WriteAllLines(Path.Combine(Output,"pen-inspection.txt"),lines);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("尘封砖语/第一关/同步当前牛栏任务设置")]
    public static void RepairCurrentPen()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        string backup = Path.Combine(Output,"Level1-before-cleanup.prefab.backup");
        if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            foreach (var trigger in root.GetComponentsInChildren<TaskCompleteTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger);
            FarmingPuzzle puzzle = root.GetComponent<FarmingPuzzle>();
            CowPen white = puzzle.whitePen, black = puzzle.blackPen;
            black.interior = white.interior;
            white.restingPosition = new Vector2(-9.4f,15f);
            black.restingPosition = new Vector2(-9.4f,13f);
            white.objectiveId = 1; black.objectiveId = 2;
            foreach (string name in new[] { "Gate", "GateHandle" }) {
                Transform old = white.transform.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            Transform sign = white.transform.Find("牛栏");
            if (sign != null) sign.position = new Vector3(-7.6f,17.3f,0);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Output,"repair-result.txt"),"Preserved custom fence transforms/colliders; removed unused gate objects; synchronized resting positions.");
    }
    public static void Set(UnityEngine.Object target, string name, object value)
    {
        SerializedObject obj = new SerializedObject(target); SerializedProperty field = obj.FindProperty(name);
        if (value is UnityEngine.Object unityObject) field.objectReferenceValue = unityObject;
        else if (value is bool boolean) field.boolValue = boolean;
        else if (value is float number) field.floatValue = number;
        else if (value is int integer) field.intValue = integer;
        else if (value is Rect rect) field.rectValue = rect;
        else if (value is string[] list) { field.arraySize = list.Length; for (int i = 0; i < list.Length; i++) field.GetArrayElementAtIndex(i).stringValue = list[i]; }
        obj.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("尘封砖语/第一关/运行流程验证")]
    public static void BeginTest()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene and exit Play mode before QA.");
        SessionState.SetString("FarmingQA.PreviousScene", SceneManager.GetActiveScene().path);
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool("FarmingQA.Run", true); EditorApplication.isPlaying = true;
    }
    private static void PlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("FarmingQA.Run", false))
        {
            SessionState.SetBool("FarmingQA.Run", false);
            new GameObject("FirstLevelQA").AddComponent<FirstLevelPlaythrough>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string previous = SessionState.GetString("FarmingQA.PreviousScene", "");
            SessionState.EraseString("FarmingQA.PreviousScene");
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        }
    }
}

#endif


