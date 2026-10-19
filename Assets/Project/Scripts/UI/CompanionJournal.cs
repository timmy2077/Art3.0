using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CompanionJournal : MonoBehaviour
{
    [SerializeField] private CompanionProfile[] profiles;
    [SerializeField] private GameObject listPage;
    [SerializeField] private Transform listContent;
    [SerializeField] private Button itemTemplate;
    [SerializeField] private Button closeListButton;
    [SerializeField] private GameObject detailPage;
    [SerializeField] private Text characterName;
    [SerializeField] private Image portrait;
    [SerializeField] private Text identity;
    [SerializeField] private Text experience;
    [SerializeField] private Text dialogueHistory;
    [SerializeField] private ScrollRect detailScroll;
    [SerializeField] private Button backButton;

    private readonly List<Button> items = new List<Button>();
    private Func<int, bool> levelUnlocked;
    private Func<DialogueData, DialogueSection, int, bool> hasRead;
    private GameObject previousSelection;
    private GameObject detailPreviousSelection;
    public bool IsDetailOpen => detailPage != null && detailPage.activeSelf;
    public CompanionProfile[] Profiles => profiles;

    private void Awake()
    {
        closeListButton.onClick.AddListener(Close);
        backButton.onClick.AddListener(BackToList);
    }

    public void Initialize(Func<int, bool> unlockQuery, Func<DialogueData, DialogueSection, int, bool> readQuery)
    {
        levelUnlocked = unlockQuery;
        hasRead = readQuery;
        foreach (Button item in items) if (item != null) Destroy(item.gameObject);
        items.Clear();
        foreach (CompanionProfile profile in profiles)
        {
            if (profile == null) continue;
            Button item = Instantiate(itemTemplate, listContent);
            item.name = "Companion_" + profile.name;
            item.onClick.AddListener(() => OpenProfile(profile));
            item.gameObject.SetActive(true);
            items.Add(item);
        }
        Refresh();
        UiClickSoundInstaller.ScanGameObject(gameObject);
    }

    public CompanionProfile FindProfile(int completedLevel)
    {
        if (profiles == null) return null;
        foreach (CompanionProfile profile in profiles)
            if (profile != null && profile.completedLevel == completedLevel) return profile;
        return null;
    }

    public bool IsMet(CompanionProfile profile)
    {
        return profile != null && levelUnlocked != null && levelUnlocked(profile.completedLevel + 1);
    }

    public void Refresh()
    {
        int index = 0;
        foreach (CompanionProfile profile in profiles)
        {
            if (profile == null) continue;
            if (index >= items.Count) break;
            Button item = items[index++];
            bool met = IsMet(profile);
            item.interactable = met;
            item.GetComponentInChildren<Text>(true).text = met
                ? "已相逢  ·  " + profile.displayName + "    查看人物档案 ›"
                : "待相逢  ·  通关第 " + profile.completedLevel + " 关";
        }
    }

    public void OpenList()
    {
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Refresh();
        detailPage.SetActive(false);
        listPage.SetActive(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Select(closeListButton.gameObject);
    }

    public void OpenProfile(CompanionProfile profile)
    {
        // 不仅依赖 Button.interactable，直接调用也不能打开未相逢人物。
        if (!IsMet(profile)) return;
        if (!gameObject.activeSelf)
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        detailPreviousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        characterName.text = profile.displayName;
        portrait.sprite = profile.portrait;
        portrait.enabled = profile.portrait != null;
        identity.text = Fallback(profile.identity, "人物身份尚未记录。");
        experience.text = Fallback(profile.sharedExperience, "与主角的经历尚未记录。");
        dialogueHistory.text = BuildHistory(profile, hasRead);
        listPage.SetActive(false);
        detailPage.SetActive(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        detailScroll.StopMovement();
        detailScroll.verticalNormalizedPosition = 1f;
        Select(backButton.gameObject);
    }

    public static string BuildHistory(CompanionProfile profile, Func<DialogueData, DialogueSection, int, bool> readQuery)
    {
        StringBuilder result = new StringBuilder();
        HashSet<DialogueData> included = new HashSet<DialogueData>();
        if (profile != null && profile.relatedDialogues != null && readQuery != null)
        {
            foreach (DialogueData data in profile.relatedDialogues)
            {
                if (data == null || !included.Add(data)) continue;
                foreach (DialogueSection section in Enum.GetValues(typeof(DialogueSection)))
                {
                    if (data.isCoreNPC == (section == DialogueSection.Minor)) continue;
                    string[] lines = DialogueHistory.Lines(data, section);
                    if (lines == null) continue;
                    bool headingWritten = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i]) || !readQuery(data, section, i)) continue;
                        if (!headingWritten)
                        {
                            if (result.Length > 0) result.Append("\n\n");
                            string heading = section == DialogueSection.Greeting ? "初次交谈" :
                                section == DialogueSection.Task ? "相助之约" : "画中见闻";
                            result.Append(data.npcName).Append(" · ").Append(heading).Append("\n");
                            headingWritten = true;
                        }
                        result.Append("「").Append(lines[i]).Append("」\n");
                    }
                }
            }
        }
        return result.Length == 0 ? "尚无已读对话记录。\n与这位故人交谈后，读完的内容会留存在这里。" : result.ToString().TrimEnd();
    }

    public void BackToList()
    {
        detailPage.SetActive(false);
        listPage.SetActive(true);
        Refresh();
        if (detailPreviousSelection != null && detailPreviousSelection.activeInHierarchy) Select(detailPreviousSelection);
        else Select(closeListButton.gameObject);
        detailPreviousSelection = null;
    }

    public void Close()
    {
        detailPage.SetActive(false);
        gameObject.SetActive(false);
        if (previousSelection != null && previousSelection.activeInHierarchy) Select(previousSelection);
        previousSelection = detailPreviousSelection = null;
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }

    private static string Fallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
