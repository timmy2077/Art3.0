// ============================================================
//  GameViewScreenshotTool.cs
//  Unity 编辑器扩展 —— 截取当前 Game 视图画面
//
//  安装：把整个 Editor 文件夹拖进任意 Unity 项目的 Assets/ 下即可
//  打开：菜单栏 Tools → 截图工具 → 打开面板
//  快截：Ctrl + Shift + S (Windows) / Cmd + Shift + S (Mac)
//
//  兼容：Unity 2018.4 / 2019 / 2020 / 2021 / 2022 / Unity 6 (内置 / URP / HDRP)
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEditorTools
{
    public class GameViewScreenshotTool : EditorWindow
    {
        // ---------------- 枚举 ----------------
        private enum CaptureMode { GameView, CameraRender }
        private enum OutputFormat { PNG, JPG }

        // ---------------- 常量 ----------------
        private const string PrefsPrefix = "GVS_";
        private const int HistoryLimit = 8;
        private const string MenuRoot = "Tools/截图工具/";

        // ---------------- 设置（持久化到 EditorPrefs）----------------
        private CaptureMode mode = CaptureMode.GameView;
        private string saveFolder;
        private string filePrefix = "GameView";
        private OutputFormat format = OutputFormat.PNG;
        private int jpgQuality = 90;
        private int superSize = 1;                 // 超采样倍数（仅相机渲染 / 运行时截屏生效）
        private bool useGameViewSize = true;       // 相机渲染模式：跟随 Game 视图尺寸
        private int customWidth = 1920;
        private int customHeight = 1080;
        private bool transparent = false;          // 透明背景（仅相机渲染生效，且强制 PNG）
        private bool flipY = true;                 // 垂直翻转（GameView 直读模式常见需要）
        private bool colorCorrection = true;       // Linear → sRGB 校正
        private bool revealAfterSave = true;       // 截图后定位到文件
        private bool autoTimestamp = true;         // 文件名追加时间戳

        // ---------------- 运行时状态 ----------------
        private Camera targetCamera;
        private Vector2 scrollPos;
        private Texture2D previewTex;
        private string lastSavedPath = "";
        private string lastInfo = "还没有截过图";
        private readonly List<string> history = new List<string>();
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;

        // ============================================================
        //  菜单入口
        // ============================================================
        [MenuItem(MenuRoot + "打开面板", false, 100)]
        public static void ShowWindow()
        {
            var win = GetWindow<GameViewScreenshotTool>(false, "Game 截图工具", true);
            win.minSize = new Vector2(360, 520);
            win.Show();
        }

        [MenuItem(MenuRoot + "快速截图 %#s", false, 101)]
        public static void QuickCapture()
        {
            // 用临时实例执行，不弹出窗口
            var win = CreateInstance<GameViewScreenshotTool>();
            win.hideFlags = HideFlags.HideAndDontSave;
            win.LoadPrefs();
            win.DoCapture();
            Debug.Log("[截图工具] 已保存：" + win.lastSavedPath);
            DestroyImmediate(win);
        }

        [MenuItem(MenuRoot + "打开保存目录", false, 120)]
        public static void OpenSaveFolder()
        {
            string folder = EditorPrefs.GetString(PrefsPrefix + "SaveFolder", "");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                folder = DefaultFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        // ============================================================
        //  生命周期
        // ============================================================
        private void OnEnable()
        {
            LoadPrefs();
            titleContent = new GUIContent("Game 截图工具", "截取当前 Game 视图画面");
        }

        private void OnDisable()
        {
            SavePrefs();
            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }
        }

        private static string DefaultFolder()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
        }

        // ---------------- 设置存取 ----------------
        private void LoadPrefs()
        {
            saveFolder = EditorPrefs.GetString(PrefsPrefix + "SaveFolder", DefaultFolder());
            filePrefix = EditorPrefs.GetString(PrefsPrefix + "Prefix", "GameView");
            format = (OutputFormat)EditorPrefs.GetInt(PrefsPrefix + "Format", 0);
            jpgQuality = EditorPrefs.GetInt(PrefsPrefix + "JpgQuality", 90);
            superSize = EditorPrefs.GetInt(PrefsPrefix + "SuperSize", 1);
            useGameViewSize = EditorPrefs.GetBool(PrefsPrefix + "UseGameViewSize", true);
            customWidth = EditorPrefs.GetInt(PrefsPrefix + "CustomWidth", 1920);
            customHeight = EditorPrefs.GetInt(PrefsPrefix + "CustomHeight", 1080);
            transparent = EditorPrefs.GetBool(PrefsPrefix + "Transparent", false);
            colorCorrection = EditorPrefs.GetBool(PrefsPrefix + "ColorCorrection", true);
            revealAfterSave = EditorPrefs.GetBool(PrefsPrefix + "Reveal", true);
            autoTimestamp = EditorPrefs.GetBool(PrefsPrefix + "AutoTime", true);
            mode = (CaptureMode)EditorPrefs.GetInt(PrefsPrefix + "Mode", 0);

            // 未手动设置过翻转时，按平台自动判断（D3D 类平台通常需要翻转）
            if (!EditorPrefs.HasKey(PrefsPrefix + "FlipY"))
                flipY = SystemInfo.graphicsUVStartsAtTop;
            else
                flipY = EditorPrefs.GetBool(PrefsPrefix + "FlipY", true);

            string h = EditorPrefs.GetString(PrefsPrefix + "History", "");
            history.Clear();
            if (!string.IsNullOrEmpty(h))
                foreach (var p in h.Split('|'))
                    if (!string.IsNullOrEmpty(p)) history.Add(p);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefsPrefix + "SaveFolder", saveFolder);
            EditorPrefs.SetString(PrefsPrefix + "Prefix", filePrefix);
            EditorPrefs.SetInt(PrefsPrefix + "Format", (int)format);
            EditorPrefs.SetInt(PrefsPrefix + "JpgQuality", jpgQuality);
            EditorPrefs.SetInt(PrefsPrefix + "SuperSize", superSize);
            EditorPrefs.SetBool(PrefsPrefix + "UseGameViewSize", useGameViewSize);
            EditorPrefs.SetInt(PrefsPrefix + "CustomWidth", customWidth);
            EditorPrefs.SetInt(PrefsPrefix + "CustomHeight", customHeight);
            EditorPrefs.SetBool(PrefsPrefix + "Transparent", transparent);
            EditorPrefs.SetBool(PrefsPrefix + "FlipY", flipY);
            EditorPrefs.SetBool(PrefsPrefix + "ColorCorrection", colorCorrection);
            EditorPrefs.SetBool(PrefsPrefix + "Reveal", revealAfterSave);
            EditorPrefs.SetBool(PrefsPrefix + "AutoTime", autoTimestamp);
            EditorPrefs.SetInt(PrefsPrefix + "Mode", (int)mode);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < history.Count && i < HistoryLimit; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(history[i]);
            }
            EditorPrefs.SetString(PrefsPrefix + "History", sb.ToString());
        }

        // ============================================================
        //  GUI
        // ============================================================
        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Game 视图截图工具", titleStyle);
            EditorGUILayout.Space(2);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // ---- 捕获模式 ----
            EditorGUILayout.LabelField("捕获方式", EditorStyles.boldLabel);
            int modeIdx = GUILayout.Toolbar((int)mode, new[] { "Game 视图", "相机渲染" });
            if (modeIdx != (int)mode) { mode = (CaptureMode)modeIdx; SavePrefs(); }
            EditorGUILayout.HelpBox(
                mode == CaptureMode.GameView
                    ? "直接读取 Game 视图当前的渲染结果，所见即所得。\n需要超高清或透明底，请用「相机渲染」。"
                    : "用指定相机重新渲染一张，可超采样 2x/3x/4x、可抠透明底。",
                MessageType.None);

            EditorGUILayout.Space(6);

            // ---- 分辨率 ----
            EditorGUILayout.LabelField("分辨率", EditorStyles.boldLabel);
            if (mode == CaptureMode.GameView)
            {
                GUI.enabled = false;
                EditorGUILayout.IntSlider("超采样倍数", 1, 1, 4);
                GUI.enabled = true;
                EditorGUILayout.HelpBox("Game 视图模式固定为视图原始分辨率；要超采样请切到「相机渲染」。", MessageType.Info);
            }
            else
            {
                superSize = EditorGUILayout.IntPopup("超采样倍数", superSize,
                    new[] { "1x", "2x", "3x", "4x" }, new[] { 1, 2, 3, 4 });

                useGameViewSize = EditorGUILayout.Toggle("跟随 Game 视图尺寸", useGameViewSize);
                if (!useGameViewSize)
                {
                    EditorGUILayout.BeginHorizontal();
                    customWidth = EditorGUILayout.IntField("宽", customWidth);
                    customHeight = EditorGUILayout.IntField("高", customHeight);
                    EditorGUILayout.EndHorizontal();
                    if (GUILayout.Button("填入 1920x1080"))
                    { customWidth = 1920; customHeight = 1080; }
                }
                customWidth = Mathf.Max(1, customWidth);
                customHeight = Mathf.Max(1, customHeight);

                targetCamera = (Camera)EditorGUILayout.ObjectField(
                    new GUIContent("相机", "留空自动找 Camera.main / 场景第一个可用相机"),
                    targetCamera, typeof(Camera), true);

                transparent = EditorGUILayout.Toggle(
                    new GUIContent("透明背景", "清空色设为 alpha=0，输出会强制 PNG"), transparent);
            }

            EditorGUILayout.Space(6);

            // ---- 输出 ----
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("保存目录");
            if (GUILayout.Button(string.IsNullOrEmpty(saveFolder) ? "选择…" : ShortPath(saveFolder),
                    EditorStyles.popup))
            {
                string picked = EditorUtility.OpenFolderPanel("选择截图保存目录",
                    Directory.Exists(saveFolder) ? saveFolder : DefaultFolder(), "");
                if (!string.IsNullOrEmpty(picked)) { saveFolder = picked; SavePrefs(); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("默认目录", GUILayout.Width(70)))
            { saveFolder = DefaultFolder(); SavePrefs(); }
            if (GUILayout.Button("Assets 下", GUILayout.Width(70)))
            { saveFolder = Path.Combine(Application.dataPath, "Screenshots"); SavePrefs(); }
            EditorGUILayout.LabelField(ShortPath(saveFolder), hintStyle);
            EditorGUILayout.EndHorizontal();

            filePrefix = EditorGUILayout.TextField("文件名前缀", filePrefix);
            autoTimestamp = EditorGUILayout.Toggle("追加时间戳", autoTimestamp);

            format = (OutputFormat)EditorGUILayout.EnumPopup("格式", format);
            if (format == OutputFormat.JPG)
            {
                jpgQuality = EditorGUILayout.IntSlider("JPG 质量", jpgQuality, 1, 100);
                if (transparent)
                    EditorGUILayout.HelpBox("JPG 不支持透明通道，透明底会失效（自动改存 PNG）。", MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            colorCorrection = EditorGUILayout.Toggle(
                new GUIContent("色彩校正", "线性空间项目下做 Linear → sRGB 转换，避免出图偏暗"), colorCorrection);
            flipY = EditorGUILayout.Toggle(
                new GUIContent("垂直翻转", "如果出图上下颠倒，勾上/取消这里再截一次"), flipY);
            revealAfterSave = EditorGUILayout.Toggle("截图后定位文件", revealAfterSave);

            EditorGUILayout.Space(10);

            // ---- 截图按钮 ----
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
            if (GUILayout.Button("截 图 (Ctrl+Shift+S)", GUILayout.Height(38)))
                DoCapture();
            GUI.backgroundColor = oldBg;

            if (EditorApplication.isPlaying == false && mode == CaptureMode.GameView)
                EditorGUILayout.HelpBox("当前未运行：Game 视图需要有内容才能截到画面。可在运行模式下截图，或改用「相机渲染」（编辑模式也能出图）。", MessageType.Info);

            EditorGUILayout.Space(8);

            // ---- 结果 / 预览 ----
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastInfo, MessageType.None);

            if (previewTex != null)
            {
                float w = Mathf.Min(position.width - 24, 480);
                float h = w * previewTex.height / (float)previewTex.width;
                Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(r, previewTex);
            }

            if (!string.IsNullOrEmpty(lastSavedPath))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("打开图片")) Application.OpenURL("file:///" + lastSavedPath.Replace("\\", "/"));
                if (GUILayout.Button("定位")) EditorUtility.RevealInFinder(lastSavedPath);
                if (GUILayout.Button("复制路径")) GUIUtility.systemCopyBuffer = lastSavedPath;
                EditorGUILayout.EndHorizontal();
            }

            // ---- 历史 ----
            if (history.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("最近截图", EditorStyles.boldLabel);
                for (int i = 0; i < history.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(Path.GetFileName(history[i]), EditorStyles.linkLabel))
                    {
                        if (File.Exists(history[i]))
                        {
                            EditorUtility.RevealInFinder(history[i]);
                            LoadPreview(history[i]);
                            lastSavedPath = history[i];
                            lastInfo = "已定位：" + history[i];
                        }
                        else
                        {
                            lastInfo = "文件已不存在：" + history[i];
                        }
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    { history.RemoveAt(i); SavePrefs(); i--; }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
                titleStyle.normal.textColor = new Color(0.85f, 0.95f, 0.85f);
            }
            if (hintStyle == null)
                hintStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic };
        }

        private static string ShortPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            if (p.Length <= 42) return p;
            return "…" + p.Substring(p.Length - 40);
        }

        // ============================================================
        //  截图主流程
        // ============================================================
        public void DoCapture()
        {
            Texture2D tex = null;
            string method = "";
            bool needFlip = flipY;   // 只有 GameView 直读才可能需要翻转，且不改动用户设置

            if (mode == CaptureMode.GameView)
            {
                // 1) 反射读 Game 视图的 RenderTarget（运行/编辑模式都可能成功）
                tex = CaptureFromGameViewRT();
                method = "Game 视图直读";

                // 2) 兜底：运行时用 ScreenCapture（超采样也靠它）
                if (tex == null && EditorApplication.isPlaying)
                {
                    tex = CaptureByScreenCapture(superSize);
                    method = "ScreenCapture";
                    if (tex != null) needFlip = false; // ScreenCapture 内部已处理方向
                }

                // 3) 再兜底：相机渲染
                if (tex == null)
                {
                    tex = CaptureByCamera(ResolveCamera(), GetGameViewPixelSize(), 1, false);
                    method = "相机渲染(兜底)";
                    if (tex != null) needFlip = false;
                }
            }
            else
            {
                Vector2 baseSize = useGameViewSize ? GetGameViewPixelSize() : new Vector2(customWidth, customHeight);
                int w = Mathf.Max(1, Mathf.RoundToInt(baseSize.x));
                int h = Mathf.Max(1, Mathf.RoundToInt(baseSize.y));
                tex = CaptureByCamera(ResolveCamera(), new Vector2(w, h), superSize, transparent);
                method = "相机渲染";
                needFlip = false;
            }

            if (tex == null)
            {
                lastInfo = "截图失败：没拿到画面。\n· 运行模式下再试\n· 或切到「相机渲染」并指定相机\n· 确认 Game 视图没被完全遮挡";
                Repaint();
                return;
            }

            // 后处理
            if (needFlip) FlipVertically(tex);
            if (colorCorrection && QualitySettings.activeColorSpace == ColorSpace.Linear) LinearToSrgb(tex);

            // 保存
            string ext = (format == OutputFormat.JPG && !transparent) ? "jpg" : "png";
            string folder = string.IsNullOrEmpty(saveFolder) ? DefaultFolder() : saveFolder;
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string name = string.IsNullOrEmpty(filePrefix) ? "Shot" : filePrefix;
                name = Sanitize(name);
                if (autoTimestamp) name += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(folder, name + "." + ext);
                int i = 1;
                while (File.Exists(path))
                    path = Path.Combine(folder, name + "_" + (++i) + "." + ext);

                byte[] bytes = ext == "jpg" ? tex.EncodeToJPG(jpgQuality) : tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                lastSavedPath = path;

                history.Remove(path);
                history.Insert(0, path);
                while (history.Count > HistoryLimit) history.RemoveAt(history.Count - 1);

                lastInfo = string.Format("已保存 {0}x{1}  {2}\n方式：{3}   大小：{4}",
                    tex.width, tex.height, Path.GetFileName(path), method, FormatSize(bytes.Length));

                SetPreview(tex);
                SavePrefs();
                Debug.Log("[截图工具] " + lastSavedPath);

                string folderSlash = folder.Replace("\\", "/");
                string dataSlash = Application.dataPath.Replace("\\", "/");
                if (folderSlash.StartsWith(dataSlash, StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.Refresh();
                    string assetPath = "Assets" + folderSlash.Substring(dataSlash.Length) + "/" + Path.GetFileName(path);
                    EditorApplication.delayCall += () =>
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
                    };
                }
                else if (revealAfterSave)
                {
                    EditorUtility.RevealInFinder(path);
                }
            }
            catch (Exception e)
            {
                lastInfo = "保存失败：" + e.Message;
                Debug.LogError("[截图工具] " + e);
            }
            finally
            {
                if (tex != null) DestroyImmediate(tex);
            }

            Repaint();
        }

        // ============================================================
        //  三种捕获实现
        // ============================================================

        /// <summary>反射取 Game 视图内部 RenderTexture 并读出像素</summary>
        private static Texture2D CaptureFromGameViewRT()
        {
            try
            {
                RenderTexture rt = GetGameViewRenderTexture();
                if (rt == null || rt.width < 2 || rt.height < 2) return null;

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[截图工具] GameView RT 读取失败：" + e.Message);
                return null;
            }
        }

        private static RenderTexture GetGameViewRenderTexture()
        {
            try
            {
                Assembly asm = typeof(EditorWindow).Assembly;

                // 优先 PlayModeView.GetMainPlayModeView().targetTexture
                Type playModeType = asm.GetType("UnityEditor.PlayModeView");
                if (playModeType != null)
                {
                    MethodInfo getMain = playModeType.GetMethod("GetMainPlayModeView",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    object main = getMain != null ? getMain.Invoke(null, null) : null;
                    if (main != null)
                    {
                        RenderTexture rt = FindRenderTexture(main, main.GetType());
                        if (rt != null) return rt;
                    }
                }

                // 退化：找 GameView 窗口实例
                Type gameViewType = asm.GetType("UnityEditor.GameView");
                if (gameViewType == null) return null;
                var found = Resources.FindObjectsOfTypeAll(gameViewType);
                if (found != null && found.Length > 0)
                {
                    RenderTexture rt = FindRenderTexture(found[0], gameViewType);
                    if (rt != null) return rt;
                }
            }
            catch { }
            return null;
        }

        private static RenderTexture FindRenderTexture(object obj, Type startType)
        {
            string[] names = { "m_TargetTexture", "m_RenderTexture", "m_TargetRenderTexture", "targetTexture" };
            Type t = startType;
            int guard = 0;
            while (t != null && guard++ < 8)
            {
                foreach (string n in names)
                {
                    FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(RenderTexture))
                    {
                        var v = f.GetValue(obj) as RenderTexture;
                        if (v != null) return v;
                    }
                    PropertyInfo p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(RenderTexture))
                    {
                        var v = p.GetValue(obj, null) as RenderTexture;
                        if (v != null) return v;
                    }
                }
                t = t.BaseType;
            }
            return null;
        }

        /// <summary>运行时用官方 API 截（可超采样）</summary>
        private static Texture2D CaptureByScreenCapture(int ss)
        {
            try
            {
#if UNITY_2018_1_OR_NEWER
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(Mathf.Max(1, ss));
                if (tex != null && tex.width > 1) return tex;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("[截图工具] ScreenCapture 失败：" + e.Message);
            }
            return null;
        }

        /// <summary>用相机渲染到 RenderTexture（可控分辨率 / 透明底）</summary>
        private static Texture2D CaptureByCamera(Camera cam, Vector2 size, int ss, bool alpha)
        {
            if (cam == null) return null;
            int w = Mathf.Max(1, Mathf.RoundToInt(size.x) * Mathf.Max(1, ss));
            int h = Mathf.Max(1, Mathf.RoundToInt(size.y) * Mathf.Max(1, ss));
            if (w > 16384 || h > 16384) { w = Mathf.Min(w, 16384); h = Mathf.Min(h, 16384); }

            RenderTexture rt = null;
            Texture2D result = null;
            try
            {
                RenderTextureReadWrite rw = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;

                rt = new RenderTexture(w, h, 24,
                    alpha ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default, rw);
                rt.Create();

                var prevTarget = cam.targetTexture;
                var prevFlags = cam.clearFlags;
                var prevBg = cam.backgroundColor;

                cam.targetTexture = rt;
                if (alpha)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                }
                cam.Render();
                cam.targetTexture = prevTarget;
                if (alpha) { cam.clearFlags = prevFlags; cam.backgroundColor = prevBg; }

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                result = new Texture2D(w, h, alpha ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                result.Apply(false);
                RenderTexture.active = prevActive;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[截图工具] 相机渲染失败：" + e.Message);
                if (result != null) { DestroyImmediate(result); result = null; }
            }
            finally
            {
                if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            }
            return result;
        }

        // ============================================================
        //  工具方法
        // ============================================================
        private Camera ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;
            if (Camera.main != null) return Camera.main;
