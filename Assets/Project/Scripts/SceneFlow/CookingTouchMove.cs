using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CookingTouchMove : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public PlayerMovement player;
    public Vector2 direction;
    public void OnPointerDown(PointerEventData e) { if (player != null) player.TouchMove=direction; }
    public void OnPointerUp(PointerEventData e) { if (player != null) player.TouchMove=Vector2.zero; }
    private void OnDisable() { if (player != null) player.TouchMove=Vector2.zero; }
}
