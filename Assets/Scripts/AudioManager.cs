using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;
    [Range(0.1f, 3f)]
    public float pitch = 1f;

    public bool loop = false;

    [HideInInspector]
    public AudioSource source;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Tracks")]
    [SerializeField] private List<Sound> musicTracks = new List<Sound>();

    [Header("Sound Effects")]
    [SerializeField] private List<Sound> soundEffects = new List<Sound>();

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float musicVolume = 1f;
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Header("Fade Settings")]
    public float defaultFadeDuration = 1f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    private string currentMusicTrack = "";
    private Coroutine musicFadeCoroutine;
    private Dictionary<string, Sound> musicDictionary = new Dictionary<string, Sound>();
    private Dictionary<string, Sound> sfxDictionary = new Dictionary<string, Sound>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudio();
            if (showDebugInfo) Debug.Log("AudioManager instance created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudio()
    {
        // Create audio sources if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        // Build dictionaries for quick lookup
        foreach (Sound music in musicTracks)
        {
            if (!musicDictionary.ContainsKey(music.name))
            {
                musicDictionary.Add(music.name, music);
            }
            else
            {
                Debug.LogWarning($"Duplicate music track name: {music.name}");
            }
        }

        foreach (Sound sfx in soundEffects)
        {
            if (!sfxDictionary.ContainsKey(sfx.name))
            {
                sfxDictionary.Add(sfx.name, sfx);
            }
            else
            {
                Debug.LogWarning($"Duplicate SFX name: {sfx.name}");
            }
        }
    }

    #region Music Methods

    public void PlayMusic(string name, bool fade = true)
    {
        if (!musicDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"Music track '{name}' not found!");
            return;
        }

        if (currentMusicTrack == name && musicSource.isPlaying)
        {
            if (showDebugInfo) Debug.Log($"Music '{name}' is already playing");
            return;
        }

        Sound music = musicDictionary[name];

        if (fade && musicSource.isPlaying)
        {
            if (musicFadeCoroutine != null)
                StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = StartCoroutine(CrossfadeMusic(music));
        }
        else
        {
            PlayMusicImmediate(music);
        }

        currentMusicTrack = name;
    }

    private void PlayMusicImmediate(Sound music)
    {
        musicSource.clip = music.clip;
        musicSource.volume = music.volume * musicVolume * masterVolume;
        musicSource.pitch = music.pitch;
        musicSource.loop = music.loop;
        musicSource.Play();

        if (showDebugInfo) Debug.Log($"Playing music: {music.name}");
    }

    public void StopMusic(bool fade = true)
    {
        if (fade)
        {
            if (musicFadeCoroutine != null)
                StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = StartCoroutine(FadeOutMusic(defaultFadeDuration));
        }
        else
        {
            musicSource.Stop();
            currentMusicTrack = "";
        }
    }

    public void PauseMusic()
    {
        musicSource.Pause();
        if (showDebugInfo) Debug.Log("Music paused");
    }

    public void ResumeMusic()
    {
        musicSource.UnPause();
        if (showDebugInfo) Debug.Log("Music resumed");
    }

    #endregion

    #region SFX Methods

    public void PlaySFX(string name)
    {
        if (!sfxDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"SFX '{name}' not found!");
            return;
        }

        Sound sfx = sfxDictionary[name];
        sfxSource.pitch = sfx.pitch;
        sfxSource.PlayOneShot(sfx.clip, sfx.volume * sfxVolume * masterVolume);

        if (showDebugInfo) Debug.Log($"Playing SFX: {name}");
    }

    public void PlaySFXAtPoint(string name, Vector3 position)
    {
        if (!sfxDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"SFX '{name}' not found!");
            return;
        }

        Sound sfx = sfxDictionary[name];
        AudioSource.PlayClipAtPoint(sfx.clip, position, sfx.volume * sfxVolume * masterVolume);

        if (showDebugInfo) Debug.Log($"Playing SFX at point: {name}");
    }

    public void PlayRandomSFX(params string[] names)
    {
        if (names.Length == 0) return;

        string randomName = names[Random.Range(0, names.Length)];
        PlaySFX(randomName);
    }

    #endregion

    #region Volume Control

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    private void UpdateVolumes()
    {
        if (musicSource.isPlaying && !string.IsNullOrEmpty(currentMusicTrack))
        {
            if (musicDictionary.ContainsKey(currentMusicTrack))
            {
                Sound currentMusic = musicDictionary[currentMusicTrack];
                musicSource.volume = currentMusic.volume * musicVolume * masterVolume;
            }
        }
    }

    #endregion

    #region Fade Coroutines

    private IEnumerator CrossfadeMusic(Sound newMusic)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        // Fade out current music
        while (elapsed < defaultFadeDuration / 2f)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (defaultFadeDuration / 2f));
            yield return null;
        }

        // Switch to new music
        PlayMusicImmediate(newMusic);
        musicSource.volume = 0f;

        elapsed = 0f;
        float targetVolume = newMusic.volume * musicVolume * masterVolume;

        // Fade in new music
        while (elapsed < defaultFadeDuration / 2f)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (defaultFadeDuration / 2f));
            yield return null;
        }

        musicSource.volume = targetVolume;
        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
        currentMusicTrack = "";
        musicFadeCoroutine = null;
    }

    #endregion

    #region Utility Methods

    public bool IsMusicPlaying()
    {
        return musicSource.isPlaying;
    }

    public string GetCurrentMusicTrack()
    {
        return currentMusicTrack;
    }

    #endregion
}