using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>通关奖励共用展示，图片资料引用画像砖录，避免重复登记。</summary>
public class BrickRewardView : MonoBehaviour
{
    public RecordWindow catalog;
    public Font font;
    public NextLevelButton source;
    public bool buildOnEnable;

    private void OnEnable()
    {
        if (!Application.isPlaying || !buildOnEnable) return;
        if (source != null) source.GrantBrickRewards();
        Build(source != null ? source.RewardBrickIds : null);
    }

    public static void Show(Transform parent, NextLevelButton source, Vector2 size)
    {
        if (parent.Find("本关砖画收藏") != null) return;
        BrickRewardView prefab = Resources.Load<BrickRewardView>("BrickRewardView");
        if (prefab == null) { Debug.LogError("缺少 BrickRewardView 奖励界面预制体"); return; }
        BrickRewardView view = Instantiate(prefab, parent, false);
        view.name = "本关砖画收藏";
        view.GetComponent<RectTransform>().sizeDelta = size;
        view.Build(source != null ? source.RewardBrickIds : null);
    }

    public void Build(string[] ids)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        var rewards = new List<string>();
        var seen = new HashSet<string>();
        if (ids != null) foreach (string raw in ids)
            if (!string.IsNullOrWhiteSpace(raw) && seen.Add(raw.Trim())) rewards.Add(raw.Trim());
        Label(transform, "收藏提示", rewards.Count == 0 ? "本关暂无新增砖画，已收藏的成果可回主页的画像砖录查看。" :
            $"本关解锁了{rewards.Count}个石像砖，快回主页的画像砖录瞧瞧吧！", new Vector2(.5f, 0), new Vector2(0, 38), new Vector2(-30, 70), 26, true);
        GameObject viewport = new GameObject("奖励横向列表", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(transform, false);
        RectTransform rect = viewport.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, .24f); rect.anchorMax = new Vector2(1, 1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        GameObject row = new GameObject("砖画", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(viewport.transform, false);
        RectTransform content = row.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, .5f); content.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24; layout.padding = new RectOffset(16, 16, 12, 12); layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false;
        row.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = rect; scroll.content = content;
        scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        float width = GetComponent<RectTransform>().sizeDelta.x;
        foreach (string id in rewards)
        {
            GameObject card = new GameObject(id, typeof(RectTransform), typeof(LayoutElement)); card.transform.SetParent(row.transform, false);
            card.GetComponent<LayoutElement>().preferredWidth = Mathf.Min(340, (width - 56) / Mathf.Max(1, Mathf.Min(3, rewards.Count)));
            Sprite sprite = null; string title = id;
            if (catalog != null && !catalog.TryGetBrick(id, out sprite, out title)) Debug.LogWarning("画像砖录未登记奖励：" + id);
            GameObject art = new GameObject("砖画图片", typeof(RectTransform), typeof(Image)); art.transform.SetParent(card.transform, false);
            RectTransform artRect = art.GetComponent<RectTransform>(); artRect.anchorMin = new Vector2(0, .2f); artRect.anchorMax = Vector2.one; artRect.offsetMin = artRect.offsetMax = Vector2.zero;
            Image image = art.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.color = sprite != null ? Color.white : new Color(.6f,.54f,.43f); image.raycastTarget = false;
            Label(card.transform, "砖画名称", title, new Vector2(.5f, 0), new Vector2(0, 25), new Vector2(-8, 45), 24, true);
        }
        Canvas.ForceUpdateCanvases();
        if (content.rect.width < width) { content.anchorMin = new Vector2(.5f, 0); content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, .5f); }
    }

    private void Label(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, bool stretch)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = stretch ? new Vector2(0, anchor.y) : anchor; rect.anchorMax = stretch ? new Vector2(1, anchor.y) : anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        Text label = go.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(.22f, .19f, .15f); label.raycastTarget = false;
    }
}
