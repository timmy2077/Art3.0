using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>仅编辑器及 Development Build 自动启用的关卡测试入口。</summary>
[DefaultExecutionOrder(-1000)]
public sealed class DebugUnlockPanel : MonoBehaviour
{
    [SerializeField] private Font chineseFont;
    private static DebugUnlockPanel instance;
    private static int closedFrame = -1;
    public static bool IsOpen => instance != null && instance.modal != null && instance.modal.activeSelf;
    public static bool BlocksGameplayInput => IsOpen || closedFrame == Time.frameCount;
    private readonly string[] names = { "农耕", "庖厨宴饮", "车马出行", "四神灵瑞", "墓主生活" };
    private readonly Text[] states = new Text[5];
    private readonly Button[] unlockButtons = new Button[5];
    private RectTransform safeArea, window;
    private GameObject modal, launcher, previousSelection;
    private Text feedback;
    private int width, height;
    private Rect lastSafeArea;
    private readonly Color accent = new Color(.36f, .85f, .69f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; closedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (instance != null) return;
        var prefab = Resources.Load<DebugUnlockPanel>("DebugUnlockPanel");
        if (prefab != null) Instantiate(prefab);
        else Debug.LogWarning("[测试工具] 缺少 Resources/DebugUnlockPanel 预制体。");
#endif
    }

    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Destroy(gameObject);
        return;
#else
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SceneManager.sceneLoaded += SceneLoaded;
#endif
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= SceneLoaded;
        instance = null;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode) { Close(); }

    private bool EntranceActive() => IndexPrologue.IsPlaying || FindObjectOfType<LevelVideoEntrance>() != null;

    private void Update()
    {
        if (launcher == null) return;
        bool blocked = EntranceActive();
        launcher.SetActive(!blocked && !IsOpen);
        if (blocked) { if (IsOpen) Close(); return; }
        if (Input.GetKeyDown(KeyCode.F8)) { if (IsOpen) Close(); else Open(); }
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (IsOpen) Refresh();
        if (width != Screen.width || height != Screen.height || lastSafeArea != Screen.safeArea) FitSafeArea();
    }

    public void Open()
    {
        if (modal == null || IsOpen || EntranceActive()) return;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        foreach (var player in FindObjectsOfType<PlayerMovement>())
        {
            player.TouchMove = Vector2.zero;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
        }
        feedback.text = "选择要解锁的关卡。";
        modal.SetActive(true);
        launcher.SetActive(false);
        FitSafeArea();
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        modal.SetActive(false);
        closedFrame = Time.frameCount;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        previousSelection = null;
        launcher.SetActive(!EntranceActive());
    }

    public void UnlockLevel(int id)
    {
        if (DataSystem.instance == null || id < 1 || id > names.Length) return;
        DataSystem.instance.UnlockLevel(id);
        FinishUnlock("第 " + id + " 关已解锁");
    }

    public void UnlockAllLevels()
    {
        if (DataSystem.instance == null) return;
        for (int id = 1; id <= names.Length; id++) DataSystem.instance.UnlockLevel(id);
        FinishUnlock("全部 5 个关卡已解锁");
    }

    private void FinishUnlock(string result)
    {
        foreach (var brick in FindObjectsOfType<LevelUnlock>(true)) brick.RefreshLevelState();
        bool persistent = DataSystem.instance.enablePersistence;
        bool saved = !persistent || SaveManager.Instance.SaveNow();
        feedback.text = result + (persistent ? (saved ? "，已保存。" : "，但存档写入失败；退出后可能丢失。") : "（当前为临时测试数据）。");
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < names.Length; i++)
        {
            bool unlocked = DataSystem.instance != null && DataSystem.instance.IsLevelUnlocked(i + 1);
            states[i].text = unlocked ? "已解锁" : "未解锁";
            states[i].color = unlocked ? accent : new Color(.7f, .72f, .77f);
            unlockButtons[i].interactable = !unlocked && DataSystem.instance != null;
        }
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32010;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        safeArea = Rect("SafeArea", transform, Vector2.zero, Vector2.zero);
        launcher = Button(safeArea, "测试工具  /  F8", new Vector2(0, -38), new Vector2(248, 60), Open).gameObject;
        var launchRect = launcher.GetComponent<RectTransform>();
        launchRect.anchorMin = launchRect.anchorMax = new Vector2(.5f, 1);
        modal = Rect("Modal", transform, Vector2.zero, Vector2.zero).gameObject;
        Stretch(modal.GetComponent<RectTransform>());
        modal.AddComponent<Image>().color = new Color(0, 0, 0, .76f);
        var content = Rect("SafeContent", modal.transform, Vector2.zero, Vector2.zero);
        Stretch(content);
        // 内容跟随安全区，遮罩仍然覆盖整屏。
        window = Rect("Window", content, Vector2.zero, new Vector2(1020, 790));
        window.gameObject.AddComponent<Image>().color = new Color(.085f, .10f, .14f, 1);
        Label(window, "关卡测试工具", new Vector2(-145, 330), new Vector2(620, 60), 40, Color.white);
        Label(window, "EDITOR / DEVELOPMENT BUILD", new Vector2(-145, 283), new Vector2(620, 40), 20, accent);
        Button(window, "关闭 ×", new Vector2(380, 326), new Vector2(165, 58), Close);
        for (int i = 0; i < names.Length; i++)
        {
            int id = i + 1;
            float y = 196 - i * 83;
            var row = Rect("Level" + id, window, new Vector2(0, y), new Vector2(920, 72));
            row.gameObject.AddComponent<Image>().color = new Color(.13f, .15f, .20f);
            Label(row, "第 " + id + " 关   " + names[i], new Vector2(-173, 0), new Vector2(500, 60), 29, Color.white);
            states[i] = Label(row, "", new Vector2(150, 0), new Vector2(140, 60), 25, accent);
            unlockButtons[i] = Button(row, "解锁", new Vector2(335, 0), new Vector2(160, 54), () => UnlockLevel(id));
        }
        Button(window, "解锁全部关卡", new Vector2(0, -223), new Vector2(920, 62), UnlockAllLevels);
        feedback = Label(window, "", new Vector2(0, -288), new Vector2(920, 50), 25, accent);
        Label(window, "解锁会记入当前存档；任务与收藏需正常游玩获得。\nF8 打开 / 关闭 · Esc 关闭 · 后续关卡以实际制作进度为准", new Vector2(0, -345), new Vector2(920, 70), 22, new Color(.7f, .73f, .8f));
        modal.SetActive(false);
        FitSafeArea();
    }

    private void FitSafeArea()
    {
        width = Screen.width; height = Screen.height; lastSafeArea = Screen.safeArea;
        if (width <= 0 || height <= 0) return;
        Vector2 min = new Vector2(lastSafeArea.xMin / width, lastSafeArea.yMin / height);
        Vector2 max = new Vector2(lastSafeArea.xMax / width, lastSafeArea.yMax / height);
        safeArea.anchorMin = min; safeArea.anchorMax = max; safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        var content = (RectTransform)window.parent;
        content.anchorMin = min; content.anchorMax = max; content.offsetMin = content.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        float scale = Mathf.Min(1f, content.rect.width / 1060f, content.rect.height / 830f);
        window.localScale = Vector3.one * scale;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var text = Rect("Text", parent, position, size).gameObject.AddComponent<Text>();
        text.font = chineseFont; text.text = value; text.fontSize = fontSize; text.color = color;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        return text;
    }
    private Button Button(Transform parent, string caption, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(caption, parent, position, size);
        rect.gameObject.AddComponent<Image>().color = new Color(.20f, .34f, .36f);
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors; colors.disabledColor = new Color(.45f, .45f, .45f, .45f); button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        var label = Label(rect, caption, Vector2.zero, size, 26, Color.white);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
}
