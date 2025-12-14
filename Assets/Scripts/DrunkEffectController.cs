using UnityEngine;

public class DrunkEffectController : MonoBehaviour
{
    public static DrunkEffectController Instance { get; private set; }

    [Header("Effect Settings")]
    [Range(0f, 1f)]
    public float targetIntensity = 0.8f;
    public float fadeInSpeed = 2f;
    public float fadeOutSpeed = 1f;
    public float effectDuration = 5f;
    public float speed = 1.5f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    public bool IsActive { get; private set; }
    public float CurrentIntensity { get; private set; }
    public float Speed => speed;

    private float effectTimer;
    private bool isFadingOut;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (showDebugInfo) Debug.Log("DrunkEffectController instance created");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!IsActive)
            return;

        // Handle effect duration
        effectTimer -= Time.deltaTime;

        if (effectTimer <= 0 && !isFadingOut)
        {
            isFadingOut = true;
            if (showDebugInfo) Debug.Log("Starting fade out");
        }

        // Fade in or out
        if (isFadingOut)
        {
            CurrentIntensity = Mathf.MoveTowards(CurrentIntensity, 0f, fadeOutSpeed * Time.deltaTime);

            if (CurrentIntensity <= 0f)
            {
                IsActive = false;
                isFadingOut = false;
                if (showDebugInfo) Debug.Log("Effect ended");
            }
        }
        else
        {
            CurrentIntensity = Mathf.MoveTowards(CurrentIntensity, targetIntensity, fadeInSpeed * Time.deltaTime);
        }
    }

    public void TriggerEffect()
    {
        IsActive = true;
        effectTimer = effectDuration;
        isFadingOut = false;
        CurrentIntensity = 0f;
        if (showDebugInfo) Debug.Log($"Drunk effect triggered! IsActive: {IsActive}, Duration: {effectDuration}");
    }

    public void TriggerEffect(float duration)
    {
        TriggerEffect();
        effectDuration = duration;
    }

    public void StopEffect()
    {
        isFadingOut = true;
    }

    private void OnGUI()
    {
        if (showDebugInfo)
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"Drunk Effect Active: {IsActive}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Intensity: {CurrentIntensity:F2}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Timer: {effectTimer:F2}");
        }
    }
}