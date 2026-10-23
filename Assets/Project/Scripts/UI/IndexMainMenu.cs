using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Index 主界面的伙伴展示与设置。所有界面元素保存在场景中，可直接编辑。</summary>
public class IndexMainMenu : MonoBehaviour
{
    [Serializable]
    public class Companion
    {
        [Min(1)] public int completedLevel = 1;
        public Image portrait;
        public Text caption;
        public string displayName = "画中故人";
    }

    public Companion[] companions;
    public Text journeyText;
    public GameObject settingsWindow;
    public GameObject journalWindow;
    public Slider musicVolumeSlider;
    public Slider soundVolumeSlider;
    public Text journalText;
    [SerializeField] private CompanionJournal journalPrefab;
    private CompanionJournal companionJournal;
    private GameObject newGameConfirmation;
    private Text confirmationMessage;
    private bool transitioning;

    private void Awake()
    {
        if (!IndexPrologue.ConsumeRequest()) return;
        IndexPrologue prefab = Resources.Load<IndexPrologue>("IndexPrologue");
        if (prefab != null) Instantiate(prefab).Begin();
    }

    public void ContinueGame()
    {
        if (transitioning || IndexPrologue.IsPlaying) return;
        string scene = DataSystem.instance != null && DataSystem.instance.enablePersistence
            ? SaveManager.Instance.GetString("Game.ResumeScene", "Select") : "Select";
        // Only known gameplay destinations may be resumed.
        if (scene != "Level1" && scene != "Level2" && scene != "Select" && scene != "Start") scene = "Select";
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("[IndexMainMenu] 继续游戏的场景未加入 Build Settings：" + scene);
            return;
        }
        transitioning = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    public void NewGame()
    {
        if (transitioning || IndexPrologue.IsPlaying) return;
        if (newGameConfirmation == null) BuildNewGameConfirmation();
        confirmationMessage.text = "重新入画将清空关卡、任务与收藏进度。\n名字和音量设置保留。是否重新开始？";
        newGameConfirmation.SetActive(true);
        newGameConfirmation.transform.SetAsLastSibling();
    }

    private void ConfirmNewGame()
    {
        if (transitioning || IndexPrologue.IsPlaying) return;
        if (!Application.CanStreamedLevelBeLoaded("Start"))
        {
            confirmationMessage.text = "Start 场景不可用，未重置进度。";
            return;
        }
        if (DataSystem.instance == null || !DataSystem.instance.enablePersistence)
        {
            confirmationMessage.text = "当前已关闭存档，请开启后再重置正式进度。";
            return;
        }
        if (!DataSystem.instance.StartNewGame())
        {
            confirmationMessage.text = "新游戏存档写入失败，请检查控制台和存档目录。";
            return;
        }
        transitioning = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Start");
    }

    private void BuildNewGameConfirmation()
    {
        var canvas = GetComponentInParent<Canvas>();
        newGameConfirmation = new GameObject("NewGameConfirmation", typeof(RectTransform), typeof(Image));
        var overlay = newGameConfirmation.GetComponent<RectTransform>();
        overlay.SetParent(canvas.transform, false);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        newGameConfirmation.GetComponent<Image>().color = new Color(0, 0, 0, .72f);
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var rect = panel.GetComponent<RectTransform>(); rect.SetParent(overlay, false);
        rect.sizeDelta = new Vector2(620, 300);
        panel.GetComponent<Image>().color = new Color(.92f, .86f, .7f);
        confirmationMessage = CreateLabel(rect, "Message", "", new Vector2(0, 55), new Vector2(560, 130));
        CreateConfirmationButton(rect, "Cancel", "取消", -145, () => newGameConfirmation.SetActive(false));
        CreateConfirmationButton(rect, "Confirm", "重新开始", 145, ConfirmNewGame);
    }

    private Text CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = obj.GetComponent<Text>(); text.text = value;
        text.font = journeyText.font; text.fontSize = 25;
        text.color = new Color(.24f, .18f, .13f); text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void CreateConfirmationButton(Transform parent, string name, string label, float x, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchoredPosition = new Vector2(x, -85); rect.sizeDelta = new Vector2(240, 64);
        obj.GetComponent<Image>().color = new Color(.75f, .64f, .46f);
        var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>();
        button.onClick.AddListener(action);
        CreateLabel(rect, "Label", label, Vector2.zero, rect.sizeDelta);
    }

