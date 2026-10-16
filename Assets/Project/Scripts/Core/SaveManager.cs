using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Single local save slot. Existing DataSystem APIs remain the gameplay facade.</summary>
public sealed class SaveManager : MonoBehaviour
{
    [Serializable] private sealed class Entry { public string key; public string value; }
    [Serializable] private sealed class SaveFile
    {
        public int version;
        public string format;
        public List<Entry> entries;
    }

    private static SaveManager instance;
    public static SaveManager Instance
    {
        get
        {
            if (instance == null) new GameObject("SaveManager").AddComponent<SaveManager>();
            return instance;
        }
    }
    public static event Action ResetOccurred;
    public string SavePath => Path.Combine(Application.persistentDataPath, "game-save.json");
    public string LastError { get; private set; }
    private readonly Dictionary<string, string> values = new Dictionary<string, string>();
    private bool dirty;
    private bool allowLegacy;
    private bool writeBlocked;
    private const string MigrationKey = "ArtGame.JsonSaveInitializedV1";

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    private void Load()
    {
        bool mainExists = File.Exists(SavePath);
        bool backupExists = File.Exists(SavePath + ".bak");
        if (mainExists && TryRead(SavePath)) return;
        // Never downgrade and overwrite a save created by a newer game version.
        if (writeBlocked) return;
        if (backupExists && TryRead(SavePath + ".bak"))
        {
            dirty = true;
            Debug.LogWarning("[SaveManager] 已从备份恢复存档。");
            return;
        }
        if (mainExists || backupExists)
        {
            writeBlocked = true;
            Debug.LogError("[SaveManager] 存档及备份无法读取，已禁止覆盖。请备份文件后排查，或主动清空存档。");
        }
        allowLegacy = !mainExists && !backupExists && PlayerPrefs.GetInt(MigrationKey, 0) == 0;
    }

    private bool TryRead(string path)
    {
        try
        {
            var data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            if (data == null || data.format != "ArtGameSave" || data.entries == null || data.version < 1)
                throw new InvalidDataException("存档格式无效");
            if (data.version > 1) { writeBlocked = true; throw new InvalidDataException("存档版本高于当前游戏"); }
            var loaded = new Dictionary<string, string>();
            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null || loaded.ContainsKey(entry.key))
                    throw new InvalidDataException("存档条目无效");
                loaded.Add(entry.key, entry.value);
            }
            values.Clear();
            foreach (var entry in loaded) values.Add(entry.Key, entry.Value);
            // Legacy keys are imported lazily, including quests not yet visited.
            allowLegacy = GetIntInternal("Save.LegacyImport", 0) == 1;
            return true;
        }
        catch (Exception ex) { LastError = ex.Message; Debug.LogWarning("[SaveManager] 读取失败：" + ex.Message); return false; }
    }

    private int GetIntInternal(string key, int fallback)
    {
        return values.TryGetValue(key, out var text) && int.TryParse(text, out int result) ? result : fallback;
    }
    public bool HasKey(string key) => values.ContainsKey(key) || (allowLegacy && PlayerPrefs.HasKey(key));
    public string GetString(string key, string fallback = "")
    {
        if (values.TryGetValue(key, out var result)) return result;
        if (!allowLegacy || !PlayerPrefs.HasKey(key)) return fallback;
        result = PlayerPrefs.GetString(key, fallback); SetString(key, result); return result;
    }
    public int GetInt(string key, int fallback = 0)
    {
        if (values.ContainsKey(key)) return GetIntInternal(key, fallback);
        if (!allowLegacy || !PlayerPrefs.HasKey(key)) return fallback;
        int result = PlayerPrefs.GetInt(key, fallback); SetInt(key, result); return result;
    }
    public float GetFloat(string key, float fallback = 0f)
    {
        if (values.TryGetValue(key, out var text))
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;
        if (!allowLegacy || !PlayerPrefs.HasKey(key)) return fallback;
        float legacy = PlayerPrefs.GetFloat(key, fallback); SetFloat(key, legacy); return legacy;
    }
    public void SetString(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("存档 key 不能为空", nameof(key));
        value = value ?? "";
        if (values.TryGetValue(key, out var old) && old == value) return;
        values[key] = value; dirty = true;
    }
    public void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));
    public void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    public void DeleteKey(string key)
    {
        values.Remove(key);
        // Tombstone prevents a deleted legacy value from reappearing.
        values[key] = ""; dirty = true;
    }

    public void Save() { SaveNow(); }
    public bool SaveNow()
    {
        if (writeBlocked) return false;
        if (!dirty) return true;
        try
        {
            var data = new SaveFile { version = 1, format = "ArtGameSave", entries = new List<Entry>() };
            values["Save.LegacyImport"] = allowLegacy ? "1" : "0";
            foreach (var entry in values) data.entries.Add(new Entry { key = entry.Key, value = entry.Value });
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            // Keep the previous VALID save. Do not replace a good backup with a corrupt primary.
            if (File.Exists(SavePath))
            {
                if (IsValidFile(SavePath)) File.Replace(temporary, SavePath, SavePath + ".bak");
                else File.Replace(temporary, SavePath, null);
            }
            else File.Move(temporary, SavePath);
            PlayerPrefs.SetInt(MigrationKey, 1); PlayerPrefs.Save();
            dirty = false; LastError = null; return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogError("[SaveManager] 保存失败，内存进度保留：" + ex.Message);
            return false;
        }
    }
    private static bool IsValidFile(string path)
    {
        try
        {
            var data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            return data != null && data.format == "ArtGameSave" && data.version == 1 && data.entries != null;
        }
        catch { return false; }
    }

    /// <summary>Explicit full reset. Settings and name are also reset by DataSystem.</summary>
    public void DeleteAll()
    {
        values.Clear(); allowLegacy = false; writeBlocked = false; dirty = true;
        // Persist the reset over both generations, so recovery cannot resurrect old progress.
        if (SaveNow())
        {
            try { File.Copy(SavePath, SavePath + ".bak", true); }
            catch (Exception ex) { LastError = ex.Message; Debug.LogError("[SaveManager] 重置备份失败：" + ex.Message); }
        }
        ResetOccurred?.Invoke();
    }
    private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    private void OnApplicationQuit() { SaveNow(); }
    private void OnDestroy() { if (instance == this) instance = null; }
}
