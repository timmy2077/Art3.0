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
            cachedPlayerName = SaveManager.Instance.GetString(KEY_PLAYER_NAME, "Player");
            hasSavedPlayerName = SaveManager.Instance.HasKey(KEY_PLAYER_NAME);
            cachedVolume = SaveManager.Instance.GetFloat(KEY_SETTINGS_VOLUME, 1f);
            cachedMusicVolume = Mathf.Clamp01(SaveManager.Instance.GetFloat(KEY_MUSIC_VOLUME, 0.72f));
            cachedSoundVolume = Mathf.Clamp01(SaveManager.Instance.GetFloat(KEY_SOUND_VOLUME, 0.82f));
            // 旧版只有总音量：迁移到两个滑条，避免留下用户无法调高的隐藏总音量。
            if (!SaveManager.Instance.HasKey(KEY_MUSIC_VOLUME) && !SaveManager.Instance.HasKey(KEY_SOUND_VOLUME) &&
                SaveManager.Instance.HasKey(KEY_SETTINGS_VOLUME))
            {
                cachedMusicVolume = cachedSoundVolume = Mathf.Clamp01(cachedVolume);
                cachedVolume = 1f;
                SaveManager.Instance.SetFloat(KEY_MUSIC_VOLUME, cachedMusicVolume);
                SaveManager.Instance.SetFloat(KEY_SOUND_VOLUME, cachedSoundVolume);
                SaveManager.Instance.SetFloat(KEY_SETTINGS_VOLUME, 1f);
                SaveManager.Instance.Save();
            }
            cachedMusic = SaveManager.Instance.GetInt(KEY_SETTINGS_MUSIC, 1) == 1;
            cachedSFX = SaveManager.Instance.GetInt(KEY_SETTINGS_SFX, 1) == 1;

            for (int i = 0; i < cachedLevelUnlocked.Length; i++)
            {
                cachedLevelUnlocked[i] = SaveManager.Instance.GetInt(KEY_LEVEL_UNLOCK_PREFIX + (i + 1), i == 0 ? 1 : 0) == 1;
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
        if (SaveManager.Instance.GetInt(KEY_BRICK_MIGRATION, 0) == 1) return;

        bool hasLegacyProgress = false;
        for (int i = 0; i < LegacyBrickIds.Length; i++)
            hasLegacyProgress |= SaveManager.Instance.HasKey(KEY_LEVEL_UNLOCK_PREFIX + (i + 1));

        if (hasLegacyProgress)
        {
            for (int i = 0; i < LegacyBrickIds.Length; i++)
            {
                if (cachedLevelUnlocked[i])
                    SaveManager.Instance.SetInt(KEY_BRICK_UNLOCK_PREFIX + LegacyBrickIds[i], 1);
            }
        }

        SaveManager.Instance.SetInt(KEY_BRICK_MIGRATION, 1);
        SaveManager.Instance.Save();
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
            SaveManager.Instance.SetString(KEY_PLAYER_NAME, name);
            SaveManager.Instance.Save();
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
        return enablePersistence ? SaveManager.Instance.GetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 0) == 1 : false;
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
            SaveManager.Instance.SetInt(KEY_LEVEL_UNLOCK_PREFIX + levelId, 1);
            SaveManager.Instance.Save();
        }
    }

    public bool IsBrickUnlocked(string brickId)
    {
        if (string.IsNullOrEmpty(brickId)) return false;
        return IsDefaultBrick(brickId) || unlockedBricks.Contains(brickId) ||
               (enablePersistence && SaveManager.Instance.GetInt(KEY_BRICK_UNLOCK_PREFIX + brickId, 0) == 1);
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
            SaveManager.Instance.SetInt(KEY_BRICK_UNLOCK_PREFIX + brickId, 1);
            SaveManager.Instance.Save();
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
            SaveManager.Instance.SetFloat(KEY_SETTINGS_VOLUME, cachedVolume);
            SaveManager.Instance.Save();
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
        SaveManager.Instance.SetFloat(KEY_MUSIC_VOLUME, cachedMusicVolume);
        SaveManager.Instance.SetInt(KEY_SETTINGS_MUSIC, cachedMusic ? 1 : 0);
        SaveManager.Instance.Save();
    }

    public void SaveSettingsSoundVolume(float volume)
    {
        cachedSoundVolume = Mathf.Clamp01(volume);
        cachedSFX = cachedSoundVolume > 0f;
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
        if (!enablePersistence) return;
        SaveManager.Instance.SetFloat(KEY_SOUND_VOLUME, cachedSoundVolume);
        SaveManager.Instance.SetInt(KEY_SETTINGS_SFX, cachedSFX ? 1 : 0);
        SaveManager.Instance.Save();
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
            SaveManager.Instance.SetInt(KEY_SETTINGS_MUSIC, enabled ? 1 : 0);
            SaveManager.Instance.Save();
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
            SaveManager.Instance.SetInt(KEY_SETTINGS_SFX, enabled ? 1 : 0);
            SaveManager.Instance.Save();
        }
    }

    /// <summary>Start a fresh journey while retaining profile and audio preferences.</summary>
    public bool StartNewGame()
    {
        string name = cachedPlayerName;
        bool named = hasSavedPlayerName;
        float volume = cachedVolume, musicVolume = cachedMusicVolume, soundVolume = cachedSoundVolume;
        bool music = cachedMusic, sound = cachedSFX;
        if (!enablePersistence) return false;
        ClearAllData();
        if (named) SavePlayerName(name);
        SaveSettingsVolume(volume);
        SaveSettingsMusicVolume(musicVolume);
        SaveSettingsSoundVolume(soundVolume);
        SaveSettingsMusic(music);
        SaveSettingsSFX(sound);
        SaveManager.Instance.SetString("Game.ResumeScene", "Start");
        SaveManager.Instance.SetInt("Game.Started", 1);
        return SaveManager.Instance.SaveNow();
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
        SaveManager.Instance.DeleteAll();
        SaveManager.Instance.Save();
        if (GameAudio.Instance != null) GameAudio.Instance.RefreshSettings();
    }
}