#pragma warning disable CS0618
#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType<Camera>();
#endif
#pragma warning restore CS0618
            if (all != null && all.Length > 0) return all[0];
            return null;
        }

        private static Vector2 GetGameViewPixelSize()
        {
            RenderTexture rt = GetGameViewRenderTexture();
            if (rt != null && rt.width > 1) return new Vector2(rt.width, rt.height);

            // 退化：按 GameView 窗口的可用区域估算（含 16:10 兜底）
            try
            {
                Type t = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (t != null)
                {
                    var found = Resources.FindObjectsOfTypeAll(t);
                    if (found != null && found.Length > 0)
                    {
                        var w = found[0] as EditorWindow;
                        if (w != null && w.position.width > 50)
                        {
                            float aspect = 16f / 9f;
                            float width = w.position.width;
                            return new Vector2(width, width / aspect);
                        }
                    }
                }
            }
            catch { }
            return new Vector2(1920, 1080);
        }

        private static void FlipVertically(Texture2D tex)
        {
            Color32[] src = tex.GetPixels32();
            var dst = new Color32[src.Length];
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
                Array.Copy(src, y * w, dst, (h - 1 - y) * w, w);
            tex.SetPixels32(dst);
            tex.Apply(false);
        }

        private static void LinearToSrgb(Texture2D tex)
        {
            // 256 项查表，避免逐像素数学函数开销
            var lut = new byte[256];
            for (int i = 0; i < 256; i++)
                lut[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(i / 255f) * 255f), 0, 255);

            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color32(lut[c.r], lut[c.g], lut[c.b], c.a);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        private static string Sanitize(string s)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in bad) s = s.Replace(c.ToString(), "_");
            return s.Replace(" ", "_");
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F1") + " KB";
            return (bytes / (1024f * 1024f)).ToString("F2") + " MB";
        }

        // ---------------- 预览 ----------------
        private void SetPreview(Texture2D src)
        {
            LoadPreviewFromTexture(src);
        }

        private void LoadPreviewFromTexture(Texture2D src)
        {
            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }
            if (src == null) return;
            int w = src.width, h = src.height;
            float scale = Mathf.Min(1f, 512f / Mathf.Max(w, h));
            int tw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int th = Mathf.Max(1, Mathf.RoundToInt(h * scale));

            var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            previewTex = new Texture2D(tw, th, TextureFormat.RGB24, false);
            previewTex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
            previewTex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }

        private void LoadPreview(string file)
        {
            try
            {
                if (!File.Exists(file)) return;
                var tmp = new Texture2D(2, 2);
                if (!tmp.LoadImage(File.ReadAllBytes(file))) { DestroyImmediate(tmp); return; }
                LoadPreviewFromTexture(tmp);
                DestroyImmediate(tmp);
            }
            catch { }
        }
    }
}
#endif
