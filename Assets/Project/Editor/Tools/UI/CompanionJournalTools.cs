using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CompanionJournalTools
{
    private const string PrefabPath = "Assets/Project/Resources/CompanionJournal.prefab";
    private const string Output = "Artifacts/CompanionJournal";
    private static readonly Color Paper = new Color(.94f, .89f, .77f);
    private static readonly Color Ink = new Color(.22f, .19f, .15f);
    private static readonly Color Accent = new Color(.47f, .24f, .17f);
    private static Font Body => AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
    private static Font Heading => AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsKaiTi.ttf");

    [InitializeOnLoadMethod]
    private static void RunRequest()
    {
        const string request = "Artifacts/CompanionJournal.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(request);
            Directory.CreateDirectory(Output);
            try { Build(); Validate(); }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    private static CompanionProfile[] Profiles()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Project/Data")) AssetDatabase.CreateFolder("Assets/Project", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Project/Data/Companions")) AssetDatabase.CreateFolder("Assets/Project/Data", "Companions");
        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Characters/NPCs/baishen.png");
        var result = new CompanionProfile[4];
        for (int i = 0; i < result.Length; i++)
        {
            string path = "Assets/Project/Data/Companions/Companion" + (i + 1) + ".asset";
            CompanionProfile profile = AssetDatabase.LoadAssetAtPath<CompanionProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CompanionProfile>();
                profile.displayName = i == 0 ? "耕民" : "第" + (i + 1) + "位故人";
                profile.completedLevel = i + 1;
                profile.portrait = portrait;
                profile.identity = i == 0
                    ? "生活在农耕砖画中的耕作者。春耕将近，他正为受惊的耕牛和遗落的耕具发愁。"
                    : "人物身份尚未记录。";
                profile.sharedExperience = i == 0
                    ? "初入砖中田野，你应下了他的求助：先观察白牛与黑牛的习性，将它们赶回牛栏，再寻回遗落的耕具。\n\n这次相助让你与他结为画中故人，也让你在牛、牵引线与农具之间，读到了画像砖留下的农耕线索。"
                    : "与主角的经历尚未记录。";
                profile.relatedDialogues = i == 0
                    ? new[] { AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Project/Scripts/Dialogue/Data/DialogueAssets/NPC1.asset") }
                    : Array.Empty<DialogueData>();
                AssetDatabase.CreateAsset(profile, path);
            }
            result[i] = profile;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:DialogueData", new[] { "Assets/Project" }))
        {
            DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(AssetDatabase.GUIDToAssetPath(guid));
            dialogue.historyId = guid;
            EditorUtility.SetDirty(dialogue);
        }
        return result;
    }

    private static void Build()
    {
        CompanionProfile[] profiles = Profiles();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) { AssetDatabase.SaveAssets(); return; }
        RectTransform root = Rect("CompanionJournal", null); Stretch(root);
        ImageOf(root, new Color(.06f, .05f, .04f, .86f));
        CompanionJournal journal = root.gameObject.AddComponent<CompanionJournal>();
        try
        {
            RectTransform list = Rect("ListPage", root); Stretch(list, 240, 110); ImageOf(list, Paper);
            Text listTitle = TextRegion(list, "Title", "人物志", 52, Ink, new Vector2(.055f, .84f), new Vector2(.7f, .97f));
            listTitle.font = Heading;
            TextRegion(list, "Subtitle", "已相逢的故人，可点击重温相助的故事。", 23, Accent, new Vector2(.055f, .77f), new Vector2(.83f, .85f));
            Button close = ButtonAt(list, "CloseList", "返回画境", new Vector2(1, 1), new Vector2(-150, -72), new Vector2(210, 60));
            ScrollRect listScroll = Scroll(list, "ListScroll", new Vector2(.055f, .08f), new Vector2(.945f, .74f));
            VerticalLayoutGroup listLayout = listScroll.content.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 18; listLayout.padding = new RectOffset(0, 22, 8, 8);
            RectTransform item = Rect("ItemTemplate", listScroll.content);
            Image itemImage = ImageOf(item, new Color(.87f, .79f, .63f));
            Button itemButton = item.gameObject.AddComponent<Button>(); itemButton.targetGraphic = itemImage;
            ColorBlock colors = itemButton.colors;
            colors.highlightedColor = new Color(1, .94f, .81f);
            colors.disabledColor = new Color(.65f, .61f, .53f, .65f);
            itemButton.colors = colors;
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            Text itemLabel = TextRegion(item, "Label", "", 28, Ink, new Vector2(.04f, 0), new Vector2(.96f, 1));
            item.gameObject.SetActive(false);

            RectTransform detail = Rect("DetailPage", root); Stretch(detail, 70, 48); ImageOf(detail, Paper);
            TextRegion(detail, "Eyebrow", "人 物 志   /   画 中 故 人", 20, Accent, new Vector2(.04f, .92f), new Vector2(.8f, .98f));
            Text name = TextRegion(detail, "CharacterName", "耕民", 52, Ink, new Vector2(.04f, .82f), new Vector2(.8f, .92f)); name.font = Heading;
            Button back = ButtonAt(detail, "BackToList", "返回人物志", new Vector2(1, 1), new Vector2(-152, -66), new Vector2(230, 60));
            RectTransform imagePanel = Rect("PortraitPanel", detail);
            Region(imagePanel, new Vector2(.04f, .09f), new Vector2(.34f, .78f));
            ImageOf(imagePanel, new Color(.86f, .80f, .66f));
            RectTransform imageRect = Rect("Portrait", imagePanel); Stretch(imageRect, 30, 38);
            Image portrait = ImageOf(imageRect, Color.white); portrait.preserveAspect = true; portrait.raycastTarget = false;
            TextRegion(detail, "Status", "已相逢 · 画中故人", 24, Accent, new Vector2(.04f, .03f), new Vector2(.34f, .085f)).alignment = TextAnchor.MiddleCenter;
            ScrollRect detailScroll = Scroll(detail, "DetailScroll", new Vector2(.39f, .065f), new Vector2(.96f, .80f));
            Text identity = Section(detailScroll.content, "Identity", "人物身份");
            Text experience = Section(detailScroll.content, "Experience", "与主角的经历");
            Text history = Section(detailScroll.content, "DialogueHistory", "对话回顾");
            Bind(journal, "listPage", list.gameObject, "listContent", listScroll.content, "itemTemplate", itemButton,
                "closeListButton", close, "detailPage", detail.gameObject, "characterName", name,
                "portrait", portrait, "identity", identity, "experience", experience, "dialogueHistory", history,
                "detailScroll", detailScroll, "backButton", back);
            var serialized = new SerializedObject(journal);
            var array = serialized.FindProperty("profiles"); array.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            detail.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }

    private static ScrollRect Scroll(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform root = Rect(name, parent); Region(root, min, max);
        ImageOf(root, new Color(1, 1, 1, .10f));
        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
        RectTransform viewport = Rect("Viewport", root); Stretch(viewport); viewport.offsetMax = new Vector2(-22, 0);
        ImageOf(viewport, new Color(1, 1, 1, .001f)); viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 10, 28); layout.spacing = 16;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        RectTransform track = Rect("Scrollbar", root); track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1, .5f); track.sizeDelta = new Vector2(10, -20); track.anchoredPosition = new Vector2(-5, 0);
        ImageOf(track, new Color(.5f, .4f, .26f, .12f));
        RectTransform handleRect = Rect("Handle", track); Stretch(handleRect);
        Image handle = ImageOf(handleRect, new Color(.5f, .35f, .22f, .65f));
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = handleRect; scrollbar.targetGraphic = handle;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private static Text Section(Transform content, string name, string title)
    {
        RectTransform heading = Rect(name + "Heading", content);
        Text label = heading.gameObject.AddComponent<Text>(); label.font = Body; label.fontSize = 23; label.color = Accent;
        label.text = title; label.raycastTarget = false;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
        RectTransform body = Rect(name, content);
        Text text = body.gameObject.AddComponent<Text>(); text.font = Body; text.fontSize = 26; text.color = Ink;
        text.supportRichText = false; text.lineSpacing = 1.25f; text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    private static void Region(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Stretch(RectTransform rect, float x = 0, float y = 0)
    { Region(rect, Vector2.zero, Vector2.one); rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
    private static Image ImageOf(RectTransform rect, Color color)
    { Image image = rect.gameObject.AddComponent<Image>(); image.color = color; return image; }
    private static Text TextRegion(Transform parent, string name, string value, int size, Color color, Vector2 min, Vector2 max)
    {
        RectTransform rect = Rect(name, parent); Region(rect, min, max);
        Text text = rect.gameObject.AddComponent<Text>(); text.text = value; text.font = Body; text.fontSize = size;
        text.color = color; text.raycastTarget = false; text.supportRichText = false; text.alignment = TextAnchor.MiddleLeft;
        return text;
    }
    private static Button ButtonAt(Transform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        Image image = ImageOf(rect, new Color(.83f, .74f, .57f));
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        TextRegion(rect, "Label", text, 24, Ink, Vector2.zero, Vector2.one).alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private static void Bind(UnityEngine.Object target, params object[] pairs)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2) data.FindProperty((string)pairs[i]).objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/人物志/验证人物档案")]
    public static void Validate()
    {
        Directory.CreateDirectory(Output);
        Scene scene = EditorSceneManager.NewPreviewScene();
        var temporary = new List<UnityEngine.Object>();
        try
        {
            GameObject canvasObject = new GameObject("JournalPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = 5;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            instance.transform.SetParent(canvas.transform, false);
            CompanionJournal journal = instance.GetComponent<CompanionJournal>();
            typeof(CompanionJournal).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(journal, null);
            var reads = new HashSet<string>();
            journal.Initialize(id => id == 2, (data, section, index) => reads.Contains(DialogueHistory.Key(data, section, index)));
            Require(journal.Profiles.Length == 4, "Expected existing four character slots");
            journal.OpenList();
            Button[] items = Get<Transform>(journal, "listContent").GetComponentsInChildren<Button>();
            Require(items.Length == 4 && items[0].interactable && !items[1].interactable, "Met/unmet button states incorrect");
            journal.OpenProfile(journal.Profiles[1]);
            Require(!journal.IsDetailOpen, "Unmet profile must reject direct open");
            BrickRecordTools.Render(canvas, scene, "list.png", 1920, 1080, Output);
            items[0].onClick.Invoke();
            Require(journal.IsDetailOpen, "Met list click must open profile");
            Require(Get<Text>(journal, "identity").text == journal.Profiles[0].identity, "Identity must render from SO");
            Require(Get<Text>(journal, "dialogueHistory").text.StartsWith("尚无"), "Unread dialogue must not leak");
            DialogueData source = journal.Profiles[0].relatedDialogues[0];
            reads.Add(DialogueHistory.Key(source, DialogueSection.Greeting, 0));
            reads.Add(DialogueHistory.Key(source, DialogueSection.Task, 0));
            journal.OpenProfile(journal.Profiles[0]);
            string history = Get<Text>(journal, "dialogueHistory").text;
            Require(history.Contains(source.greetingLines[0]) && history.Contains(source.taskLines[0]), "Read dialogue missing");
            Require(!history.Contains(source.scienceLines[0]), "Unread science branch leaked");
            Require(Get<ScrollRect>(journal, "detailScroll").verticalNormalizedPosition >= .99f, "Detail must scroll to top");
            BrickRecordTools.Render(canvas, scene, "detail.png", 1920, 1080, Output);
            Get<ScrollRect>(journal, "detailScroll").verticalNormalizedPosition = 0;
            BrickRecordTools.Render(canvas, scene, "dialogue-history.png", 1920, 1080, Output);
            BrickRecordTools.Render(canvas, scene, "detail-wide.png", 2400, 1080, Output);
            journal.BackToList(); Require(!journal.IsDetailOpen && journal.gameObject.activeSelf, "Back should keep list open");
            journal.Close(); Require(!journal.gameObject.activeSelf, "Close failed");

            DialogueData test = ScriptableObject.CreateInstance<DialogueData>(); temporary.Add(test);
            test.historyId = "isolated-test"; test.greetingLines = new[] { "原文" };
            string oldKey = DialogueHistory.Key(test, DialogueSection.Greeting, 0);
            test.greetingLines[0] = "修订原文";
            Require(oldKey != DialogueHistory.Key(test, DialogueSection.Greeting, 0), "Revised dialogue must not inherit read flag");
            Require(DialogueHistory.Key(test, DialogueSection.Greeting, -1) == null, "Invalid index guard failed");
            CompanionProfile fixture = ScriptableObject.CreateInstance<CompanionProfile>(); temporary.Add(fixture);
            fixture.relatedDialogues = new[] { test, test, null };
            test.isCoreNPC = false; test.minorNPCLines = new[] { "路人的已读见闻", "尚未交谈的内容" };
            string minorHistory = CompanionJournal.BuildHistory(fixture, (data, section, index) => index == 0);
            Require(minorHistory.Contains(test.minorNPCLines[0]) && !minorHistory.Contains(test.minorNPCLines[1]) &&
                !minorHistory.Contains(test.greetingLines[0]), "Minor NPC review filtering failed");
            Require(minorHistory.IndexOf(test.minorNPCLines[0], StringComparison.Ordinal) ==
                minorHistory.LastIndexOf(test.minorNPCLines[0], StringComparison.Ordinal), "Duplicate SO references must not duplicate dialogue");

            // 验证打字机完成回调和中断清理，不触碰玩家存档。
            GameObject uiObject = new GameObject("DialogueCallbackTest", typeof(RectTransform)); SceneManager.MoveGameObjectToScene(uiObject, scene);
            uiObject.transform.SetParent(canvas.transform, false);
            DialogueUI ui = uiObject.AddComponent<DialogueUI>();
            ui.dialogueText = uiObject.AddComponent<Text>(); ui.dialogueText.font = Body;
            int revealCount = 0;
            ui.ShowDialogueLine("读完一句", () => revealCount++);
            ui.ShowFullText(); ui.ShowFullText();
            Require(revealCount == 1, "Skip typewriter must mark read exactly once");
            ui.ShowDialogueLine("中断的句子", () => revealCount++);
            ui.HidePanel();
            ui.ShowFullText();
            Require(revealCount == 1, "Interrupted dialogue must not be marked read");
            System.Collections.IEnumerator typing = (System.Collections.IEnumerator)typeof(DialogueUI)
                .GetMethod("TypeTextCoroutine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, new object[] { "自然读完" });
            typeof(DialogueUI).GetField("lineRevealed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ui, (Action)(() => revealCount++));
            while (typing.MoveNext()) { }
            Require(revealCount == 2, "Natural typewriter completion must reveal the line");
            ValidateMenuIntegration();
            File.WriteAllText(Output + "/validation.txt", "PASS\nFour SO profiles and runtime prefab\nMet buttons enabled, unmet buttons disabled and guarded\nList click opens SO-driven identity and experience\nOnly read dialogue lines visible; unread branches hidden\nMinor NPC sections and duplicate SO references\nBack/close hierarchy and scroll reset\nText revision invalidates stale read key\nTypewriter natural/skip callback once; interrupted line not recorded\nActual Index scene menu initialization and portrait click routing\n1920x1080 and 2400x1080 layout renders\nNo player save data read or written\n");
            Debug.Log("[CompanionJournal] Validation PASS");
        }
        finally
        {
            foreach (var item in temporary) UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void ValidateMenuIntegration()
    {
        Require(DataSystem.instance == null, "Run validation outside Play mode");
        Scene previousScene = SceneManager.GetActiveScene();
        const string previewPath = "Assets/Project/Resources/CompanionMenuValidation.unity";
        File.Copy("Assets/Project/Scenes/Index.unity", previewPath, true);
        AssetDatabase.ImportAsset(previewPath);
        Scene scene = EditorSceneManager.OpenScene(previewPath, OpenSceneMode.Additive);
        try
        {
            IndexMainMenu menu = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                menu = root.GetComponentInChildren<IndexMainMenu>(true);
                if (menu != null) break;
            }
            Require(menu != null, "Index menu missing");
            typeof(IndexMainMenu).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
            CompanionJournal journal = Get<CompanionJournal>(menu, "companionJournal");
            Require(journal != null, "Index must load the configured journal prefab");
            journal.Initialize(id => id == 2, (data, section, index) => false);
            menu.OpenCompanion(2);
            Require(!journal.gameObject.activeSelf, "Index must reject locked portrait");
            menu.companions[0].portrait.GetComponent<Button>().onClick.Invoke();
            Require(journal.IsDetailOpen && journal.gameObject.activeSelf, "Index portrait click must open profile");
            menu.CloseWindows();
            Require(!journal.gameObject.activeSelf, "Menu must close profile when switching modals");
            menu.OpenJournal();
            Require(journal.gameObject.activeSelf && !journal.IsDetailOpen, "Journal menu button must show new list");
            menu.OpenSettings();
            Require(!journal.gameObject.activeSelf && menu.settingsWindow.activeSelf, "Settings must not overlap the journal");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            AssetDatabase.DeleteAsset(previewPath);
        }
    }
    private static T Get<T>(object target, string field) where T : class => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target) as T;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
