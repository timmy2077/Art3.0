using UnityEngine;

/// <summary>Tracks one cow's arrival in the shared pen. No gate or repair interaction is required.</summary>
public sealed class CowPen : MonoBehaviour
{
    [Tooltip("本组件记录归栏的牛；同一个牛栏分别挂白牛和黑牛两份组件。")]
    public CowEscape2D cow;
    [Tooltip("牛身体的碰撞体，不是感知范围。整个身体进入判定框才完成。")]
    public CircleCollider2D cowBody;
    [Tooltip("牛栏内部判定框，两头牛共用同一个触发框。")]
    public BoxCollider2D interior;
    [Tooltip("按 R 时未归栏的牛返回的位置（世界坐标）。")]
    public Vector2 spawnPosition;
    [Tooltip("归栏后停留位置（世界坐标），必须在判定框内。")]
    public Vector2 restingPosition;
    [Tooltip("任务编号：白牛 1，黑牛 2。")]
    public int objectiveId;
    public bool Captured { get; private set; }

    public void Restore(bool captured)
    {
        Captured = captured;
        if (captured) SettleCow();
    }
    public bool CowFullyInside()
    {
        Bounds pen = interior.bounds, body = cowBody.bounds;
        return body.min.x > pen.min.x && body.max.x < pen.max.x && body.min.y > pen.min.y && body.max.y < pen.max.y;
    }
    public bool TryRecordArrival()
    {
        if (Captured || !CowFullyInside()) return false;
        Captured = true;
        SettleCow();
        return true;
    }
    private void SettleCow()
    {
        cow.enabled = false;
        Rigidbody2D body = cowBody.attachedRigidbody;
        body.velocity = Vector2.zero;
        body.position = restingPosition;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        Animator animator = cow.GetComponent<Animator>();
        if (animator != null) animator.SetBool("run", false);
    }
    public void ResetLooseCow()
    {
        if (Captured) return;
        cow.enabled = false;
        Rigidbody2D body = cowBody.attachedRigidbody;
        body.velocity = Vector2.zero;
        body.position = spawnPosition;
        cow.enabled = true;
    }
}
