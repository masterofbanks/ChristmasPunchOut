using UnityEngine;

public class MainMenuAudio : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    void Start()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogError("[MainMenuAudio] AudioManager.Instance is NULL! Make sure AudioManager exists in the scene.");
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log("[MainMenuAudio] AudioManager found, starting playlist...");
        }

        AudioManager.Instance.StartRandomPlaylist();

        // Verify after starting
        if (showDebugInfo)
        {
            Debug.Log($"[MainMenuAudio] Playlist active: {AudioManager.Instance.IsPlaylistActive()}");
            Debug.Log($"[MainMenuAudio] Music playing: {AudioManager.Instance.IsMusicPlaying()}");
            Debug.Log($"[MainMenuAudio] Current track: {AudioManager.Instance.GetCurrentMusicTrack()}");
        }
    }
}