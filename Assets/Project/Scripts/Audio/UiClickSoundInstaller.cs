using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 点击音效自动挂载器：进入游戏和每次加载场景后，自动给场景内所有
/// Button 或 IPointerClickHandler 所在物体补挂 UiClickSound（按物体去重）。
/// 运行时 Instantiate 出来的 UI（如记录窗口、关卡预制体）在生成后调用
/// ScanGameObject 即可补挂，无需逐个按钮手工绑定。
/// </summary>
public static class UiClickSoundInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        ScanLoadedScenes();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScanScene(scene);
    }

    /// <summary>扫描当前所有已加载场景，给可点击物体补挂点击音效。</summary>
    public static void ScanLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            ScanScene(SceneManager.GetSceneAt(i));
    }

    /// <summary>扫描指定场景，给可点击物体补挂点击音效。</summary>
    public static void ScanScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
            ScanTransform(root.transform);
    }

    /// <summary>扫描某个物体及其全部子物体（含未激活物体），给可点击物体补挂点击音效。</summary>
    public static void ScanGameObject(GameObject root)
    {
        if (root != null)
            ScanTransform(root.transform);
    }

    private static void ScanTransform(Transform target)
    {
        bool isClickable = target.GetComponent<Button>() != null ||
                           target.GetComponent<IPointerClickHandler>() != null;

        if (isClickable && target.GetComponent<UiClickSound>() == null)
            target.gameObject.AddComponent<UiClickSound>();

        for (int i = 0; i < target.childCount; i++)
            ScanTransform(target.GetChild(i));
    }
}
