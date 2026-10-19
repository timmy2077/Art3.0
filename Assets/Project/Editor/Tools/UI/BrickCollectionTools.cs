using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BrickCollectionTools
{
    [InitializeOnLoadMethod]
    private static void Requested()
    {
        const string request = "Artifacts/BrickCollection.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(request);
            try { Build(); }
            catch (Exception e) { File.WriteAllText("Artifacts/BrickCollection-error.txt", e.ToString()); Debug.LogException(e); }
        };
    }

    [MenuItem("Tools/画像砖录/更新收集与奖励界面")]
    public static void Build()
    {
        const string path = "Assets/Project/Prefabs/RecordUI.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Text title = root.transform.Find("Panel/Title").GetComponent<Text>();
            Transform existing = title.transform.parent.Find("CollectionCount");
            Text count = existing != null ? existing.GetComponent<Text>() : UnityEngine.Object.Instantiate(title, title.transform.parent);
            count.name = "CollectionCount"; count.text = "砖画收集：0 / 5"; count.fontSize = 24;
            count.rectTransform.anchoredPosition = new Vector2(0, -105); count.rectTransform.sizeDelta = new Vector2(-180, 40);
            var data = new SerializedObject(root.GetComponent<RecordWindow>());
            data.FindProperty("collectionCount").objectReferenceValue = count; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        GameObject reward = new GameObject("本关砖画收藏", typeof(RectTransform), typeof(BrickRewardView));
        try
        {
            BrickRewardView view = reward.GetComponent<BrickRewardView>();
            view.catalog = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RecordWindow>();
            view.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
            reward.GetComponent<RectTransform>().sizeDelta = new Vector2(1500, 560);
            PrefabUtility.SaveAsPrefabAsset(reward, "Assets/Project/Resources/BrickRewardView.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(reward); }
        UpgradeCompletionPanel();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject canvas = new GameObject("RewardPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, scene);
            Canvas c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera; c.planeDistance = 5;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            GameObject background = new GameObject("Paper", typeof(RectTransform), typeof(Image)); background.transform.SetParent(canvas.transform, false);
            RectTransform rect = background.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.94f,.89f,.77f);
            BrickRewardView view = UnityEngine.Object.Instantiate(Resources.Load<BrickRewardView>("BrickRewardView"), canvas.transform);
            view.Build(new[] { "gengzhong", "paochu", "gengzhong", "" });
            Text hint = view.transform.Find("收藏提示").GetComponent<Text>();
            if (!hint.text.Contains("2个")) throw new Exception("奖励去重计数错误");
            if (view.transform.Find("奖励横向列表/砖画").childCount != 2) throw new Exception("奖励图片数量错误");
            foreach (Image art in view.GetComponentsInChildren<Image>())
                if (art.name == "砖画图片" && art.sprite == null) throw new Exception("奖励图片缺失");
            Directory.CreateDirectory("Artifacts/BrickCollection");
            BrickRecordTools.Render(c, scene, "通关奖励预览.png", 1920,1080,"Artifacts/BrickCollection");
            view.Build(new string[0]);
            if (!view.transform.Find("收藏提示").GetComponent<Text>().text.Contains("暂无")) throw new Exception("零奖励提示错误");
            view.Build(new[] { "gengzhong", "paochu", "yishi", "sishen", "muzhu" });
            if (view.transform.Find("奖励横向列表/砖画").childCount != 5) throw new Exception("多奖励展示错误");
            UnityEngine.Object.DestroyImmediate(view.gameObject);
            GameObject record = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            record.transform.SetParent(canvas.transform,false);
            record.GetComponent<RecordWindow>().Refresh();
            var recordData = new SerializedObject(record.GetComponent<RecordWindow>());
            Text counter = (Text)recordData.FindProperty("collectionCount").objectReferenceValue;
            if (counter == null || !counter.text.EndsWith("/ 5")) throw new Exception("收集总数或计数绑定错误");
            BrickRecordTools.Render(c, scene, "画像砖录收集进度.png", 1920,1080,"Artifacts/BrickCollection");
            UnityEngine.Object.DestroyImmediate(record);
            GameObject level = PrefabUtility.LoadPrefabContents("Assets/Project/Prefabs/Level1.prefab");
            try
            {
                Transform panel = null;
                foreach (Transform t in level.GetComponentsInChildren<Transform>(true)) if (t.name == "CongratulationPanel") { panel = t; break; }
                GameObject completion = UnityEngine.Object.Instantiate(panel.gameObject, canvas.transform, false);
                completion.SetActive(true);
                BrickRecordTools.Render(c, scene, "CongratulationPanel预览.png",1920,1080,"Artifacts/BrickCollection");
            }
            finally { PrefabUtility.UnloadPrefabContents(level); }
            File.WriteAllText("Artifacts/BrickCollection/validation.txt", "PASS: Unity compile; RecordUI counter refresh and total; reward catalog references; duplicate/empty IDs filtered; zero/two/five rewards; all reward sprites; completion prefab layout rendered.\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        AssetDatabase.SaveAssets();
    }

    private static void UpgradeCompletionPanel()
    {
        const string path = "Assets/Project/Prefabs/Level1.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform panel = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "CongratulationPanel") { panel = t.Find("Image"); break; }
            if (panel == null) throw new Exception("未找到 CongratulationPanel/Image");
            RectTransform rect = (RectTransform)panel;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1200,760);
            panel.GetComponent<Image>().color = new Color(.94f,.89f,.77f);
            NextLevelButton source = panel.GetComponentInChildren<NextLevelButton>(true);
            Transform old = panel.Find("本关砖画收藏");
            BrickRewardView view = old != null ? old.GetComponent<BrickRewardView>() :
                ((GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("BrickRewardView"), panel)).GetComponent<BrickRewardView>();
            view.name = "本关砖画收藏"; view.GetComponent<RectTransform>().sizeDelta = new Vector2(1100,500);
            view.source = source; view.buildOnEnable = true; view.Build(source != null ? source.RewardBrickIds : null);
            foreach (Text text in panel.GetComponentsInChildren<Text>(true))
                if (text.transform.parent == panel) { text.text = "砖画修复完成"; text.font = view.font; text.fontSize = 42; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.22f,.19f,.15f);
                    text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f,.5f); text.rectTransform.anchoredPosition = new Vector2(0,310); text.rectTransform.sizeDelta = new Vector2(900,80); }
            Transform next = panel.Find("btn_next");
            if (next != null)
            {
                RectTransform button = (RectTransform)next; button.anchorMin = button.anchorMax = new Vector2(.5f,.5f); button.anchoredPosition = new Vector2(0,-315); button.sizeDelta = new Vector2(420,64);
                Text label = next.GetComponentInChildren<Text>(true); if (label != null) { label.text = "返回选关"; label.font = view.font; label.fontSize = 28; }
            }
            Transform close = panel.Find("btn_close");
            if (close != null)
            {
                RectTransform button = (RectTransform)close; button.anchorMin = button.anchorMax = Vector2.one; button.anchoredPosition = new Vector2(-42,-42); button.sizeDelta = new Vector2(60,60);
                Image image = close.GetComponent<Image>(); if (image != null) image.color = new Color(.34f,.27f,.20f);
                Text label = close.GetComponentInChildren<Text>(true);
                if (label == null) { GameObject text = new GameObject("关闭文字",typeof(RectTransform),typeof(Text)); text.transform.SetParent(close,false); label = text.GetComponent<Text>(); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; }
                label.text = "×"; label.font = view.font; label.fontSize = 36; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(.94f,.89f,.77f); label.raycastTarget = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
