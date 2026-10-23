using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>第二关饭店内外场配置，不重建现有室内或改动第一关。</summary>
[InitializeOnLoad]
public static partial class LevelTwoRestaurantWorkshop
{
    public const string Output = "Artifacts/Level2Restaurant";
    static LevelTwoRestaurantWorkshop() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        string path = Output + "/command.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        string action = File.ReadAllText(path).Trim(); File.Delete(path);
        try
        {
            if (action == "inspect") Inspect();
            Dispatch(action);
        }
        catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    static partial void Dispatch(string action);
    private static void Inspect()
    {
        var lines = new List<string>();
        lines.Add("Editor: play=" + EditorApplication.isPlaying + " scenes=" + string.Join(",", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path + " dirty=" + SceneManager.GetSceneAt(i).isDirty)));
        foreach (string path in new[] { "Assets/Project/Prefabs/Level1.prefab", "Assets/Project/Prefabs/Level2.prefab" })
        {
            lines.Add("PREFAB " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string local = AnimationUtility.CalculateTransformPath(t, root.transform);
                    if (local.Split('/').Length > 2) continue;
                    string value = local + " active=" + t.gameObject.activeSelf + " pos=" + t.position + " scale=" + t.localScale + " components=" + string.Join(",", t.GetComponents<Component>().Select(c => c != null ? c.GetType().Name : "MISSING"));
                    var renderer = t.GetComponent<SpriteRenderer>();
                    if (renderer != null) value += " sprite=" + AssetDatabase.GetAssetPath(renderer.sprite) + " bounds=" + renderer.bounds + " sort=" + renderer.sortingOrder;
                    lines.Add(value);
                }
                foreach (var cam in root.GetComponentsInChildren<Cinemachine.CinemachineVirtualCamera>(true)) lines.Add("CAM size=" + cam.m_Lens.OrthographicSize + " lens=" + cam.m_Lens.Aspect);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllLines(Output + "/inspection.txt", lines);
    }
}
