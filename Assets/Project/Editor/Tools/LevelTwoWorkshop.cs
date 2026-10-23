using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[InitializeOnLoad]
public static partial class LevelTwoWorkshop
{
    public const string Output = "Artifacts/Level2";
    public const string PrefabPath = "Assets/Project/Prefabs/Level2.prefab";
    public const string VideoPath = "Assets/Project/Video/扬场图入场.mp4";
    static LevelTwoWorkshop() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        string command = Output + "/command.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(command)) return;
        string action = File.ReadAllText(command).Trim(); File.Delete(command);
        try
        {
            if (action == "inspect") Inspect();
            if (action == "status") File.WriteAllText(Output+"/editor-state.txt",$"play={EditorApplication.isPlaying} paused={EditorApplication.isPaused} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path} focus={EditorWindow.focusedWindow} qa={UnityEngine.Object.FindObjectOfType<LevelTwoPlaythrough>()}");
            Dispatch(action);
        }
        catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    static partial void Dispatch(string action);
    private static void Inspect()
    {
        var lines = new System.Collections.Generic.List<string>();
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Project/Prefabs/Level1.prefab");
        try
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(t,root.transform);
                if (path.Split('/').Length <= 3)
                    lines.Add(path + " pos=" + t.position + " active=" + t.gameObject.activeSelf + " components=" + string.Join(",",t.GetComponents<Component>().Select(c=>c == null ? "MISSING" : c.GetType().Name)));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/Select.unity",OpenSceneMode.Additive);
        try
        {
            foreach (GameObject obj in scene.GetRootGameObjects()) foreach (Button b in obj.GetComponentsInChildren<Button>(true))
                for (int i=0;i<b.onClick.GetPersistentEventCount();i++) lines.Add("BUTTON "+b.name+" target="+b.onClick.GetPersistentTarget(i)+" method="+b.onClick.GetPersistentMethodName(i));
            foreach(GameObject obj in scene.GetRootGameObjects())
            {
                foreach(ShowObjectOnClick c in obj.GetComponentsInChildren<ShowObjectOnClick>(true)) lines.Add("SHOW "+AnimationUtility.CalculateTransformPath(c.transform,obj.transform)+" => "+c.targetUI+" scene="+c.targetSceneName);
                foreach(ButtonHideShow c in obj.GetComponentsInChildren<ButtonHideShow>(true)) lines.Add("HIDE-SHOW "+AnimationUtility.CalculateTransformPath(c.transform,obj.transform)+" hide="+c.objectToHide+" show="+c.objectToShow);
                foreach(var trigger in obj.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) foreach(var entry in trigger.triggers)
                    for(int i=0;i<entry.callback.GetPersistentEventCount();i++) lines.Add("EVENT "+AnimationUtility.CalculateTransformPath(trigger.transform,obj.transform)+" "+entry.eventID+" => "+entry.callback.GetPersistentTarget(i)+"."+entry.callback.GetPersistentMethodName(i));
                foreach(var behaviour in obj.GetComponentsInChildren<MonoBehaviour>(true)) if(behaviour!=null && behaviour.GetType().Assembly==typeof(LevelBase).Assembly) lines.Add("SCRIPT "+AnimationUtility.CalculateTransformPath(behaviour.transform,obj.transform)+" => "+behaviour.GetType().Name);
            }
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
        lines.Add("VIDEO " + clip.width + "x" + clip.height + " fps=" +clip.frameRate+" seconds="+clip.length+" audio="+clip.audioTrackCount);
        File.WriteAllLines(Output+"/inspection.txt",lines);
    }
}
