using System.Collections;
using UnityEngine;

///=============================================================================
/// 牛的逃跑行为（挂在牛身上）
///
/// 选路模型（连续代价，不会卡死）：
/// 1. 牛向四周做密集的【圆形检测】（CircleCast，半径=牛身体半径+余量），
///    墙越近代价越高，身体已经贴进墙的方向代价极高；
/// 2. 牛始终与主角“连线”：逃跑方向越朝向主角代价越高（形成避人扇区），
///    开阔地带牛绝不会朝人跑；
/// 3. 当牛真被墙和人逼到墙角时，墙的代价 > 朝人代价，
///    牛会沿避人扇区的【边缘】贴着墙滑出去，而不是原地卡住；
/// 4. 物理接触补充：记录墙给牛的碰撞法线，移动方向若指向墙，
///    自动沿墙面切线滑动；持续位移过小还会触发“卡死自救”。
///
/// 逃跑 fleeDuration 秒（默认5秒）后停下；逃跑中感应范围失效。
///
/// 组件要求：
///   1. Rigidbody2D（Dynamic、冻结Z旋转）
///   2. Animator，含名为 "run" 的 Bool 参数
///   3. 子物体 Collider2D(IsTrigger) 挂 CowFleeTrigger 作为感应范围
///   4. 身体实体碰撞体（CircleCollider2D 最佳）
///   5. 主角 Tag = "Player"
///=============================================================================
[RequireComponent(typeof(Rigidbody2D))]
public class CowFlee : MonoBehaviour
{
    [Header("感应范围（子物体触发器，拖进来）")]
    public CowFleeTrigger triggerArea;

    [Header("移动设置")]
    public float fleeSpeed = 5f;
    [Tooltip("每次逃跑持续时间（秒）")]
    public float fleeDuration = 5f;
    [Tooltip("主角的Tag")]
    public string playerTag = "Player";

    [Header("墙体检测设置")]
    [Tooltip("墙体所在的Layer")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("检测距离（从牛中心算起，米）")]
    public float rayLength = 2f;

    [Tooltip("扫描方向数：越大越细腻（默认24个方向）")]
    [Range(8, 48)]
    public int rayCount = 24;

    [Tooltip("牛身体半径（自动取CircleCollider，也可手动覆盖）")]
    public float bodyRadius = -1f;

    [Tooltip("离墙安全余量（米），越大越早拐弯")]
    public float wallSafetyMargin = 0.15f;

    [Tooltip("是否把墙层上的Trigger碰撞体也算作墙（空气墙是Trigger时勾选）")]
    public bool triggerWallsBlock = false;

    [Header("避人设置")]
    [Tooltip("避人扇区角度（度）：开阔地带朝主角小于此角度的方向被禁")]
    [Range(0f, 180f)]
    public float playerAvoidAngle = 50f;

    [Tooltip("保持当前方向的权重，减少左右抖动")]
    public float directionStability = 0.3f;

    [Header("卡死自救")]
    [Tooltip("持续多少秒几乎没位移就触发自救")]
    public float stuckTime = 0.25f;
    [Tooltip("低于此位移比例（相对应走距离）视为没动")]
    public float stuckMoveRatio = 0.25f;

    [Header("朝向设置")]
    public bool defaultFacingRight = true;

    [Header("动画设置")]
    public string runBoolName = "run";

    private Rigidbody2D rb;
    private Animator anim;
    private float baseScaleXAbs = 1f;

    private bool isFleeing = false;
    private Coroutine fleeRoutine = null;
    private Transform playerTransform = null;
    private Vector2 currentDir = Vector2.left;

    // 墙体碰撞法线累积（OnCollisionStay2D 写入，每物理帧用完清空）
    private Vector2 contactWallNormal = Vector2.zero;

