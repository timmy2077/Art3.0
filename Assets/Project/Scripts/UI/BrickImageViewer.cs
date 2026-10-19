using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>使用原 Sprite 显示大图，不复制纹理。支持按钮、滚轮、双指缩放及 ScrollRect 拖动。</summary>
public class BrickImageViewer : MonoBehaviour, IScrollHandler
{
    [SerializeField] private Image artwork;
    [SerializeField] private ScrollRect imageScroll;
    [SerializeField] private Text title;
    [SerializeField] private Text caption;
    [SerializeField] private Text zoomLabel;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    private float zoom = 1f;
    private Vector2 viewportSize;
    private GameObject previousSelection;
    private const float MaximumZoom = 4f;

    private void Awake()
    {
        zoomInButton.onClick.AddListener(ZoomIn);
        zoomOutButton.onClick.AddListener(ZoomOut);
        resetButton.onClick.AddListener(ResetZoom);
        closeButton.onClick.AddListener(Close);
    }

    public void Open(Sprite sprite, string displayName, string imageCaption)
    {
        if (sprite == null) return;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        artwork.sprite = sprite;
        title.text = displayName;
        caption.text = imageCaption;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        ResetZoom();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    public void Close()
    {
        imageScroll.StopMovement();
        gameObject.SetActive(false);
        if (previousSelection != null && previousSelection.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    public void ZoomIn() { SetZoom(zoom + .5f); }
    public void ZoomOut() { SetZoom(zoom - .5f); }

    public void ResetZoom()
    {
        SetZoom(1f);
        artwork.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void SetZoom(float value)
    {
        if (artwork.sprite == null) return;
        Vector2 previousSize = artwork.rectTransform.rect.size;
        Vector2 position = artwork.rectTransform.anchoredPosition;
        imageScroll.StopMovement();
        zoom = Mathf.Clamp(value, 1f, MaximumZoom);
        viewportSize = imageScroll.viewport.rect.size;
        Vector2 source = artwork.sprite.rect.size;
        float fit = Mathf.Min(viewportSize.x / source.x, viewportSize.y / source.y);
        artwork.rectTransform.sizeDelta = source * fit * zoom;
        Canvas.ForceUpdateCanvases();
        Vector2 size = artwork.rectTransform.rect.size;
        // 保持当前画面中心，不使用适合画面时不确定的 ScrollRect 归一化坐标。
        if (previousSize.x > 0) position.x *= size.x / previousSize.x;
        if (previousSize.y > 0) position.y *= size.y / previousSize.y;
        Vector2 limit = Vector2.Max(Vector2.zero, (size - viewportSize) * .5f);
        position.x = Mathf.Clamp(position.x, -limit.x, limit.x);
        position.y = Mathf.Clamp(position.y, -limit.y, limit.y);
        artwork.rectTransform.anchoredPosition = position;
        zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        zoomInButton.interactable = zoom < MaximumZoom;
        zoomOutButton.interactable = zoom > 1f;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(imageScroll.viewport,
            eventData.position, eventData.enterEventCamera)) SetZoom(zoom + eventData.scrollDelta.y * .15f);
    }

    private void Update()
    {
        if (Input.touchCount == 2)
        {
            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(imageScroll.viewport, first.position, camera) &&
                RectTransformUtility.RectangleContainsScreenPoint(imageScroll.viewport, second.position, camera))
            {
                float before = Vector2.Distance(first.position - first.deltaPosition, second.position - second.deltaPosition);
                if (before > 1f) SetZoom(zoom * Vector2.Distance(first.position, second.position) / before);
            }
        }
        if (viewportSize != imageScroll.viewport.rect.size) SetZoom(zoom);
    }
}
