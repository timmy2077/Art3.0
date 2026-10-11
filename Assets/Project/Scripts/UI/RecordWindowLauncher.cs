using UnityEngine;

public class RecordWindowLauncher : MonoBehaviour
{
    [SerializeField] private GameObject recordWindowPrefab;
    [SerializeField] private Transform uiParent;

    private GameObject openWindow;

    public void OpenRecordWindow()
    {
        if (openWindow != null) return;
        if (recordWindowPrefab == null)
        {
            Debug.LogWarning("[RecordWindowLauncher] 请指定记录界面预制体。", this);
            return;
        }

        Transform parent = uiParent != null ? uiParent : transform;
        openWindow = Instantiate(recordWindowPrefab, parent);
        openWindow.SetActive(true);
        openWindow.transform.SetAsLastSibling();

        UiClickSoundInstaller.ScanGameObject(openWindow);
    }
}
