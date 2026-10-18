using System;
using UnityEngine;

/// <summary>
/// 通用关卡文字加载器。
///
/// 约定：
///   每个关卡一个 JSON 文件，放在 Assets/Project/Resources/LevelText/{关卡ID}.json
///   每个关卡有自己的文字数据类（只需 [Serializable]，无需继承），通过泛型加载。
///   JSON 丢失或解析失败时使用传入的兜底默认值，保证游戏不崩。
///
/// 用法（以农耕关卡为例）：
///   texts = LevelTextLoader.Load("farming", BuildDefaultText);
/// </summary>
public static class LevelTextLoader
{
    private const string ResourceFolder = "LevelText/";

    /// <summary>
    /// 加载指定关卡的文字配置。
    /// </summary>
    /// <typeparam name="T">文字数据类类型（需标记 [Serializable]）</typeparam>
    /// <param name="levelId">关卡 ID，对应 Resources/LevelText/{levelId}.json</param>
    /// <param name="fallback">JSON 找不到或解析失败时返回的默认文案工厂</param>
    public static T Load<T>(string levelId, Func<T> fallback) where T : class
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourceFolder + levelId);
        if (asset != null && !string.IsNullOrEmpty(asset.text))
        {
            try
            {
                T loaded = JsonUtility.FromJson<T>(asset.text);
                if (loaded != null) return loaded;
                Debug.LogError("[LevelTextLoader] 解析 " + levelId + ".json 返回 null，使用默认文案。");
            }
            catch (Exception e)
            {
                Debug.LogError("[LevelTextLoader] 解析 " + levelId + ".json 异常，使用默认文案：\n" + e);
            }
        }
        else
        {
            Debug.LogWarning("[LevelTextLoader] 未找到 LevelText/" + levelId + ".json，使用默认文案。");
        }
        return fallback != null ? fallback() : null;
    }
}
