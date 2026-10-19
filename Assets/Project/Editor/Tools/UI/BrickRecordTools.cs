using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>构建可编辑的画像砖详情页，并提供不读写玩家存档的独立检查。</summary>
public static class BrickRecordTools
{
    private const string PrefabPath = "Assets/Project/Prefabs/RecordUI.prefab";
    private static readonly Color Ink = new Color(.22f, .19f, .15f);
    private static readonly Color Muted = new Color(.43f, .38f, .30f);
    private static readonly Color Paper = new Color(.94f, .89f, .77f);
    private static readonly Color Accent = new Color(.47f, .24f, .17f);
    private static Font BodyFont => AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsYaHei.ttf");
    private static Font TitleFont => AssetDatabase.LoadAssetAtPath<Font>("Assets/Project/Fonts/SettingsKaiTi.ttf");

    [InitializeOnLoadMethod]
    private static void RunRequestedUpgrade()
    {
        const string request = "Artifacts/BrickRecordUpgrade.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(request);
            try { Upgrade(); Validate(); }
            catch (Exception exception)
            {
                File.WriteAllText("Artifacts/BrickRecord-validation-error.txt", exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/画像砖录/验证详情页")]
    public static void Validate()
    {
        Directory.CreateDirectory("Artifacts/BrickRecord");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject canvasObject = new GameObject("BrickRecordValidationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 5f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            root.transform.SetParent(canvasObject.transform, false);
            BrickDetailView detail = root.GetComponentInChildren<BrickDetailView>(true);
            BrickImageViewer viewer = root.GetComponentInChildren<BrickImageViewer>(true);
            Require(detail != null && viewer != null, "Missing detail/viewer");
            Invoke(detail, "Awake"); Invoke(viewer, "Awake");
            var windowData = new SerializedObject(root.GetComponent<RecordWindow>());
            var records = windowData.FindProperty("bricks");
            Require(records.arraySize == 5, "Expected five existing records");
            // 直接使用 prefab 资料，验证全部条目均能打开，无需修改玩家解锁记录。
            for (int i = 0; i < records.arraySize; i++)
            {
                var record = records.GetArrayElementAtIndex(i);
                string name = record.FindPropertyRelative("displayName").stringValue;
                Sprite sprite = (Sprite)record.FindPropertyRelative("sprite").objectReferenceValue;
                Require(sprite != null, "Missing sprite: " + name);
                detail.Open(name, sprite, String(record, "imageCaption"), String(record, "era"),
                    String(record, "excavationSite"), String(record, "currentCollection"),
                    String(record, "interpretation"), String(record, "sources"));
                Require(detail.gameObject.activeSelf, "Detail did not open");
                if (i == 0)
                {
                    Render(canvas, scene, "detail-1920.png", 1920, 1080);
                    detail.OpenImage();
                    Render(canvas, scene, "image-fit-1920.png", 1920, 1080);
                    for (int step = 0; step < 12; step++) viewer.ZoomIn();
                    Require(Get<Text>(viewer, "zoomLabel").text == "400%", "Maximum zoom must clamp to 400%");
                    Require(Get<Image>(viewer, "artwork").rectTransform.anchoredPosition.sqrMagnitude < .01f,
                        "Zoom from fit should preserve image center");
                    RectTransform artworkRect = Get<Image>(viewer, "artwork").rectTransform;
                    ScrollRect imageScroll = Get<ScrollRect>(viewer, "imageScroll");
                    Require(imageScroll.movementType == ScrollRect.MovementType.Clamped, "Image drag must clamp to viewport");
                    artworkRect.anchoredPosition = new Vector2(80, 60);
                    viewer.ZoomOut();
                    Require(artworkRect.anchoredPosition.x > 0 && artworkRect.anchoredPosition.x < 80,
                        "Zoom should preserve the current panned region");
                    viewer.ResetZoom();
                    Require(artworkRect.anchoredPosition.sqrMagnitude < .01f, "Fit must recenter a panned artwork");
                    for (int step = 0; step < 6; step++) viewer.ZoomIn();
                    Render(canvas, scene, "image-zoom-1920.png", 1920, 1080);
                    for (int step = 0; step < 12; step++) viewer.ZoomOut();
                    Require(Get<Text>(viewer, "zoomLabel").text == "100%", "Minimum zoom must clamp to 100%");
                    viewer.Close();
                    Require(detail.gameObject.activeSelf && !viewer.gameObject.activeSelf, "Image back should keep detail open");
                    ScrollRect scroll = Get<ScrollRect>(detail, "informationScroll");
                    scroll.verticalNormalizedPosition = 0f;
                    Render(canvas, scene, "detail-sources-1920.png", 1920, 1080);
                    Render(canvas, scene, "detail-wide-2400.png", 2400, 1080);
                }
                detail.OpenImage();
                Require(viewer.gameObject.activeSelf, "Image did not open: " + name);
                Require(Get<Text>(viewer, "zoomLabel").text == "100%", "Opening a new image should reset zoom");
                detail.Close();
                Require(!viewer.gameObject.activeSelf && !detail.gameObject.activeSelf, "Closing detail should close viewer");
            }
            // 长文与缺图回退，确保不会沿用上一条资料。
            detail.Open("缺图条目", null, null, null, null, null, new string('文', 2000), null);
            Require(!Get<Button>(detail, "enlargeButton").interactable, "Missing artwork must disable enlarge");
            Require(Get<Text>(detail, "era").text == "待考证", "Empty field fallback failed");
            Require(Get<ScrollRect>(detail, "informationScroll").verticalNormalizedPosition >= .99f, "New detail must scroll to top");
            Require(Get<ScrollRect>(detail, "informationScroll").verticalScrollbar != null, "Information scrollbar missing");
            // 验证真实列表入口的事件绑定，避免只测试直接调用详情页。
            LevelRecordItem item = UnityEngine.Object.Instantiate(root.GetComponentInChildren<LevelRecordItem>(true), canvasObject.transform);
            bool clicked = false;
            item.SetOpenAction(() => clicked = true);
            item.gameObject.SetActive(true);
            item.GetComponent<Button>().onClick.Invoke();
            Require(clicked && item.GetComponent<Image>().raycastTarget, "List item click binding failed");
            File.WriteAllText("Artifacts/BrickRecord/validation.txt", "PASS\nFive records and image references\nDetail open and nested close\nZoom clamps 100%-400%, preserves center/pan and resets per artwork\nMissing image and metadata fallback\nScroll reset, visible scrollbar and long text\nList button event binding\nFive Unity-rendered layout captures\nNo player save data read or written\nTouch gestures require device testing\n");
            Debug.Log("[BrickRecord] Validation PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static string String(SerializedProperty record, string field) => record.FindPropertyRelative(field).stringValue;
    private static T Get<T>(object target, string field) where T : class =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target) as T;
    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Render(Canvas canvas, Scene scene, string name, int width, int height,
        string outputDirectory = "Artifacts/BrickRecord")
    {
        GameObject cameraObject = new GameObject("BrickRecordCaptureCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.1f, .08f, .05f);
        camera.orthographic = true; camera.orthographicSize = height / 2f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        RenderTexture texture = new RenderTexture(width, height, 24);
        camera.targetTexture = texture;
        canvas.worldCamera = camera;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
        Canvas.ForceUpdateCanvases();
        foreach (Text text in canvas.GetComponentsInChildren<Text>())
            if (text.font != null) text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
        Canvas.ForceUpdateCanvases();
        RenderPipelineAsset graphicsPipeline = GraphicsSettings.defaultRenderPipeline;
        RenderPipelineAsset qualityPipeline = QualitySettings.renderPipeline;
        RenderTexture previous = RenderTexture.active;
        Texture2D output = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            camera.Render();
            RenderTexture.active = texture;
            output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            output.Apply();
            File.WriteAllBytes(Path.Combine(outputDirectory, name), output.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            GraphicsSettings.defaultRenderPipeline = graphicsPipeline;
            QualitySettings.renderPipeline = qualityPipeline;
            camera.targetTexture = null; canvas.worldCamera = null;
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    public static void Upgrade()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RecordWindow window = root.GetComponent<RecordWindow>();
            var data = new SerializedObject(window);
            SeedRecords(data.FindProperty("bricks"));
            if (root.GetComponentInChildren<BrickDetailView>(true) == null)
            {
                BrickDetailView detail = BuildDetail(root.transform);
                data.FindProperty("detailView").objectReferenceValue = detail;
            }
            AddInformationScrollbar(root.GetComponentInChildren<BrickDetailView>(true));
            data.ApplyModifiedPropertiesWithoutUndo();
            var listTitle = root.transform.Find("Panel/Title");
            if (listTitle != null) listTitle.GetComponent<Text>().text = "画像砖录";
            // 模板始终隐藏；复制后由 RecordWindow 显示并绑定对应资料。
            LevelRecordItem template = root.GetComponentInChildren<LevelRecordItem>(true);
            Image background = template.GetComponent<Image>();
            background.color = new Color(.93f, .86f, .69f);
            background.raycastTarget = true;
            Button button = template.GetComponent<Button>() ?? template.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, .94f, .82f);
            colors.pressedColor = new Color(.83f, .73f, .56f);
            button.colors = colors;
            if (template.transform.Find("DetailHint") == null)
            {
                RectTransform hint = Rect("DetailHint", template.transform);
                Place(hint, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 18), new Vector2(0, 22));
                Label(hint, "Text", "点击查看砖画与文物资料", 18, Muted, TextAnchor.MiddleCenter);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BrickRecord] 详情页和五条资料已写入 RecordUI。 ");
    }

    private static void AddInformationScrollbar(BrickDetailView detail)
    {
        ScrollRect scroll = Get<ScrollRect>(detail, "informationScroll");
        if (scroll.verticalScrollbar != null) return;
        scroll.viewport.offsetMax = new Vector2(-20, 0);
        RectTransform track = Rect("VerticalScrollbar", scroll.transform);
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1, .5f); track.sizeDelta = new Vector2(10, -20);
        track.anchoredPosition = new Vector2(-5, 0);
        Image background = ImageOf(track, new Color(.5f, .4f, .26f, .12f));
        RectTransform handleRect = Rect("Handle", track);
        Fill(handleRect);
        Image handle = ImageOf(handleRect, new Color(.5f, .35f, .22f, .65f));
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private static BrickDetailView BuildDetail(Transform parent)
    {
        RectTransform overlay = Rect("BrickDetail", parent);
        Fill(overlay);
        ImageOf(overlay, new Color(.06f, .05f, .04f, .92f));
        BrickDetailView detail = overlay.gameObject.AddComponent<BrickDetailView>();
        RectTransform panel = Rect("PaperPanel", overlay);
        Fill(panel, 70, 48);
        ImageOf(panel, Paper);
        RectTransform eyebrow = Rect("Eyebrow", panel);
        Place(eyebrow, new Vector2(.04f, 1), new Vector2(.8f, 1), new Vector2(0, -34), new Vector2(0, 28));
        Label(eyebrow, "Text", "画 像 砖 录   /   文 物 资 料", 20, Accent);
        RectTransform heading = Rect("Title", panel);
        Place(heading, new Vector2(.04f, 1), new Vector2(.8f, 1), new Vector2(0, -88), new Vector2(0, 66));
        Text title = Label(heading, "Text", "《耕种图》", 52, Ink);
        title.font = TitleFont;
        Button close = ButtonAt(panel, "BackToRecords", "返回砖录", new Vector2(1, 1), new Vector2(-142, -64), new Vector2(210, 60));

        RectTransform imagePane = Rect("ArtworkPanel", panel);
        Region(imagePane, new Vector2(.04f, .18f), new Vector2(.48f, .79f));
        ImageOf(imagePane, new Color(.20f, .18f, .14f));
        Image artwork = ImageOf(Rect("Artwork", imagePane), Color.white);
        Fill(artwork.rectTransform, 24, 24);
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;
        Button enlargeImage = imagePane.gameObject.AddComponent<Button>();
        enlargeImage.targetGraphic = imagePane.GetComponent<Image>();
        Navigation noNavigation = new Navigation { mode = Navigation.Mode.None };
        enlargeImage.navigation = noNavigation;
        RectTransform captionRect = Rect("ImageCaption", panel);
        Region(captionRect, new Vector2(.04f, .12f), new Vector2(.48f, .17f));
        Text caption = Label(captionRect, "Text", "游戏复原图", 21, Muted, TextAnchor.MiddleCenter);
        RectTransform enlargeHint = Rect("EnlargeHint", panel);
        Region(enlargeHint, new Vector2(.04f, .065f), new Vector2(.48f, .115f));
        Label(enlargeHint, "Text", "点击砖画放大查看", 24, Accent, TextAnchor.MiddleCenter);

        RectTransform scrollRoot = Rect("InformationScroll", panel);
        Region(scrollRoot, new Vector2(.52f, .065f), new Vector2(.96f, .80f));
        ImageOf(scrollRoot, new Color(1, 1, 1, .15f));
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36;
        RectTransform viewport = Rect("Viewport", scrollRoot);
        Fill(viewport);
        ImageOf(viewport, new Color(1, 1, 1, .001f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 28, 12, 28);
        layout.spacing = 14;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Text era = Field(content, "Era", "年代", "魏晋时期（题材年代）");
        Text site = Field(content, "ExcavationSite", "出土地", "具体原砖出土信息待考证");
        Text collection = Field(content, "CurrentCollection", "现藏地", "待考证");
        Text interpretation = Field(content, "Interpretation", "图像解读", "");
        Text sources = Field(content, "Sources", "资料来源", "");
        scroll.viewport = viewport; scroll.content = content;

        BrickImageViewer viewer = BuildViewer(overlay);
        Bind(detail, "title", title, "artwork", artwork, "imageCaption", caption,
            "era", era, "excavationSite", site, "currentCollection", collection,
            "interpretation", interpretation, "sources", sources, "informationScroll", scroll,
            "closeButton", close, "enlargeButton", enlargeImage, "imageViewer", viewer);
        overlay.gameObject.SetActive(false);
        return detail;
    }

    private static BrickImageViewer BuildViewer(Transform parent)
    {
        RectTransform overlay = Rect("BrickImageViewer", parent);
        Fill(overlay);
        ImageOf(overlay, new Color(.08f, .07f, .05f, 1));
        BrickImageViewer viewer = overlay.gameObject.AddComponent<BrickImageViewer>();
        ScrollRect scroll = overlay.gameObject.AddComponent<ScrollRect>();
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        // 滚轮由同一物体上的 BrickImageViewer 解释为缩放。
        scroll.scrollSensitivity = 0;
        RectTransform viewport = Rect("Viewport", overlay);
        Region(viewport, new Vector2(.045f, .16f), new Vector2(.955f, .83f));
        ImageOf(viewport, new Color(.14f, .12f, .09f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform imageRect = Rect("Artwork", viewport);
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(.5f, .5f);
        Image artwork = ImageOf(imageRect, Color.white);
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;
        scroll.viewport = viewport; scroll.content = imageRect;
        RectTransform heading = Rect("Title", overlay);
        Region(heading, new Vector2(.05f, .87f), new Vector2(.65f, .96f));
        Text title = Label(heading, "Text", "", 38, Paper);
        title.font = TitleFont;
        RectTransform captionRect = Rect("Caption", overlay);
        Region(captionRect, new Vector2(.05f, .835f), new Vector2(.65f, .875f));
        Text caption = Label(captionRect, "Text", "", 20, new Color(.72f, .66f, .55f));
        Button close = ButtonAt(overlay, "BackToDetail", "返回资料", new Vector2(1, 1), new Vector2(-180, -80), new Vector2(210, 60));
        Button minus = ButtonAt(overlay, "ZoomOut", "缩小 −", new Vector2(.5f, 0), new Vector2(-310, 85), new Vector2(180, 60));
        Button plus = ButtonAt(overlay, "ZoomIn", "放大 ＋", new Vector2(.5f, 0), new Vector2(0, 85), new Vector2(180, 60));
        Button reset = ButtonAt(overlay, "ResetZoom", "适合画面", new Vector2(.5f, 0), new Vector2(270, 85), new Vector2(210, 60));
        RectTransform percent = Rect("ZoomPercent", overlay);
        Place(percent, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-157, 85), new Vector2(110, 60));
        Text zoomLabel = Label(percent, "Text", "100%", 24, Paper, TextAnchor.MiddleCenter);
        RectTransform hint = Rect("ControlsHint", overlay);
        Region(hint, new Vector2(.05f, .015f), new Vector2(.95f, .05f));
        Label(hint, "Text", "拖动查看细节 · 滚轮或双指缩放 · Esc 返回", 20, new Color(.72f, .66f, .55f), TextAnchor.MiddleCenter);
        Bind(viewer, "artwork", artwork, "imageScroll", scroll, "title", title, "caption", caption,
            "zoomLabel", zoomLabel, "zoomInButton", plus, "zoomOutButton", minus,
            "resetButton", reset, "closeButton", close);
        overlay.gameObject.SetActive(false);
        return viewer;
    }

    private static void SeedRecords(SerializedProperty records)
    {
        string[] names = { "耕种图", "庖厨图", "驿使图", "四神图", "墓主图" };
        string[] interpretations =
        {
            "画面围绕牛、犁与耕作者展开，将人物动作和农具组织为连续的劳作场景。观察牛与犁的连接、人物的姿态，可以理解砖画如何用简练的线条表现生产活动。\n\n本作以这一题材构建农耕生活的想象。画中具体农具名称、耕作方式及其历史解释，仍需结合对应原砖与考古资料核对。",
            "庖厨题材描绘食材处理、烹饪与备餐等生活活动。人物的手势、器具的摆放和动作之间的联系，共同构成一幅日常劳作图景。\n\n本作借此呈现饮食生活。具体食材、器物名称和礼仪含义，需要结合对应原砖与史料辨认，不能仅凭游戏画面确定。",
            "驿使题材以人物与马匹的组合表现行旅和传递消息的活动。人物朝向、马匹姿态与简洁的构图，使静态砖画产生鲜明的行进感。\n\n本作将这一题材转化为交通与交流的叙事。具体人物身份、驿传制度及年代，需依据对应原砖资料进一步说明。",
            "四神题材以青龙、白虎、朱雀、玄武等传统形象表达方位观念与象征意义。观看时可以留意形体、姿态及彼此的排列关系。\n\n本作的画面属于题材改编，不代表四种形象出自同一块实物砖。具体图像来源、组合方式及墓葬语境需逐项考证。",
            "墓主生活题材常借人物、车马与仪仗等形象组织叙事，呈现墓葬图像中的生活想象。观看时可以留意人物大小、位置和行进方向如何建立画面秩序。\n\n本作以此营造巡游与告别的情境。具体墓主身份、社会等级及仪式含义，不能由游戏改编图直接推定。"
        };
        for (int i = 0; i < records.arraySize; i++)
        {
            SerializedProperty record = records.GetArrayElementAtIndex(i);
            if (i >= names.Length) continue;
            record.FindPropertyRelative("displayName").stringValue = names[i];
            Default(record, "imageCaption", "游戏复原图 · 依据画像砖题材改编");
            Default(record, "era", "魏晋时期（题材年代）");
            Default(record, "excavationSite", "具体原砖出土信息待考证");
            Default(record, "currentCollection", "待考证");
            Default(record, "interpretation", interpretations[i]);
            Default(record, "sources", "题材依据：本作《尘封砖语》设计方案及开题报告中的河西魏晋画像砖选题。\n\n当前展示为游戏复原图。对应文物的编号、正式图录页码与馆藏资料尚待核验，具体年代、出土地和现藏地以正式资料为准。");
        }
    }

    private static void Default(SerializedProperty record, string field, string value)
    {
        SerializedProperty property = record.FindPropertyRelative(field);
        if (string.IsNullOrWhiteSpace(property.stringValue)) property.stringValue = value;
    }

    private static Text Field(Transform parent, string name, string heading, string value)
    {
        RectTransform header = Rect(name + "Label", parent);
        Text label = Label(header, "Text", heading, 22, Accent);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
        // 文字本身作为布局元素，让长资料按可用宽度自然换行。
        RectTransform body = Rect(name, parent);
        Text text = body.gameObject.AddComponent<Text>();
        text.font = BodyFont; text.fontSize = 26; text.color = Ink;
        text.text = value; text.raycastTarget = false; text.supportRichText = false;
        text.lineSpacing = 1.25f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Fill(RectTransform rect, float horizontal = 0, float vertical = 0)
    {
        Region(rect, Vector2.zero, Vector2.one);
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void Region(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    private static Image ImageOf(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text Label(RectTransform parent, string name, string value, int size, Color color,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RectTransform rect = Rect(name, parent);
        Fill(rect);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = BodyFont; text.fontSize = size; text.text = value; text.color = color;
        text.alignment = alignment; text.raycastTarget = false; text.supportRichText = false;
        return text;
    }

    private static Button ButtonAt(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, anchor, position, size);
        Image image = ImageOf(rect, new Color(.83f, .74f, .57f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(rect, "Text", value, 24, Ink, TextAnchor.MiddleCenter);
        return button;
    }

    private static void Bind(UnityEngine.Object target, params object[] pairs)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
            data.FindProperty((string)pairs[i]).objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
