using UnityEngine;

///=============================================================================
/// NPC 交互脚本
/// 挂在 NPC 物体上，负责检测玩家进入范围、显示交互提示、按E触发对话。
///
/// 组件要求：
///   1. 此脚本挂在 NPC 物体上
///   2. NPC 物体需要有 Collider（2D 或 3D），勾选 IsTrigger
///   3. 玩家物体需要有 Collider + Rigidbody（2D 或 3D），Tag 设为 "Player"
///   4. 创建对话数据资产并拖入 dialogueData 字段
///=============================================================================
public class NPCInteraction : MonoBehaviour
{
    [Header("对话数据")]
    [Tooltip("拖入此NPC对应的对话数据资产（右键 Create → 尘封砖语 → 对话数据）")]
    public DialogueData dialogueData;

    [Header("交互设置")]
    [Tooltip("玩家的Tag")]
    public string playerTag = "Player";

    [Tooltip("是否使用2D碰撞器（2D游戏勾选，3D游戏取消）")]
    public bool use2DCollider = true;

    [Header("状态（运行时自动更新，无需手动设置）")]
    [Tooltip("科普线是否已完成")]
    public bool IsScienceCompleted = false;

    // 玩家是否在交互范围内
    private bool playerInRange = false;

    /// <summary>玩家是否在范围内</summary>
    public bool PlayerInRange => playerInRange;

    // ==================== 2D 碰撞检测 ====================

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
            ShowHint();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
            HideHint();
            NotifyPlayerLeft();
        }
    }

    // ==================== 3D 碰撞检测 ====================

    void OnTriggerEnter(Collider other)
    {
        if (use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
            ShowHint();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (use2DCollider) return;
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
            HideHint();
            NotifyPlayerLeft();
        }
    }

    // ==================== 按键交互 ====================

    void Update()
    {
        if (dialogueData == null) return;
        if (!playerInRange) return;

        var dm = DialogueManager.Instance;
        if (dm == null) return;

        // 对话进行中不响应E键
        if (dm.IsDialogueActive) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            dm.StartDialogue(this);
        }
    }

    // ==================== 提示显示 ====================

    private void ShowHint()
    {
        var dm = DialogueManager.Instance;
        if (dm != null && dm.dialogueUI != null)
            dm.dialogueUI.ShowInteractionHint();
    }

    private void HideHint()
    {
        var dm = DialogueManager.Instance;
        if (dm != null && dm.dialogueUI != null)
            dm.dialogueUI.HideInteractionHint();
    }

    /// <summary>通知 DialogueManager 玩家离开了NPC范围</summary>
    private void NotifyPlayerLeft()
    {
        var dm = DialogueManager.Instance;
        if (dm != null)
            dm.OnPlayerLeftNPC();
    }

    /// <summary>对话结束时由 DialogueManager 调用</summary>
    public void OnDialogueEnded()
    {
        // 如果玩家还在范围内，重新显示交互提示
        if (playerInRange) ShowHint();
    }

    // ==================== 调试可视化 ====================

    /// <summary>在Scene视图显示交互范围（仅当有Collider时）</summary>
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);

        if (use2DCollider)
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
        else
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}
