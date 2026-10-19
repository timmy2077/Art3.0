using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class IndexPrologueTools
{
    [InitializeOnLoadMethod]
    private static void BuildRequested()
    {
        const string request = "Artifacts/IndexPrologue.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(request);
            try { Build(); }
            catch (Exception exception) { File.WriteAllText("Artifacts/IndexPrologue-error.txt", exception.ToString()); Debug.LogException(exception); }
        };
    }

    private static void Build()
    {
        const string path = "Assets/Project/Resources/IndexPrologue.prefab";
        GameObject root = new GameObject("入画序章", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(IndexPrologue));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            RectTransform background = Rect("石砖底色", root.transform, Vector2.zero, Vector2.one);
            background.gameObject.AddComponent<Image>().color = new Color(.24f, .20f, .16f, 1);
            RectTransform panel = Rect("叙事区域", root.transform, new Vector2(.16f, .12f), new Vector2(.84f, .88f));
            Text text = panel.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
            text.fontSize = 30; text.lineSpacing = 1.32f; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.91f, .85f, .72f); text.supportRichText = false; text.raycastTarget = false;
            var data = new SerializedObject(root.GetComponent<IndexPrologue>());
            data.FindProperty("narrative").objectReferenceValue = text;
            data.FindProperty("curtain").objectReferenceValue = root.GetComponent<CanvasGroup>();
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            // 使用同一预制体内容渲染完整文字，检查排版。
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                Canvas previewCanvas = preview.GetComponent<Canvas>(); previewCanvas.renderMode = RenderMode.ScreenSpaceCamera; previewCanvas.planeDistance = 5;
                Text previewText = preview.GetComponentInChildren<Text>();
                previewText.text = data.FindProperty("story").stringValue;
                Directory.CreateDirectory("Artifacts/IndexPrologue");
                BrickRecordTools.Render(previewCanvas, scene, "序章预览.png", 1920, 1080, "Artifacts/IndexPrologue");
                Canvas.ForceUpdateCanvases();
                if (previewText.preferredHeight > previewText.rectTransform.rect.height) throw new Exception("序章文字超出区域");
                if (!IndexPrologue.ConsumeRequest()) { IndexPrologue.Request(); }
                if (!IndexPrologue.ConsumeRequest() || IndexPrologue.ConsumeRequest()) throw new Exception("序章触发消费状态错误");
                File.WriteAllText("Artifacts/IndexPrologue/validation.txt", "PASS: Unity compile, prefab references, full text fits, one-shot Start-to-Index request.\n");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
