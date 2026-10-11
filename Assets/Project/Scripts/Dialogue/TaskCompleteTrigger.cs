using UnityEngine;

///=============================================================================
/// 任务完成触发器
/// 挂在触发区域物体上（需要 Collider 并勾选 IsTrigger）。
/// 玩家进入该范围时，通知玩家身上的 PlayerTaskManager 完成指定ID的任务。
///
/// 组件要求：
///   1. 此脚本挂在触发区域物体上
///   2. 物体需要有 Collider（2D 或 3D），勾选 IsTrigger
///   3. 玩家物体 Tag 设为 "Player"，身上挂 PlayerTaskManager
///=============================================================================
public class TaskCompleteTrigger : MonoBehaviour
{
    [Header("任务设置")]
    [Tooltip("进入此区域要完成的任务ID（对应任务储存里的 taskId）")]
    public int taskId = 0;

    [Tooltip("玩家的Tag")]
    public string playerTag = "Player";

    [Tooltip("是否使用2D碰撞器（2D游戏勾选，3D游戏取消）")]
    public bool use2DCollider = true;

    // ==================== 2D 碰撞检测 ====================

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            NotifyComplete();
        }
    }

    // ==================== 3D 碰撞检测 ====================

    void OnTriggerEnter(Collider other)
    {
        if (use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            NotifyComplete();
        }
    }

    // ==================== 完成任务 ====================

    private void NotifyComplete()
    {
        if (PlayerTaskManager.Instance != null)
        {
            PlayerTaskManager.Instance.CompleteTask(taskId);
        }
        else
        {
            Debug.LogWarning("[TaskCompleteTrigger] 场景中未找到 PlayerTaskManager！");
        }
    }

    // ==================== 调试可视化 ====================

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

        if (use2DCollider)
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            var col = GetComponent<Collider>();
            if (col != null)
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
