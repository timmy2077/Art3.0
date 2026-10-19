using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecordWindow : MonoBehaviour
{
    [System.Serializable]
    private class BrickRecord
    {
        public string id;
        public string displayName;
        public Sprite sprite;
        [Tooltip("详情与大图使用的图片；未指定时使用列表图片。")]
        public Sprite detailSprite;
        public string imageCaption;
        public string era;
        public string excavationSite;
        public string currentCollection;
        [TextArea(3, 10)] public string interpretation;
        [TextArea(2, 8)] public string sources;
    }

    [SerializeField] private Transform contentRoot;
    [SerializeField] private LevelRecordItem recordItemPrefab;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private BrickRecord[] bricks;
    [SerializeField] private BrickDetailView detailView;
    [SerializeField] private Text collectionCount;

    public bool TryGetBrick(string id, out Sprite sprite, out string displayName)
    {
        if (bricks != null) foreach (BrickRecord brick in bricks)
            if (brick != null && brick.id == id) { sprite = brick.sprite; displayName = brick.displayName; return true; }
        sprite = null; displayName = id; return false;
    }

    private readonly List<LevelRecordItem> spawnedItems = new List<LevelRecordItem>();

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (detailView != null) detailView.Close();
        foreach (LevelRecordItem item in spawnedItems)
        {
            if (item == null) continue;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        if (contentRoot == null || recordItemPrefab == null)
        {
            Debug.LogWarning("[RecordWindow] 请在 RecordUI 预制体上指定内容容器和记录条目预制体。", this);
            return;
        }

        int count = 0;
        int total = 0;
        var seen = new HashSet<string>();
        if (bricks != null)
        {
            foreach (BrickRecord brick in bricks)
            {
                if (brick == null || string.IsNullOrWhiteSpace(brick.id) || !seen.Add(brick.id)) continue;
                total++;
                if (
                    (!DataSystem.IsDefaultBrick(brick.id) &&
                     (DataSystem.instance == null || !DataSystem.instance.IsBrickUnlocked(brick.id)))) continue;

                LevelRecordItem item = Instantiate(recordItemPrefab, contentRoot);
                item.SetData(brick.id, brick.displayName, brick.sprite);
                item.SetOpenAction(() => OpenDetail(brick));
                item.gameObject.SetActive(true);
                spawnedItems.Add(item);
                count++;
            }
        }

        if (emptyState != null)
            emptyState.SetActive(count == 0);
        if (collectionCount != null) collectionCount.text = $"砖画收集：{count} / {total}";
    }

    private void OpenDetail(BrickRecord brick)
    {
        if (detailView == null) return;
        detailView.Open(brick.displayName, brick.detailSprite != null ? brick.detailSprite : brick.sprite,
            brick.imageCaption, brick.era, brick.excavationSite, brick.currentCollection,
            brick.interpretation, brick.sources);
    }

    /// <summary>
    /// 清空全部存档（玩家名、关卡解锁、石砖解锁、音量设置），并立即刷新记录列表。
    /// 绑定到记录面板上的“清空存档”按钮。
    /// </summary>
    public void ClearSaveData()
    {
        if (DataSystem.instance != null)
        {
            DataSystem.instance.ClearAllData();
            Debug.Log("[RecordWindow] 已清空全部存档，记录列表已刷新。");
        }
        else
        {
            Debug.LogWarning("[RecordWindow] DataSystem 不存在，无法清空存档。", this);
        }

        Refresh();
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
