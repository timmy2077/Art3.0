using Cinemachine;
using UnityEngine;

/// <summary>第二关内外区域切换，共用一个玩家、一个庖长和同一套任务状态。</summary>
public sealed class CookingRestaurant : MonoBehaviour
{
    public CookingPuzzle puzzle;
    public GameObject exterior, interior;
    public PolygonCollider2D exteriorBounds, interiorBounds;
    public Transform entrance, exit, restaurantName;
    public BoxCollider2D buildingCollider;
    public Transform exteriorSpawn, interiorSpawn, exteriorChef, interiorChef;
    public CinemachineVirtualCamera virtualCamera;
    public Camera fieldCamera;
    public float exteriorCameraSize = 5, interiorCameraSize = 6;
    [Min(.5f)] public float interactionDistance = 2.7f;
    public bool IsInside { get; private set; }
    public bool IsReady => initialized && Time.unscaledTime >= readyAt;
    private bool initialized;
    private float readyAt;
    private string SaveKey => "Quest." + puzzle.storage.questId + ".Cooking.InRestaurant";

    public void RestoreArea()
    {
        bool inside = initialized && puzzle.tasks.PersistenceEnabled && SaveManager.Instance.GetInt(SaveKey, 0) == 1;
        if (!initialized || inside != IsInside) ChangeArea(inside, false);
    }

    public bool TryUseDoor()
    {
        Transform door = IsInside ? exit : entrance;
        if (!IsReady || door == null || !door.gameObject.activeInHierarchy ||
            !IsNearDoor(door) ||
            puzzle.IsInteractionBlocked || DebugUnlockPanel.BlocksGameplayInput ||
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)) return false;
        ChangeArea(!IsInside, true);
        return true;
    }

    private bool IsNearDoor(Transform door)
    {
        Vector2 position = puzzle.player.transform.position;
        // 外场按建筑实体判断，避免图片移动后隐藏入口点与建筑脱节。
        if (!IsInside && buildingCollider != null && buildingCollider.isActiveAndEnabled)
            return Vector2.Distance(position, buildingCollider.ClosestPoint(position)) <= interactionDistance;
        return Vector2.Distance(position, door.position) <= interactionDistance;
    }

    private void ChangeArea(bool inside, bool save)
    {
        bool first = !initialized;
        initialized = true; IsInside = inside;
        var player = puzzle.player;
        Vector3 previous = player.transform.position;
        Vector3 destination = inside ? interiorSpawn.position : first ? exteriorSpawn.position : entrance.position + Vector3.down * 1.8f;
        // 先清除按住的触屏方向和物理速度，避免穿门后继续滑行。
        player.TouchMove = Vector2.zero;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.velocity = Vector2.zero; body.position = destination; }
        player.transform.position = destination;
        if (puzzle.chef != null)
        {
            puzzle.chef.ResetInteractionRange();
            puzzle.chef.transform.position = inside ? interiorChef.position : exteriorChef.position;
        }
        exterior.SetActive(!inside); exteriorBounds.gameObject.SetActive(!inside);
        interior.SetActive(inside); interiorBounds.gameObject.SetActive(inside);
        exit.gameObject.SetActive(inside);
        Physics2D.SyncTransforms();
        if (virtualCamera != null)
        {
            var confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();
            if (confiner != null) { confiner.m_BoundingShape2D = inside ? interiorBounds : exteriorBounds; confiner.InvalidateCache(); }
            virtualCamera.m_Lens.OrthographicSize = inside ? interiorCameraSize : exteriorCameraSize;
            virtualCamera.OnTargetObjectWarped(player.transform, destination - previous);
            virtualCamera.PreviousStateIsValid = false;
        }
        if (fieldCamera != null) fieldCamera.transform.position = destination + Vector3.back * 10;
        readyAt = Time.unscaledTime + .35f;
        if (save && puzzle.tasks.PersistenceEnabled)
        {
            SaveManager.Instance.SetInt(SaveKey, inside ? 1 : 0);
            SaveManager.Instance.Save();
        }
        if (!first) puzzle.OnRestaurantAreaChanged();
    }
}
