using UnityEngine;

/// <summary>用于关卡完成面板：返回选关界面并播放下一关的解锁动画。</summary>
public class NextLevelButton : MonoBehaviour
{
    [Tooltip("要解锁的下一关ID。")]
    [SerializeField] private int nextLevelId = 2;
    [Tooltip("完成当前关卡后获得的石砖 ID，可填写多个。")]
    [SerializeField] private string[] rewardBrickIds;

    private bool hasTriggered;

    public void UnlockAndShowNextLevel()
    {
        if (hasTriggered) return;

        if (DataSystem.instance != null)
        {
            DataSystem.instance.UnlockLevel(nextLevelId);
            if (rewardBrickIds != null)
            {
                foreach (string brickId in rewardBrickIds)
                    DataSystem.instance.UnlockBrick(brickId);
            }
        }
        else
        {
            Debug.LogWarning("[NextLevelButton] 未找到 DataSystem，无法记录下一关的解锁状态。", this);
        }

        UIPageSwitch pageSwitch = FindObjectOfType<UIPageSwitch>(true);
        int nextPanelIndex = nextLevelId - 1;
        if (pageSwitch == null || pageSwitch.uiPanels == null ||
            nextPanelIndex < 0 || nextPanelIndex >= pageSwitch.uiPanels.Length)
        {
            Debug.LogError("[NextLevelButton] 未找到有效的选关页面组件或下一关页面。", this);
            return;
        }

        hasTriggered = true;

        CanvasGroup canvasGroup = pageSwitch.GetComponentInParent<CanvasGroup>(true);
        if (canvasGroup != null)
        {
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            for (Transform parent = pageSwitch.transform; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
        }

        pageSwitch.SwitchToPage(nextPanelIndex);

        GameObject nextPanel = pageSwitch.uiPanels[nextPanelIndex];
        LevelUnlock levelUnlock = nextPanel != null ? nextPanel.GetComponent<LevelUnlock>() : null;
        if (levelUnlock != null)
            levelUnlock.PlayUnlockAnimation();
        else
            Debug.LogWarning("[NextLevelButton] 下一关页面缺少 LevelUnlock，无法播放解锁动画。", this);

        GameObject levelInstance = transform.root.gameObject;
        levelInstance.SetActive(false);
        Destroy(levelInstance);
    }
}