    private void Start()
    {
        CompanionJournal prefab = journalPrefab != null ? journalPrefab : Resources.Load<CompanionJournal>("CompanionJournal");
        if (prefab != null)
        {
            companionJournal = Instantiate(prefab, journalWindow.transform.parent);
            companionJournal.Initialize(id => DataSystem.instance != null && DataSystem.instance.IsLevelUnlocked(id), DialogueHistory.HasRead);
            companionJournal.gameObject.SetActive(false);
            foreach (Companion companion in companions)
            {
                if (companion.portrait == null) continue;
                Button button = companion.portrait.GetComponent<Button>() ?? companion.portrait.gameObject.AddComponent<Button>();
                companion.portrait.raycastTarget = true;
                button.targetGraphic = companion.portrait;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OpenCompanion(companion.completedLevel));
            }
            UiClickSoundInstaller.ScanGameObject(gameObject);
        }
        settingsWindow.SetActive(false);
        journalWindow.SetActive(false);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        soundVolumeSlider.onValueChanged.AddListener(SetSoundVolume);
        Refresh();
    }

    public void Refresh()
    {
        var data = DataSystem.instance;
        if (data == null) return;
        int count = 0;
        string journal = "";
        foreach (var companion in companions)
        {
            // 当前项目以解锁下一关作为通关记录；第一关默认开放不会解锁伙伴。
            bool unlocked = data.IsLevelUnlocked(companion.completedLevel + 1);
            CompanionProfile profile = companionJournal != null ? companionJournal.FindProfile(companion.completedLevel) : null;
            string name = profile != null ? profile.displayName : companion.displayName;
            if (unlocked) count++;
            if (companion.portrait != null)
            {
                companion.portrait.color = unlocked ? Color.white : new Color(.28f, .23f, .19f, .16f);
                if (profile != null && profile.portrait != null) companion.portrait.sprite = profile.portrait;
                Button button = companion.portrait.GetComponent<Button>();
                if (button != null) button.interactable = unlocked && profile != null;
            }
            if (companion.caption != null)
                companion.caption.text = unlocked ? name : "第 " + companion.completedLevel + " 关相逢";
            journal += (unlocked ? "已相逢  ·  " + name : "待相逢  ·  通关第 " + companion.completedLevel + " 关") + "\n\n";
        }
        journeyText.text = "已与 " + count + " 位故人相逢 · 继续入画，续写故事";
        journalText.text = journal;
        musicVolumeSlider.SetValueWithoutNotify(data.GetSettingsMusic() ? data.GetSettingsMusicVolume() : 0f);
        soundVolumeSlider.SetValueWithoutNotify(data.GetSettingsSFX() ? data.GetSettingsSoundVolume() : 0f);
    }

    public void OpenSettings() { if (IndexPrologue.IsPlaying) return; CloseWindows(); Refresh(); settingsWindow.SetActive(true); settingsWindow.transform.SetAsLastSibling(); }
    public void OpenJournal()
    {
        if (IndexPrologue.IsPlaying) return;
        CloseWindows(); Refresh();
        if (companionJournal != null) companionJournal.OpenList();
        else { journalWindow.SetActive(true); journalWindow.transform.SetAsLastSibling(); }
    }
    public void OpenCompanion(int completedLevel)
    {
        if (IndexPrologue.IsPlaying) return;
        if (companionJournal == null) return;
        CompanionProfile profile = companionJournal.FindProfile(completedLevel);
        if (!companionJournal.IsMet(profile)) return;
        CloseWindows();
        companionJournal.OpenProfile(profile);
    }
    public void CloseWindows() { settingsWindow.SetActive(false); journalWindow.SetActive(false); if (companionJournal != null) companionJournal.Close(); if (newGameConfirmation != null) newGameConfirmation.SetActive(false); }
    private void SetMusicVolume(float volume) { if (DataSystem.instance != null) DataSystem.instance.SaveSettingsMusicVolume(volume); }
    private void SetSoundVolume(float volume) { if (DataSystem.instance != null) DataSystem.instance.SaveSettingsSoundVolume(volume); }
    private void OnDestroy()
    {
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (soundVolumeSlider != null) soundVolumeSlider.onValueChanged.RemoveListener(SetSoundVolume);
    }
    public void QuitGame()
    {
        if (IndexPrologue.IsPlaying) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void Update()
    {
        if (DebugUnlockPanel.BlocksGameplayInput) return;
        if (IndexPrologue.IsPlaying) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (companionJournal != null && companionJournal.gameObject.activeSelf && companionJournal.IsDetailOpen)
            companionJournal.BackToList();
        else CloseWindows();
    }
}
