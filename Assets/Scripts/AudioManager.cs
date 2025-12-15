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
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Tracks")]
    public List<Sound> musicTracks = new List<Sound>();

    [Header("Volume")]
    [Range(0f, 1f)]
    public float musicVolume = 1f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private AudioSource musicSource;
    private List<Sound> shuffledPlaylist = new List<Sound>();
    private int currentTrackIndex = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupAudioSource();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupAudioSource()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        if (showDebugInfo)
        {
            Debug.Log($"[AudioManager] Setup complete. Tracks available: {musicTracks.Count}");
            foreach (Sound track in musicTracks)
            {
                Debug.Log($"[AudioManager] Track: {track.name}, Clip: {(track.clip != null ? track.clip.name : "NULL")}");
            }
        }
    }

    public void StartRandomPlaylist()
    {
        if (musicTracks.Count == 0)
        {
            Debug.LogError("[AudioManager] No music tracks assigned!");
            return;
        }

        // Remove null clips
        shuffledPlaylist.Clear();
        foreach (Sound track in musicTracks)
        {
            if (track.clip != null)
            {
                shuffledPlaylist.Add(track);
            }
        }

        if (shuffledPlaylist.Count == 0)
        {
            Debug.LogError("[AudioManager] All clips are null!");
            return;
        }

        // Shuffle
        for (int i = shuffledPlaylist.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Sound temp = shuffledPlaylist[i];
            shuffledPlaylist[i] = shuffledPlaylist[randomIndex];
            shuffledPlaylist[randomIndex] = temp;
        }

        currentTrackIndex = 0;
        PlayCurrentTrack();
    }

    private void PlayCurrentTrack()
    {
        if (shuffledPlaylist.Count == 0) return;

        Sound track = shuffledPlaylist[currentTrackIndex];

        musicSource.clip = track.clip;
        musicSource.volume = track.volume * musicVolume;
        musicSource.Play();

        if (showDebugInfo)
        {
            Debug.Log($"[AudioManager] ▶ NOW PLAYING: {track.name}");
            Debug.Log($"[AudioManager]   Clip: {track.clip.name}");
            Debug.Log($"[AudioManager]   Length: {track.clip.length}s");
            Debug.Log($"[AudioManager]   Volume: {musicSource.volume}");
            Debug.Log($"[AudioManager]   IsPlaying: {musicSource.isPlaying}");
        }

        StartCoroutine(WaitForTrackEnd());
    }

    private IEnumerator WaitForTrackEnd()
    {
        yield return new WaitForSeconds(musicSource.clip.length);

        if (showDebugInfo)
        {
            Debug.Log($"[AudioManager] Track finished, playing next...");
        }

        currentTrackIndex++;
        if (currentTrackIndex >= shuffledPlaylist.Count)
        {
            // Re-shuffle and start over
            StartRandomPlaylist();
        }
        else
        {
            PlayCurrentTrack();
        }
    }

    public bool IsMusicPlaying()
    {
        return musicSource != null && musicSource.isPlaying;
    }

    public bool IsPlaylistActive()
    {
        return shuffledPlaylist.Count > 0;
    }

    public string GetCurrentMusicTrack()
    {
        if (shuffledPlaylist.Count == 0 || currentTrackIndex >= shuffledPlaylist.Count)
            return "";
        return shuffledPlaylist[currentTrackIndex].name;
    }
}