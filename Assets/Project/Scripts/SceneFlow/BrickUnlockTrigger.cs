using UnityEngine;

public class BrickUnlockTrigger : MonoBehaviour
{
    [Tooltip("与 RecordUI 中的石砖 ID 相同。")]
    [SerializeField] private string brickId;

    public void Unlock()
    {
        if (DataSystem.instance != null)
            DataSystem.instance.UnlockBrick(brickId);
        else
            Debug.LogWarning("[BrickUnlockTrigger] 未找到 DataSystem。", this);
    }
}
