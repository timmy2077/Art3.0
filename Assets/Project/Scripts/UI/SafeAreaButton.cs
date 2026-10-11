using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaButton : MonoBehaviour
{
    [SerializeField] private float margin = 24f;

    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 originalPosition;
    private Rect previousSafeArea;
    private Vector2Int previousScreenSize;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        originalPosition = rectTransform.anchoredPosition;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        Rect safeArea = Screen.safeArea;
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (safeArea == previousSafeArea && size == previousScreenSize) return;
        previousSafeArea = safeArea;
        previousScreenSize = size;

        rectTransform.anchoredPosition = originalPosition;
        Canvas.ForceUpdateCanvases();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float firstX = RectTransformUtility.WorldToScreenPoint(camera, corners[0]).x;
        float oppositeX = RectTransformUtility.WorldToScreenPoint(camera, corners[2]).x;
        float minX = Mathf.Min(firstX, oppositeX);
        float maxX = Mathf.Max(firstX, oppositeX);
        float scale = canvas.scaleFactor;
        float inset = margin * scale;
        float shift = 0f;
        if (minX < safeArea.xMin + inset)
            shift = safeArea.xMin + inset - minX;
        else if (maxX > safeArea.xMax - inset)
            shift = safeArea.xMax - inset - maxX;

        if (scale > 0f)
            rectTransform.anchoredPosition = originalPosition + Vector2.right * (shift / scale);
    }
}
