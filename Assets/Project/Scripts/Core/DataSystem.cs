using System.Collections.Generic;
using UnityEngine;

public class DataSystem : MonoBehaviour
{
    public static DataSystem instance;

    [Header("数据持久化设置")]
    public bool enablePersistence = true;

    private const string KEY_PLAYER_NAME = "PlayerName";
    private const string KEY_LEVEL_UNLOCK_PREFIX = "LevelUnlocked_";
    private const string KEY_BRICK_UNLOCK_PREFIX = "BrickUnlocked_";
    private const string KEY_BRICK_MIGRATION = "BrickUnlockMigrationV1";
    private const string KEY_SETTINGS_VOLUME = "SettingsVolume";
    private const string KEY_SETTINGS_MUSIC = "SettingsMusic";
    private const string KEY_SETTINGS_SFX = "SettingsSFX";
    private const string KEY_MUSIC_VOLUME = "SettingsMusicVolume";
    private const string KEY_SOUND_VOLUME = "SettingsSoundVolume";
    private const string DEFAULT_BRICK_ID = "gengzhong";

    private static readonly string[] LegacyBrickIds =
    {
        "gengzhong", "paochu", "yishi", "sishen", "muzhu"
    };

    private string cachedPlayerName = "Player";
    private bool hasSavedPlayerName;
    private bool[] cachedLevelUnlocked = new bool[10];
    private readonly HashSet<string> unlockedBricks = new HashSet<string>();
    private float cachedVolume = 1f;
    private float cachedMusicVolume = 0.72f;
    private float cachedSoundVolume = 0.82f;
    private bool cachedMusic = true;
    private bool cachedSFX = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance == null)
            new GameObject("DataSystem").AddComponent<DataSystem>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCache();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadCache()
    {
        if (enablePersistence)
        {
            cachedPlayerName = PlayerPrefs.GetString(KEY_PLAYER_NAME, "Player");
            hasSavedPlayerName = PlayerPrefs.HasKey(KEY_PLAYER_NAME);
            cachedVolume = PlayerPrefs.GetFloat(KEY_SETTINGS_VOLUME, 1f);
            cachedMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, 0.72f));
            cachedSoundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SOUND_VOLUME, 0.82f));
            // 旧版只有总音量：迁移到两个滑条，避免留下用户无法调高的隐藏总音量。
            if (!PlayerPrefs.HasKey(KEY_MUSIC_VOLUME) && !PlayerPrefs.HasKey(KEY_SOUND_VOLUME) &&
                PlayerPrefs.HasKey(KEY_SETTINGS_VOLUME))
            {
                cachedMusicVolume = cachedSoundVolume = Mathf.Clamp01(cachedVolume);
                cachedVolume = 1f;
                PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, cachedMusicVolume);
                PlayerPrefs.SetFloat(KEY_SOUND_VOLUME, cachedSoundVolume);
                PlayerPrefs.SetFloat(KEY_SETTINGS_VOLUME, 1f);
                PlayerPrefs.Save();
            }
            cachedMusic = PlayerPrefs.GetInt(KEY_SETTINGS_MUSIC, 1) == 1;
            cachedSFX = PlayerPrefs.GetInt(KEY_SETTINGS_SFX, 1) == 1;

            for (int i = 0; i < cachedLevelUnlocked.Length; i++)
            {
                cachedLevelUnlocked[i] = PlayerPrefs.GetInt(KEY_LEVEL_UNLOCK_PREFIX + (i + 1), i == 0 ? 1 : 0) == 1;
            }

            MigrateLegacyBrickRecords();
        }
        else
        {
            cachedLevelUnlocked[0] = true;
        }
    }

    private void MigrateLegacyBrickRecords()
    {
        if (PlayerPrefs.GetInt(KEY_BRICK_MIGRATION, 0) == 1) return;

        bool hasLegacyProgress = false;
        for (int i = 0; i < LegacyBrickIds.Length; i++)
            hasLegacyProgress |= PlayerPrefs.HasKey(KEY_LEVEL_UNLOCK_PREFIX + (i + 1));

        if (hasLegacyProgress)
        {
            for (int i = 0; i < LegacyBrickIds.Length; i++)
            {
                if (cachedLevelUnlocked[i])
                    PlayerPrefs.SetInt(KEY_BRICK_UNLOCK_PREFIX + LegacyBrickIds[i], 1);
            }
        }

        PlayerPrefs.SetInt(KEY_BRICK_MIGRATION, 1);
        PlayerPrefs.Save();
    }

    public string GetPlayerName()
    {
        return cachedPlayerName;
    }

    public bool HasSavedPlayerName()
    {
        return hasSavedPlayerName;
    }

    public void SavePlayerName(string name)
    {
        cachedPlayerName = name;
        hasSavedPlayerName = true;
        if (enablePersistence)
        {
            PlayerPrefs.SetString(KEY_PLAYER_NAME, name);
            PlayerPrefs.Save();
        }
    }

    public bool IsLevelUnlocked(int levelId)
    {
        if (levelId == 1)
        {
            return true;
        }
        int index = levelId - 1;
        if (index >= 0 && index < cachedLevelUnlocked.Length)
        {
            return cachedLevelUnlocked[index];
        }
        return enablePersistence ? PlayerPrefs.GetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 0) == 1 : false;
    }

    public void UnlockLevel(int levelId)
    {
        int index = levelId - 1;
        if (index >= 0 && index < cachedLevelUnlocked.Length)
        {
            cachedLevelUnlocked[index] = true;
        }
        if (enablePersistence)
        {
            PlayerPrefs.SetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 1);
            PlayerPrefs.Save();
        }
    }

    public bool IsBrickUnlocked(string brickId)
    {
        if (string.IsNullOrEmpty(brickId)) return false;
        return IsDefaultBrick(brickId) || unlockedBricks.Contains(brickId) ||
               (enablePersistence && PlayerPrefs.GetInt(KEY_BRICK_UNLOCK_PREFIX + brickId, 0) == 1);
    }

    public static bool IsDefaultBrick(string brickId)
    {
        return brickId == DEFAULT_BRICK_ID;
    }

    public void UnlockBrick(string brickId)
    {
        if (string.IsNullOrEmpty(brickId))
        {
            Debug.LogWarning("[DataSystem] 石砖 ID 不能为空。", this);
            return;
        }

        if (IsBrickUnlocked(brickId)) return;
        unlockedBricks.Add(brickId);
        if (enablePersistence)
        {
            PlayerPrefs.SetInt(KEY_BRICK_UNLOCK_PREFIX + brickId, 1);
            PlayerPrefs.Save();
        }
    }

    public float GetSettingsVolume()
    {
        return cachedVolume;
    }

    public void SaveSettingsVolume(float volume)
    {
        cachedVolume = Mathf.Clamp01(volume);
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (enablePersistence)
        {
            PlayerPrefs.SetFloat(KEY_SETTINGS_VOLUME, cachedVolume);
            PlayerPrefs.Save();
        }
    }

    public float GetSettingsMusicVolume() { return cachedMusicVolume; }
    public float GetSettingsSoundVolume() { return cachedSoundVolume; }

    public void SaveSettingsMusicVolume(float volume)
    {
        cachedMusicVolume = Mathf.Clamp01(volume);
        cachedMusic = cachedMusicVolume > 0f;
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (!enablePersistence) return;
        PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, cachedMusicVolume);
        PlayerPrefs.SetInt(KEY_SETTINGS_MUSIC, cachedMusic ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SaveSettingsSoundVolume(float volume)
    {
        cachedSoundVolume = Mathf.Clamp01(volume);
        cachedSFX = cachedSoundVolume > 0f;
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (!enablePersistence) return;
        PlayerPrefs.SetFloat(KEY_SOUND_VOLUME, cachedSoundVolume);
        PlayerPrefs.SetInt(KEY_SETTINGS_SFX, cachedSFX ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool GetSettingsMusic()
    {
        return cachedMusic;
    }

    public void SaveSettingsMusic(bool enabled)
    {
        cachedMusic = enabled;
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (enablePersistence)
        {
            PlayerPrefs.SetInt(KEY_SETTINGS_MUSIC, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public bool GetSettingsSFX()
    {
        return cachedSFX;
    }

    public void SaveSettingsSFX(bool enabled)
    {
        cachedSFX = enabled;
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (enablePersistence)
        {
            PlayerPrefs.SetInt(KEY_SETTINGS_SFX, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public void ClearAllData()
    {
        unlockedBricks.Clear();
        cachedPlayerName = "Player";
        hasSavedPlayerName = false;
        cachedVolume = 1f;
        cachedMusicVolume = 0.72f;
        cachedSoundVolume = 0.82f;
        cachedMusic = true;
        cachedSFX = true;
        for (int i = 0; i < cachedLevelUnlocked.Length; i++)
        {
            cachedLevelUnlocked[i] = (i == 0);
        }
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
    }
}


// using UnityEngine;

// public class DataSystem : MonoBehaviour
// {
//     public static DataSystem instance;

//     private const string KEY_PLAYER_NAME = "PlayerName";
//     private const string KEY_LEVEL_UNLOCK_PREFIX = "LevelUnlocked_";
//     private const string KEY_SETTINGS_VOLUME = "SettingsVolume";
//     private const string KEY_SETTINGS_MUSIC = "SettingsMusic";
//     private const string KEY_SETTINGS_SFX = "SettingsSFX";

//     private void Awake()
//     {
//         if (instance == null)
//         {
//             instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }

//     public string GetPlayerName()
//     {
//         return PlayerPrefs.GetString(KEY_PLAYER_NAME, "Player");
//     }

//     public void SavePlayerName(string name)
//     {
//         PlayerPrefs.SetString(KEY_PLAYER_NAME, name);
//         PlayerPrefs.Save();
//     }

//     public bool IsLevelUnlocked(int levelId)
//     {
//         if (levelId == 1)
//         {
//             return true;
//         }
//         return PlayerPrefs.GetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 0) == 1;
//     }

//     public void UnlockLevel(int levelId)
//     {
//         PlayerPrefs.SetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 1);
//         PlayerPrefs.Save();
//     }

//     public float GetSettingsVolume()
//     {
//         return PlayerPrefs.GetFloat(KEY_SETTINGS_VOLUME, 1f);
//     }

//     public void SaveSettingsVolume(float volume)
//     {
//         PlayerPrefs.SetFloat(KEY_SETTINGS_VOLUME, Mathf.Clamp01(volume));
//         PlayerPrefs.Save();
//     }

//     public bool GetSettingsMusic()
//     {
//         return PlayerPrefs.GetInt(KEY_SETTINGS_MUSIC, 1) == 1;
//     }

//     public void SaveSettingsMusic(bool enabled)
//     {
//         PlayerPrefs.SetInt(KEY_SETTINGS_MUSIC, enabled ? 1 : 0);
//         PlayerPrefs.Save();
//     }

//     public bool GetSettingsSFX()
//     {
//         return PlayerPrefs.GetInt(KEY_SETTINGS_SFX, 1) == 1;
//     }

//     public void SaveSettingsSFX(bool enabled)
//     {
//         PlayerPrefs.SetInt(KEY_SETTINGS_SFX, enabled ? 1 : 0);
//         PlayerPrefs.Save();
//     }

//     public void ClearAllData()
//     {
//         PlayerPrefs.DeleteAll();
//         PlayerPrefs.Save();
//     }
// }
