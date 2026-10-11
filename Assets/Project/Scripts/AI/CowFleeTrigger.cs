using UnityEngine;

///=============================================================================
/// 牛的感应范围触发器
/// 挂在牛的【子物体】上，子物体需要一个 Collider2D 并勾选 IsTrigger。
/// 只在主角“进入瞬间”通知一次；牛逃跑期间忽略；牛停下后需主角重新进入才会再跑。
///=============================================================================
public class CowFleeTrigger : MonoBehaviour
{
    [Tooltip("主角的Tag（一般保持默认即可）")]
    public string playerTag = "Player";

    /// <summary>所属的牛（由父物体 CowFlee 自动赋值，无需手动拖）</summary>
    [HideInInspector]
    public CowFlee owner;

    void Awake()
    {
        // 兜底：如果父物体没有自动赋值，这里自行查找
        if (owner == null)
            owner = GetComponentInParent<CowFlee>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag) || owner == null)
            return;

        // 牛正在逃跑时，感应范围失效，避免重复触发
        if (owner.IsFleeing)
            return;

        owner.OnPlayerInRange(other.transform);
    }

    // 不使用 OnTriggerStay2D：
    // 否则牛在范围内停下的瞬间会被立刻再次触发，导致无法“停下后重新进入才跑”。
}
