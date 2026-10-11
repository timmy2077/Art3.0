using System;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Project/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Tooltip("Print audio playback and skipped playback to the Unity Console.")]
    public bool logPlayback = true;

    [Serializable]
    public class Entry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public Entry[] music = Array.Empty<Entry>();
    public Entry[] soundEffects = Array.Empty<Entry>();

    public Entry FindMusic(string id) => Find(music, id);
    public Entry FindSoundEffect(string id) => Find(soundEffects, id);

    private static Entry Find(Entry[] entries, string id)
    {
        if (entries == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (Entry entry in entries)
        {
            if (entry != null && entry.id == id && entry.clip != null)
                return entry;
        }
        return null;
    }
}
