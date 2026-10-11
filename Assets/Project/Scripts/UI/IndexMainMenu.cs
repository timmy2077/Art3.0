using System;
using UnityEngine;
using UnityEngine.UI;

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

    private void Start()
    {
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
            if (unlocked) count++;
            if (companion.portrait != null)
                companion.portrait.color = unlocked ? Color.white : new Color(.28f, .23f, .19f, .16f);
            if (companion.caption != null)
                companion.caption.text = unlocked ? companion.displayName : "第 " + companion.completedLevel + " 关相逢";
            journal += (unlocked ? "已相逢  ·  " + companion.displayName : "待相逢  ·  通关第 " + companion.completedLevel + " 关") + "\n\n";
        }
        journeyText.text = "已与 " + count + " 位故人相逢 · 继续入画，续写故事";
        journalText.text = journal;
        musicVolumeSlider.SetValueWithoutNotify(data.GetSettingsMusic() ? data.GetSettingsMusicVolume() : 0f);
        soundVolumeSlider.SetValueWithoutNotify(data.GetSettingsSFX() ? data.GetSettingsSoundVolume() : 0f);
    }

    public void OpenSettings() { CloseWindows(); Refresh(); settingsWindow.SetActive(true); settingsWindow.transform.SetAsLastSibling(); }
    public void OpenJournal() { CloseWindows(); Refresh(); journalWindow.SetActive(true); journalWindow.transform.SetAsLastSibling(); }
    public void CloseWindows() { settingsWindow.SetActive(false); journalWindow.SetActive(false); }
    private void SetMusicVolume(float volume) { if (DataSystem.instance != null) DataSystem.instance.SaveSettingsMusicVolume(volume); }
    private void SetSoundVolume(float volume) { if (DataSystem.instance != null) DataSystem.instance.SaveSettingsSoundVolume(volume); }
    private void OnDestroy()
    {
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (soundVolumeSlider != null) soundVolumeSlider.onValueChanged.RemoveListener(SetSoundVolume);
    }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) CloseWindows(); }
}
