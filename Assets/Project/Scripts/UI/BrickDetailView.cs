using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>文物资料与大图分层展示，内容由 RecordWindow 的砖画资料提供。</summary>
public class BrickDetailView : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Image artwork;
    [SerializeField] private Text imageCaption;
    [SerializeField] private Text era;
    [SerializeField] private Text excavationSite;
    [SerializeField] private Text currentCollection;
    [SerializeField] private Text interpretation;
    [SerializeField] private Text sources;
    [SerializeField] private ScrollRect informationScroll;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button enlargeButton;
    [SerializeField] private BrickImageViewer imageViewer;

    private GameObject previousSelection;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        enlargeButton.onClick.AddListener(OpenImage);
    }

    public void Open(string displayName, Sprite sprite, string caption, string eraText,
        string siteText, string collectionText, string interpretationText, string sourceText)
    {
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        title.text = "《" + displayName + "》";
        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
        imageCaption.text = ValueOr(caption, "游戏复原图");
        era.text = ValueOr(eraText, "待考证");
        excavationSite.text = ValueOr(siteText, "待考证");
        currentCollection.text = ValueOr(collectionText, "待考证");
        interpretation.text = ValueOr(interpretationText, "暂无图像解读。");
        sources.text = ValueOr(sourceText, "资料来源待补充。");
        enlargeButton.interactable = sprite != null;
        imageViewer.Close();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        informationScroll.StopMovement();
        informationScroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    public void OpenImage()
    {
        if (artwork.sprite != null) imageViewer.Open(artwork.sprite, title.text, imageCaption.text);
    }

    public void Close()
    {
        if (imageViewer != null) imageViewer.Close();
        gameObject.SetActive(false);
        if (previousSelection != null && previousSelection.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void OnDisable()
    {
        if (imageViewer != null) imageViewer.Close();
    }

    private void Update()
    {
        if (DebugUnlockPanel.BlocksGameplayInput) return;
        // 只有详情页处理返回键，避免同一帧连关两层。
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (imageViewer.gameObject.activeSelf) imageViewer.Close();
        else Close();
    }

    private static string ValueOr(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
