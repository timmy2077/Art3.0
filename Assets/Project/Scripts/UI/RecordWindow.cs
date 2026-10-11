using System.Collections.Generic;
using UnityEngine;

public class RecordWindow : MonoBehaviour
{
    [System.Serializable]
    private class BrickRecord
    {
        public string id;
        public string displayName;
        public Sprite sprite;
    }

    [SerializeField] private Transform contentRoot;
    [SerializeField] private LevelRecordItem recordItemPrefab;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private BrickRecord[] bricks;

    private readonly List<LevelRecordItem> spawnedItems = new List<LevelRecordItem>();

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
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
        if (bricks != null)
        {
            foreach (BrickRecord brick in bricks)
            {
                if (brick == null ||
                    (!DataSystem.IsDefaultBrick(brick.id) &&
                     (DataSystem.instance == null || !DataSystem.instance.IsBrickUnlocked(brick.id)))) continue;

                LevelRecordItem item = Instantiate(recordItemPrefab, contentRoot);
                item.SetData(brick.id, brick.displayName, brick.sprite);
                item.gameObject.SetActive(true);
                spawnedItems.Add(item);
                count++;
            }
        }

        if (emptyState != null)
            emptyState.SetActive(count == 0);
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
