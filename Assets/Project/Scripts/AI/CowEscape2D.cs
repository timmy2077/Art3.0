using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public sealed class CowEscape2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Collider2D cowCollider;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Animator animator;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Escape Triggers")]
    [Tooltip("Inner circle: starts escape when the player's body enters.")]
    [SerializeField] private CircleCollider2D startEscapeTrigger;
    [Tooltip("Outer circle: stops escape only when the player's whole body leaves.")]
    [SerializeField] private CircleCollider2D stopEscapeTrigger;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
    [SerializeField, Range(8, 64)] private int directionCount = 16;
    [SerializeField, Min(0.1f)] private float lookAhead = 2f;
    [SerializeField, Min(0.005f)] private float wallPadding = 0.05f;
    [SerializeField, Min(0f)] private float directionPersistence = 0.45f;

    [Header("Facing")]
    [Tooltip("牛的美术素材在 scale.x 为正时是否面朝右。若逃跑时牛反而对着你跑，取消此勾选")]
    [SerializeField] private bool defaultFacingRight = true;

    [Header("Optional Wall Unstuck")]
    [Tooltip("Temporarily ignore only obstacle collisions when escape movement is blocked.")]
    [SerializeField] private bool ignoreWallsWhenStuck = false;
    [SerializeField, Min(0.05f)] private float minimumWallIgnoreSeconds = 0.35f;
    [Tooltip("After this time, try a safe teleport if still inside a wall.")]
    [SerializeField, Min(0.1f)] private float wallIgnoreTeleportAfter = 2f;

    [Header("Teleport")]
    [Tooltip("总开关：是否允许牛在危急/卡住时瞬移。关闭后牛只做射线寻路和贴墙滑行，绝不瞬移。")]
    [SerializeField] private bool enableTeleport = false;

    [Tooltip("Extra gap between the cow and player bodies that triggers teleport.")]
    [SerializeField, Min(0.05f)] private float dangerGap = 0.6f;
    [SerializeField, Min(0.1f)] private float teleportMinDistance = 5f;
    [SerializeField, Min(0.1f)] private float teleportMaxDistance = 10f;
    [SerializeField, Range(8, 256)] private int teleportAttempts = 80;
    [SerializeField, Min(0.05f)] private float stuckSeconds = 0.5f;
    [SerializeField, Min(0.05f)] private float failedTeleportRetry = 0.2f;
    [Tooltip("World-space rectangle. It must match your playable map.")]
    [SerializeField] private Rect worldBounds = new Rect(-20f, -20f, 40f, 40f);

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private readonly RaycastHit2D[] slideHits = new RaycastHit2D[32];
    private readonly Collider2D[] landingHits = new Collider2D[1];
    private static readonly int RunParameter = Animator.StringToHash("run");
    private Rigidbody2D body;
    private ContactFilter2D obstacleFilter;
    private Vector2 lastDirection;
    private float baseScaleXAbs = 1f;
    private Vector2 previousCowPosition;
    private Vector2 previousPlayerPosition;
    private float blockedTime;
    private float nextTeleportAttempt;
    private Collider2D[] playerColliders;
    private bool isEscaping;
    private bool attemptedMovement;
    private bool isIgnoringWalls;
    private float wallIgnoreTime;
    private readonly Dictionary<Collider2D, bool> ignoredWalls = new Dictionary<Collider2D, bool>();

    public bool IsEscaping { get { return isEscaping; } }
    public bool IsIgnoringWalls { get { return isIgnoringWalls; } }
    public bool IgnoreWallsWhenStuck
    {
        get { return ignoreWallsWhenStuck; }
        set { ignoreWallsWhenStuck = value; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        baseScaleXAbs = Mathf.Abs(transform.localScale.x);
        if (baseScaleXAbs < 0.0001f) baseScaleXAbs = 1f;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        SetRunning(false);
        if (cowCollider == null)
            cowCollider = GetComponent<Collider2D>();
        if (playerCollider == null && player != null)
            playerCollider = player.GetComponent<Collider2D>();

        if (player == null || cowCollider == null ||
            cowCollider.attachedRigidbody != body || cowCollider.isTrigger)
        {
            Debug.LogError("CowEscape2D needs a player and a non-trigger cow collider attached to its Rigidbody2D.", this);
            enabled = false;
            return;
        }

        playerColliders = player.GetComponentsInChildren<Collider2D>(true);
        if (playerCollider == null)
        {
            for (int i = 0; i < playerColliders.Length; i++)
            {
                if (!playerColliders[i].isTrigger)
                {
                    playerCollider = playerColliders[i];
                    break;
                }
            }
        }

        if (!ValidEscapeTriggers())
        {
            Debug.LogError("CowEscape2D needs two active child CircleCollider2D triggers on the cow's Rigidbody2D. The outer stop circle must fully contain the inner start circle with an extra margin.", this);
            enabled = false;
            return;
        }
        if (playerCollider == null || playerCollider.isTrigger)
        {
            Debug.LogError("CowEscape2D needs a non-trigger Player Collider. Assign Player to the player's root object.", this);
            enabled = false;
            return;
        }

        obstacleFilter = new ContactFilter2D();
        obstacleFilter.SetLayerMask(obstacleLayers);
        obstacleFilter.useTriggers = false;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (obstacleLayers.value == 0)
            Debug.LogWarning("CowEscape2D: assign obstacleLayers to the wall/obstacle layers.", this);

        previousCowPosition = body.position;
        previousPlayerPosition = PlayerPosition;
    }

    private Vector2 PlayerPosition
    {
        get { return playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position; }
    }

    private float CowRadius
    {
        get
        {
            Vector3 extent = cowCollider.bounds.extents;
            if (cowCollider is CircleCollider2D)
                return Mathf.Max(extent.x, extent.y);
            // A conservative enclosing circle also supports box/capsule bodies.
            return new Vector2(extent.x, extent.y).magnitude;
        }
    }

    private float PlayerRadius
    {
        get
        {
            if (playerCollider == null)
                return 0.5f;
            Vector3 extent = playerCollider.bounds.extents;
            if (playerCollider is CircleCollider2D)
                return Mathf.Max(extent.x, extent.y);
            return new Vector2(extent.x, extent.y).magnitude;
        }
    }

    private void FixedUpdate()
    {
        if (player == null || cowCollider == null || !cowCollider.enabled)
        {
            StopEscaping();
            return;
        }

        Vector2 playerPosition = PlayerPosition;
        Vector2 cowPosition = cowCollider.bounds.center;
        float step = moveSpeed * Time.fixedDeltaTime;
        float playerSpeed = Vector2.Distance(playerPosition, previousPlayerPosition) / Time.fixedDeltaTime;
        previousPlayerPosition = playerPosition;

        // Include one physics step of closing distance in the emergency margin.
        float emergencyDistance = CowRadius + PlayerRadius + dangerGap +
                                  (playerSpeed + moveSpeed) * Time.fixedDeltaTime;

        UpdateEscapeState();
        if (isIgnoringWalls)
        {
            wallIgnoreTime += Time.fixedDeltaTime;
            IgnoreNearbyWalls();
            bool clearOfWalls = ClearOfWalls(cowPosition);
            if (clearOfWalls && (wallIgnoreTime >= minimumWallIgnoreSeconds ||
                                !ignoreWallsWhenStuck || !isEscaping))
            {
                RestoreWallCollisions();
            }
            else if (!ignoreWallsWhenStuck || wallIgnoreTime >= wallIgnoreTeleportAfter)
            {
                if (TryTeleport(playerPosition, emergencyDistance))
                    return;
            }

            // If the outer sensor loses the player inside a wall, finish exiting
            // before obeying the stop transition. Never restore collisions in a wall.
            if (isIgnoringWalls && !clearOfWalls)
                isEscaping = true;
        }
        if (!isEscaping)
        {
            StopEscaping();
            previousCowPosition = body.position;
            return;
        }

        float actualMovement = Vector2.Distance(body.position, previousCowPosition);
        previousCowPosition = body.position;
        if (attemptedMovement && actualMovement < step * 0.1f)
            blockedTime += Time.fixedDeltaTime;
        else
            blockedTime = 0f;

        bool inDanger = Vector2.Distance(cowPosition, playerPosition) <= emergencyDistance;
        bool outsideMap = !InsideBounds(cowPosition, CowRadius + wallPadding);

        if (ignoreWallsWhenStuck && !isIgnoringWalls && blockedTime >= stuckSeconds)
        {
            isIgnoringWalls = true;
            wallIgnoreTime = 0f;
            blockedTime = 0f;
            IgnoreNearbyWalls();
        }

        if (inDanger || outsideMap ||
            (!ignoreWallsWhenStuck && !isIgnoringWalls && blockedTime >= stuckSeconds))
        {
            if (TryTeleport(playerPosition, emergencyDistance))
                return;
        }

        Vector2 away = cowPosition - playerPosition;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;

        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        float bestTravel = 0f;
        float scanDistance = Mathf.Max(lookAhead, step + wallPadding);
        attemptedMovement = true;

        for (int i = 0; i < directionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / directionCount;
            EvaluateDirection(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), away,
                cowPosition, scanDistance, step, ref bestDirection, ref bestScore, ref bestTravel);
        }

        // Explicit tangents allow exact sideways escape regardless of sample angles.
        Vector2 tangent = new Vector2(-away.y, away.x);
        EvaluateDirection(away, away, cowPosition, scanDistance, step,
            ref bestDirection, ref bestScore, ref bestTravel);
        EvaluateDirection(tangent, away, cowPosition, scanDistance, step,
            ref bestDirection, ref bestScore, ref bestTravel);
        EvaluateDirection(-tangent, away, cowPosition, scanDistance, step,
            ref bestDirection, ref bestScore, ref bestTravel);

        // Add wall-slide candidates from the direct escape cast.
        int hitCount = isIgnoringWalls ? 0 : cowCollider.Cast(away, obstacleFilter, slideHits, scanDistance);
        for (int i = 0; i < hitCount; i++)
        {
            Vector2 normal = slideHits[i].normal;
            Vector2 slide = away - Vector2.Dot(away, normal) * normal;
            if (slide.sqrMagnitude > 0.0001f)
                EvaluateDirection(slide.normalized, away, cowPosition, scanDistance, step,
                    ref bestDirection, ref bestScore, ref bestTravel);
        }

        if (bestDirection == Vector2.zero)
        {
            SetRunning(false);
            lastDirection = Vector2.zero;
            if (!ignoreWallsWhenStuck || isIgnoringWalls)
                TryTeleport(playerPosition, emergencyDistance);
            body.velocity = Vector2.zero;
            return;
        }

        lastDirection = bestDirection;
        body.velocity = Vector2.zero;
        float travel = Mathf.Min(step, bestTravel);
        SetRunning(travel > 0.0001f);
        body.MovePosition(body.position + bestDirection * travel);
        ApplyFacing(bestDirection.x);
    }

    /// <summary>按逃跑方向翻转朝向（面朝逃跑方向，即背对主角）</summary>
    private void ApplyFacing(float moveX)
    {
        if (Mathf.Approximately(moveX, 0f))
            return;

        float dirSign = moveX > 0f ? 1f : -1f;
        if (!defaultFacingRight) dirSign = -dirSign;

        Vector3 scale = transform.localScale;
        scale.x = baseScaleXAbs * dirSign;
        transform.localScale = scale;
    }

    private void EvaluateDirection(Vector2 direction, Vector2 away, Vector2 position,
        float scanDistance, float step, ref Vector2 bestDirection, ref float bestScore, ref float bestTravel)
    {
        float alignment = Vector2.Dot(direction, away);
        if (alignment < -0.00001f)
            return;

        float clearance = AvailableDistance(direction, position, scanDistance);
        if (clearance < Mathf.Min(step, 0.02f))
            return;

        float score = alignment * 2f + clearance / scanDistance * 2.5f +
                      Vector2.Dot(direction, lastDirection) * directionPersistence;
        if (score <= bestScore)
            return;

        bestScore = score;
        bestDirection = direction;
        bestTravel = clearance;
    }

    private float AvailableDistance(Vector2 direction, Vector2 position, float distance)
    {
        int count = isIgnoringWalls ? 0 : cowCollider.Cast(direction, obstacleFilter, castHits, distance);
        // A full buffer may hide a nearer hit; conservatively reject this direction.
        if (count == castHits.Length)
            return 0f;
        for (int i = 0; i < count; i++)
            distance = Mathf.Min(distance, Mathf.Max(0f, castHits[i].distance - wallPadding));

        float radius = CowRadius + wallPadding;
        if (direction.x > 0.00001f)
            distance = Mathf.Min(distance, (worldBounds.xMax - radius - position.x) / direction.x);
        else if (direction.x < -0.00001f)
            distance = Mathf.Min(distance, (worldBounds.xMin + radius - position.x) / direction.x);
        if (direction.y > 0.00001f)
            distance = Mathf.Min(distance, (worldBounds.yMax - radius - position.y) / direction.y);
        else if (direction.y < -0.00001f)
            distance = Mathf.Min(distance, (worldBounds.yMin + radius - position.y) / direction.y);
        return Mathf.Max(0f, distance);
    }

    private bool TryTeleport(Vector2 playerPosition, float emergencyDistance)
    {
        // 总开关关闭时绝不瞬移
        if (!enableTeleport)
            return false;

        if (Time.time < nextTeleportAttempt)
            return false;
        nextTeleportAttempt = Time.time + failedTeleportRetry;

        float minDistance = Mathf.Max(teleportMinDistance, emergencyDistance + dangerGap);
        float maxDistance = Mathf.Max(teleportMaxDistance, minDistance + 0.1f);
        float radius = CowRadius + wallPadding;

        for (int i = 0; i < teleportAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(Random.Range(minDistance * minDistance, maxDistance * maxDistance));
            Vector2 target = playerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (!InsideBounds(target, radius))
                continue;
            if (Physics2D.OverlapCircle(target, radius, obstacleFilter, landingHits) > 0)
                continue;

            Vector2 centerOffset = (Vector2)cowCollider.bounds.center - body.position;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = target - centerOffset;
            Physics2D.SyncTransforms();
            RestoreWallCollisions();
            previousCowPosition = body.position;
            blockedTime = 0f;
            lastDirection = Vector2.zero;
            attemptedMovement = false;
            nextTeleportAttempt = 0f;
            SetRunning(false);
            ApplyFacing(target.x - playerPosition.x);
            return true;
        }
        return false;
    }

    private bool InsideBounds(Vector2 center, float radius)
    {
        return center.x - radius >= worldBounds.xMin && center.x + radius <= worldBounds.xMax &&
               center.y - radius >= worldBounds.yMin && center.y + radius <= worldBounds.yMax;
    }

    private void SetRunning(bool running)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(RunParameter, running);
    }

    private bool ClearOfWalls(Vector2 center)
    {
        return Physics2D.OverlapCircle(center, CowRadius + wallPadding, obstacleFilter, landingHits) == 0;
    }

    private void IgnoreNearbyWalls()
    {
        // Register just nearby pairs, not a global layer rule affecting other cows.
        float range = CowRadius + Mathf.Max(lookAhead, moveSpeed * Time.fixedDeltaTime) + wallPadding;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(cowCollider.bounds.center, range, obstacleLayers);
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D wall = nearby[i];
            if (wall == null || wall.isTrigger || wall.attachedRigidbody == body)
                continue;
            if (!ignoredWalls.ContainsKey(wall))
                ignoredWalls.Add(wall, Physics2D.GetIgnoreCollision(cowCollider, wall));
            // Reapply: Unity can reset ignore pairs when colliders are deactivated.
            Physics2D.IgnoreCollision(cowCollider, wall, true);
        }
    }

    private void RestoreWallCollisions()
    {
        if (cowCollider != null)
        {
            foreach (KeyValuePair<Collider2D, bool> pair in ignoredWalls)
            {
                if (pair.Key != null)
                    Physics2D.IgnoreCollision(cowCollider, pair.Key, pair.Value);
            }
        }
        ignoredWalls.Clear();
        isIgnoringWalls = false;
        wallIgnoreTime = 0f;
        blockedTime = 0f;
    }

    private void UpdateEscapeState()
    {
        if (startEscapeTrigger == null || stopEscapeTrigger == null ||
            !startEscapeTrigger.isActiveAndEnabled || !stopEscapeTrigger.isActiveAndEnabled)
        {
            StopEscaping();
            return;
        }

        // Only one transition is evaluated per state; the inner exit never stops escape.
        // Query overlaps instead of callbacks so teleport and multiple player colliders
        // cannot cause competing Enter/Exit events to overwrite the state.
        if (!isEscaping)
        {
            if (IsPlayerInside(startEscapeTrigger))
                isEscaping = true;
        }
        else if (!IsPlayerInside(stopEscapeTrigger))
        {
            StopEscaping();
        }
    }

    private bool IsPlayerInside(Collider2D sensor)
    {
        if (OverlapsPlayerBody(sensor, playerCollider))
            return true;
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (OverlapsPlayerBody(sensor, playerColliders[i]))
                return true;
        }
        return false;
    }

    private static bool OverlapsPlayerBody(Collider2D sensor, Collider2D target)
    {
        if (target == null || !target.isActiveAndEnabled || target.isTrigger)
            return false;
        ColliderDistance2D separation = sensor.Distance(target);
        return separation.isValid && separation.distance <= 0f;
    }

    private bool ValidEscapeTriggers()
    {
        if (startEscapeTrigger == null || stopEscapeTrigger == null ||
            startEscapeTrigger == stopEscapeTrigger ||
            !startEscapeTrigger.isTrigger || !stopEscapeTrigger.isTrigger ||
            !startEscapeTrigger.isActiveAndEnabled || !stopEscapeTrigger.isActiveAndEnabled ||
            startEscapeTrigger.attachedRigidbody != body || stopEscapeTrigger.attachedRigidbody != body)
            return false;

        float innerRadius = Mathf.Max(startEscapeTrigger.bounds.extents.x, startEscapeTrigger.bounds.extents.y);
        float outerRadius = Mathf.Max(stopEscapeTrigger.bounds.extents.x, stopEscapeTrigger.bounds.extents.y);
        float centerDistance = Vector2.Distance(startEscapeTrigger.bounds.center, stopEscapeTrigger.bounds.center);
        return outerRadius > centerDistance + innerRadius + wallPadding;
    }

    private void StopEscaping()
    {
        isEscaping = false;
        lastDirection = Vector2.zero;
        attemptedMovement = false;
        blockedTime = 0f;
        nextTeleportAttempt = 0f;
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        SetRunning(false);
    }

    private void OnDisable()
    {
        if (isIgnoringWalls && cowCollider != null && player != null &&
            !ClearOfWalls(cowCollider.bounds.center))
        {
            nextTeleportAttempt = 0f;
            float safeDistance = CowRadius + PlayerRadius + dangerGap;
            if (!TryTeleport(PlayerPosition, safeDistance))
                Debug.LogWarning("CowEscape2D was disabled inside a wall without a safe teleport point. Collision pairs will still be restored.", this);
        }
        RestoreWallCollisions();
        StopEscaping();
    }

    private void OnValidate()
    {
        directionCount = Mathf.Clamp(directionCount, 8, 64);
        teleportAttempts = Mathf.Clamp(teleportAttempts, 8, 256);
        teleportMaxDistance = Mathf.Max(teleportMaxDistance, teleportMinDistance + 0.1f);
        wallIgnoreTeleportAfter = Mathf.Max(wallIgnoreTeleportAfter, minimumWallIgnoreSeconds);
        worldBounds.width = Mathf.Max(0.1f, worldBounds.width);
        worldBounds.height = Mathf.Max(0.1f, worldBounds.height);
    }
}
