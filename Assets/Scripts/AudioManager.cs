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

    [Header("Random Playlist Settings")]
    [SerializeField] private bool enableRandomPlaylist = false;
    [SerializeField] private bool shufflePlaylist = true;
    [Tooltip("Prevent the same track from playing twice in a row")]
    [SerializeField] private bool preventRepeat = true;

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
    private Coroutine playlistCoroutine;
    private Dictionary<string, Sound> musicDictionary = new Dictionary<string, Sound>();
    private Dictionary<string, Sound> sfxDictionary = new Dictionary<string, Sound>();

    // Playlist management
    private List<Sound> playlistQueue = new List<Sound>();
    private int currentPlaylistIndex = 0;
    private Sound lastPlayedTrack = null;

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
            musicSource.loop = false; // Changed to false for playlist support
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

        // Stop playlist mode if active
        StopRandomPlaylist();

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
        StopRandomPlaylist();

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

    #region Random Playlist Methods

    public void StartRandomPlaylist()
    {
        if (musicTracks.Count == 0)
        {
            Debug.LogWarning("No music tracks available for playlist!");
            return;
        }

        enableRandomPlaylist = true;
        BuildPlaylistQueue();

        if (playlistCoroutine != null)
            StopCoroutine(playlistCoroutine);

        playlistCoroutine = StartCoroutine(PlaylistCoroutine());

        if (showDebugInfo) Debug.Log("Random playlist started");
    }

    public void StopRandomPlaylist()
    {
        enableRandomPlaylist = false;

        if (playlistCoroutine != null)
        {
            StopCoroutine(playlistCoroutine);
            playlistCoroutine = null;
        }
    }

    public void PlayNextTrackInPlaylist()
    {
        if (!enableRandomPlaylist || playlistQueue.Count == 0)
            return;

        currentPlaylistIndex++;

        // Rebuild queue if we've reached the end
        if (currentPlaylistIndex >= playlistQueue.Count)
        {
            BuildPlaylistQueue();
            currentPlaylistIndex = 0;
        }

        Sound nextTrack = playlistQueue[currentPlaylistIndex];
        PlayMusicImmediate(nextTrack);
        lastPlayedTrack = nextTrack;
        currentMusicTrack = nextTrack.name;
    }

    private void BuildPlaylistQueue()
    {
        playlistQueue.Clear();
        playlistQueue.AddRange(musicTracks);

        if (shufflePlaylist)
        {
            ShufflePlaylist();
        }

        // Prevent same track from playing twice in a row
        if (preventRepeat && lastPlayedTrack != null && playlistQueue.Count > 1)
        {
            if (playlistQueue[0].name == lastPlayedTrack.name)
            {
                // Swap first track with a random other track
                int randomIndex = Random.Range(1, playlistQueue.Count);
                Sound temp = playlistQueue[0];
                playlistQueue[0] = playlistQueue[randomIndex];
                playlistQueue[randomIndex] = temp;
            }
        }

        currentPlaylistIndex = -1; // Will be incremented to 0 on first play

        if (showDebugInfo)
        {
            Debug.Log($"Playlist queue built with {playlistQueue.Count} tracks");
        }
    }

    private void ShufflePlaylist()
    {
        // Fisher-Yates shuffle
        for (int i = playlistQueue.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Sound temp = playlistQueue[i];
            playlistQueue[i] = playlistQueue[randomIndex];
            playlistQueue[randomIndex] = temp;
        }
    }

    private IEnumerator PlaylistCoroutine()
    {
        while (enableRandomPlaylist)
        {
            // Play next track
            PlayNextTrackInPlaylist();

            // Wait for track to finish
            if (musicSource.clip != null)
            {
                yield return new WaitForSeconds(musicSource.clip.length);
            }
            else
            {
                yield return new WaitForSeconds(1f); // Fallback
            }
        }
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

    public bool IsPlaylistActive()
    {
        return enableRandomPlaylist;
    }

    #endregion
}