    // 卡死检测
    private Vector2 lastPos;
    private float stuckTimer = 0f;
    private bool forceUnstick = false;

    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];

    public bool IsFleeing { get { return isFleeing; } }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        baseScaleXAbs = Mathf.Abs(transform.localScale.x);
        if (baseScaleXAbs < 0.0001f) baseScaleXAbs = 1f;

        // 自动取身体圆形碰撞体半径
        if (bodyRadius <= 0f)
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle != null)
                bodyRadius = circle.bounds.extents.x;
            else
            {
                Collider2D anyCol = GetComponent<Collider2D>();
                bodyRadius = anyCol != null ? Mathf.Min(anyCol.bounds.extents.x, anyCol.bounds.extents.y) : 0.3f;
            }
        }

        if (triggerArea == null)
            triggerArea = GetComponentInChildren<CowFleeTrigger>(true);
        if (triggerArea != null)
            triggerArea.owner = this;
    }

    // ==================== 触发器调用 ====================

    public void OnPlayerInRange(Transform player)
    {
        if (isFleeing) return;
        playerTransform = player;
        if (fleeRoutine != null) StopCoroutine(fleeRoutine);
        fleeRoutine = StartCoroutine(FleeRoutine());
    }

    public void OnPlayerOutOfRange() { }

    // ==================== 物理碰撞法线 ====================

    void OnCollisionStay2D(Collision2D collision)
    {
        // 只记录墙层的碰撞法线（法线由墙指向牛，即“被推开”的方向）
        if (((1 << collision.gameObject.layer) & obstacleMask.value) == 0) return;

        for (int i = 0; i < collision.contactCount; i++)
            contactWallNormal += collision.GetContact(i).normal;
    }

    // ==================== 逃跑协程 ====================

    private IEnumerator FleeRoutine()
    {
        isFleeing = true;
        lastPos = transform.position;
        stuckTimer = 0f;
        forceUnstick = false;

        if (anim != null) anim.SetBool(runBoolName, true);

        float timer = 0f;
        while (timer < fleeDuration)
        {
            // 1. 选方向（用上一次物理步收集到的墙法线）
            currentDir = ChooseEscapeDirection();

            // 2. 贴墙滑动：若选择方向指向墙，投影到墙面切线
            if (contactWallNormal.sqrMagnitude > 0.0001f)
            {
                Vector2 n = contactWallNormal.normalized;
                if (Vector2.Dot(currentDir, n) < 0f)
                {
                    Vector2 slide = Vector2.Perpendicular(n);
                    // 选与当前逃跑方向更接近的那条切线
                    if (Vector2.Dot(slide, currentDir) < 0f) slide = -slide;
                    // 混入一点“离开墙”的分量，避免一直蹭墙
                    currentDir = (slide + n * 0.25f).normalized;
                }
            }

            rb.velocity = currentDir * fleeSpeed;
            ApplyFacing(currentDir.x);

            // 3. 卡死检测
            float moved = Vector2.Distance(lastPos, transform.position);
            float expected = fleeSpeed * Time.fixedDeltaTime;
            if (moved < expected * stuckMoveRatio)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= stuckTime) forceUnstick = true;
            }
            else
            {
                stuckTimer = 0f;
                forceUnstick = false;
            }
            lastPos = transform.position;

            // 本帧法线用完清空，下一帧重新收集
            contactWallNormal = Vector2.zero;

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        StopFlee();
    }

    // ==================== 连续代价选路 ====================

    private Vector2 ChooseEscapeDirection()
    {
        Vector2 pos = transform.position;

        // 牛→主角连线
        Vector2 toPlayer = Vector2.zero;
        Vector2 awayPlayer = Vector2.zero;
        if (playerTransform != null)
        {
            toPlayer = (Vector2)playerTransform.position - pos;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                toPlayer.Normalize();
                awayPlayer = -toPlayer;
            }
        }

        // 已经扎进墙里时的推出方向
        Vector2 overlapPush = GetOverlapPushDirection(pos);

        Vector2 bestDir = Vector2.zero;
        float bestPenalty = float.MaxValue;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i + (180f / rayCount);
            Vector2 dir = DegreeToDirection(angle);

            float penalty = 0f;

            // —— 代价1：墙体（CircleCast，按身体半径检测）——
            float clearance = ProbeClearance(pos, dir); // 净空距离
            if (clearance < 0f)
            {
                // 身体已嵌入/贴着墙：极高代价，且方向与推出方向相反时再加罚
                penalty += 10000f;
                if (overlapPush.sqrMagnitude > 0.0001f && Vector2.Dot(dir, overlapPush) < 0f)
                    penalty += 5000f;
            }
            else
            {
                float t = Mathf.Clamp01(1f - clearance / rayLength); // 0=开阔 1=贴墙
                penalty += 1200f * t * t;
            }

            // —— 代价2：朝主角扇区（开阔时是硬禁，墙角时让位于墙）——
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                float dot = Vector2.Dot(dir, toPlayer);
                float cone = Mathf.Cos(playerAvoidAngle * Mathf.Deg2Rad);
                if (dot > cone)
                    penalty += 600f * (dot - cone) / (1f - cone); // 越正对主角罚得越多
            }

            // —— 代价3：与当前方向不一致（防抖）——
            penalty += (1f - Vector2.Dot(dir, currentDir)) * directionStability * 100f;

            // —— 卡死自救：优先沿墙法线推出方向 ——
            if (forceUnstick && contactWallNormal.sqrMagnitude > 0.0001f)
                penalty -= Vector2.Dot(dir, contactWallNormal.normalized) * 3000f;

            // 同等代价下，越远离主角越好（用极小权重做排序倾向）
            penalty -= (awayPlayer.sqrMagnitude > 0.0001f ? Vector2.Dot(dir, awayPlayer) : 0f) * 0.01f;

            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestDir = dir;
            }
        }

        return bestDir.sqrMagnitude > 0.0001f ? bestDir : currentDir;
    }

    /// <summary>
    /// 圆形检测某方向净空：返回“身体边缘还能向前走多远”。
    /// 负数表示身体已与墙重叠。
    /// </summary>
    private float ProbeClearance(Vector2 pos, Vector2 dir)
    {
        int count = Physics2D.CircleCastNonAlloc(
            pos, bodyRadius, dir, hitBuffer, rayLength);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i].collider;
            if (col == null) continue;
            if (col.attachedRigidbody == rb) continue;
            if (col.isTrigger && !triggerWallsBlock) continue;
            if (((1 << col.gameObject.layer) & obstacleMask.value) == 0) continue;

            if (hitBuffer[i].distance < nearest)
                nearest = hitBuffer[i].distance;
        }

        if (nearest < float.MaxValue - 1f)
            return nearest - bodyRadius - wallSafetyMargin;

        return rayLength;
    }

    /// <summary>检测身体是否已与墙重叠，返回推出方向</summary>
    private Vector2 GetOverlapPushDirection(Vector2 pos)
    {
        Vector2 push = Vector2.zero;
        int count = Physics2D.OverlapCircleNonAlloc(pos, bodyRadius * 0.95f, overlapBuffer, obstacleMask);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col == null) continue;
            if (col.attachedRigidbody == rb) continue;
            if (col.isTrigger && !triggerWallsBlock) continue;

            Vector2 closest = col.ClosestPoint(pos);
            Vector2 away = pos - closest;
            if (away.sqrMagnitude < 0.0001f)
                away = pos - (Vector2)col.bounds.center;
            if (away.sqrMagnitude > 0.0001f)
                push += away.normalized;
        }
        return push.normalized;
    }

    private Vector2 DegreeToDirection(float degree)
    {
        float rad = degree * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void StopFlee()
    {
        isFleeing = false;
        fleeRoutine = null;
        playerTransform = null;
        rb.velocity = Vector2.zero;
        if (anim != null) anim.SetBool(runBoolName, false);
    }

    private void ApplyFacing(float moveX)
    {
        if (Mathf.Approximately(moveX, 0f)) return;
        float dirSign = moveX > 0 ? 1f : -1f;
        if (!defaultFacingRight) dirSign = -dirSign;

        Vector3 scale = transform.localScale;
        scale.x = baseScaleXAbs * dirSign;
        transform.localScale = scale;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 扫描方向
        Gizmos.color = Color.cyan;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i + (180f / rayCount);
            Vector2 d = DegreeToDirection(angle);
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + d * rayLength);
        }

        // 身体半径
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, bodyRadius > 0 ? bodyRadius : 0.3f);

        // 与主角连线
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }
#endif
}
