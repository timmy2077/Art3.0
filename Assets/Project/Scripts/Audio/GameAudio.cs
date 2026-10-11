using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudio : MonoBehaviour
{
    private const int SoundEffectVoices = 8;
    private const float MusicFadeSeconds = 0.35f;

    public static GameAudio Instance { get; private set; }

    private AudioLibrary library;
    private AudioSource musicSource;
    private AudioSource[] soundSources;
    private float[] soundClipVolumes;
    private Coroutine musicFade;
    private string currentMusicId;
    private float currentMusicVolume = 1f;
    private float masterVolume = 1f;
    private float musicVolume = 1f;
    private float soundVolume = 1f;
    private bool musicEnabled = true;
    private bool soundEffectsEnabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
            new GameObject("GameAudio").AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        library = Resources.Load<AudioLibrary>("AudioLibrary");

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        soundSources = new AudioSource[SoundEffectVoices];
        soundClipVolumes = new float[SoundEffectVoices];
        for (int i = 0; i < soundSources.Length; i++)
        {
            soundSources[i] = gameObject.AddComponent<AudioSource>();
            soundSources[i].playOnAwake = false;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshSettings();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSettings();
        // Each scene opts into music through AudioSceneMusic. No profile means silence.
        if (mode == LoadSceneMode.Single)
            StopMusic();
    }

    public void RefreshSettings()
    {
        DataSystem data = DataSystem.instance;
        masterVolume = data != null ? data.GetSettingsVolume() : 1f;
        musicVolume = data != null ? data.GetSettingsMusicVolume() : 1f;
        soundVolume = data != null ? data.GetSettingsSoundVolume() : 1f;
        musicEnabled = data == null || data.GetSettingsMusic();
        soundEffectsEnabled = data == null || data.GetSettingsSFX();
        musicSource.mute = !musicEnabled;
        musicSource.volume = masterVolume * musicVolume * currentMusicVolume;
        for (int i = 0; i < soundSources.Length; i++)
        {
            soundSources[i].mute = !soundEffectsEnabled;
            soundSources[i].volume = masterVolume * soundVolume * soundClipVolumes[i];
        }
    }

    public void PlayMusic(string id)
    {
        AudioLibrary.Entry entry = library != null ? library.FindMusic(id) : null;
        if (entry == null)
        {
            LogSkipped("Music", id, "not found in AudioLibrary or has no clip");
            return;
        }
        if (currentMusicId == id && musicSource.isPlaying) return;

        if (musicFade != null) StopCoroutine(musicFade);
        currentMusicId = id;
        currentMusicVolume = entry.volume;
        musicFade = StartCoroutine(ChangeMusic(id, entry.clip));
    }

    public void StopMusic()
    {
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = null;
        currentMusicId = null;
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlaySound(string id)
    {
        AudioLibrary.Entry entry = library != null ? library.FindSoundEffect(id) : null;
        if (entry == null)
        {
            LogSkipped("SFX", id, "not found in AudioLibrary or has no clip");
            return;
        }
        if (!soundEffectsEnabled)
        {
            LogSkipped("SFX", id, "sound effects disabled in settings");
            return;
        }

        for (int i = 0; i < soundSources.Length; i++)
        {
            AudioSource source = soundSources[i];
            if (source.isPlaying) continue;
            soundClipVolumes[i] = entry.volume;
            source.volume = masterVolume * soundVolume * entry.volume;
            source.clip = entry.clip;
            source.Play();
            LogPlayed("SFX", id, entry.clip, source);
            return;
        }
        LogSkipped("SFX", id, "all 8 sound-effect voices are busy");
    }

    private IEnumerator ChangeMusic(string id, AudioClip nextClip)
    {
        float startVolume = musicSource.volume;
        for (float elapsed = 0f; elapsed < MusicFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / MusicFadeSeconds);
            yield return null;
        }

        musicSource.clip = nextClip;
        musicSource.volume = 0f;
        musicSource.Play();
        LogPlayed("Music", id, nextClip, musicSource);
        for (float elapsed = 0f; elapsed < MusicFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, masterVolume * musicVolume * currentMusicVolume, elapsed / MusicFadeSeconds);
            yield return null;
        }
        musicSource.volume = masterVolume * musicVolume * currentMusicVolume;
        musicFade = null;
    }

    private void LogPlayed(string type, string id, AudioClip clip, AudioSource source)
    {
        if (library == null || !library.logPlayback) return;
        Debug.Log($"[GameAudio] Playing {type}: ID='{id}', Clip='{clip.name}', Unity mute={source.mute}, master volume={masterVolume:0.##}. System mute is not detectable.", this);
    }

    private void LogSkipped(string type, string id, string reason)
    {
        if (library == null || !library.logPlayback || string.IsNullOrWhiteSpace(id)) return;
        Debug.LogWarning($"[GameAudio] Skipped {type}: ID='{id}' ({reason}).", this);
    }
}
