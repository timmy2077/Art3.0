using UnityEngine;
using UnityEngine.EventSystems;

///=============================================================================
/// 关卡运行时守卫（挂在关卡预制体根物体上，例如 Level1 根节点）
///
/// 解决两个问题：
/// 1. Select 场景中有多个动画入口，每个入口的动画事件都会 Instantiate 同一张
///    关卡预制体；重复实例化会导致场景中出现多个 EventSystem / AudioListener，
///    Console 每帧刷屏警告，UI 输入也会变得不稳定（按钮点不动/对话卡住）。
/// 2. 关卡预制体自带 EventSystem 和 Main Camera(AudioListener)，而宿主场景
///    （Select / Level1 场景本身）也各有一套，直接进入关卡时必然重复。
///
/// 策略：
/// - 同一张关卡被实例化第二次时，直接销毁整个重复副本；
/// - 运行时保证全局只有一个 EventSystem（优先保留宿主场景原有的）；
/// - 运行时保证只有一个启用的 AudioListener（保留关卡相机上的）。
///=============================================================================
[DefaultExecutionOrder(-1000)]
public class LevelRuntimeBootstrap : MonoBehaviour
{
    private static LevelRuntimeBootstrap activeInstance;

    void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            // 同一关卡已存在（多个动画入口同时触发），销毁重复副本。
            Destroy(gameObject);
            return;
        }
        activeInstance = this;

        // 在 OnEnable 产生重复警告/输入冲突之前尽快清理。
        EnsureSingleEventSystem();
        EnsureSingleAudioListener();
    }

    private void EnsureSingleEventSystem()
    {
        EventSystem[] systems = FindObjectsOfType<EventSystem>();
        if (systems.Length <= 1) return;

        EventSystem own = GetComponentInChildren<EventSystem>(true);
        if (own != null)
        {
            // 宿主场景自带 EventSystem（如 Select 场景），移除预制体自带的那套。
            Destroy(own.gameObject);
            return;
        }

        // 本预制体没有 EventSystem 时，只保留场景中的第一个。
        for (int i = 1; i < systems.Length; i++)
        {
            if (systems[i] != null) Destroy(systems[i].gameObject);
        }
    }

    private void EnsureSingleAudioListener()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        AudioListener own = GetComponentInChildren<AudioListener>(true);

        bool keptOne = false;
        foreach (AudioListener listener in listeners)
        {
            if (listener == null || !listener.enabled) continue;
            bool keep = own != null ? listener == own : !keptOne;
            if (keep) { keptOne = true; continue; }
            // 关卡相机上的监听器保留，禁用宿主/场景相机上的重复监听器。
            listener.enabled = false;
        }
    }
}
