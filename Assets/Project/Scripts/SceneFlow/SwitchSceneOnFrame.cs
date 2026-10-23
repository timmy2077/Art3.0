using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchSceneOnFrame : MonoBehaviour
{
    public string targetSceneName;

    private void Start()
    {
        if (targetSceneName != "Index" || GetComponent<UnityEngine.UI.Button>() == null) return;
        // 独立排序，避免被关卡的世界名称标签盖住。
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 250;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    public void OnAnimationFrame()
    {
        // 选关场景中的返回按钮：有关卡副本时先退出关卡，不跳主页。
        var level = FindObjectOfType<LevelBase>();
        if (level != null && targetSceneName == "Index")
        {
            var pages = FindObjectOfType<UIPageSwitch>(true);
            level.CloseModal();
            if (pages == null) { SceneManager.LoadScene("Select"); return; }
            var group = pages.GetComponentInParent<CanvasGroup>(true);
            for (Transform parent = pages.transform; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
            if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
            level.gameObject.SetActive(false);
            Destroy(level.gameObject);
            bool audioRestored = false;
            foreach (var listener in FindObjectsOfType<AudioListener>())
            {
                listener.enabled = !audioRestored;
                audioRestored = true;
            }
            return;
        }
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty!");
            return;
        }

        if (gameObject.scene.name == "Start" && targetSceneName == "Index") IndexPrologue.Request();
        SceneManager.LoadScene(targetSceneName);
    }
}
