using UnityEngine;

public class DrunkItemPickup : MonoBehaviour
{
    [Header("Effect Settings")]
    public float effectDuration = 5f;
    public bool destroyOnPickup = true;

    [Header("3D Collision")]
    public bool use3DCollision = true;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private void Start()
    {
        // Verify setup
        Collider col = GetComponent<Collider>();
        Collider2D col2D = GetComponent<Collider2D>();

        if (use3DCollision && col == null)
        {
            Debug.LogError($"[{gameObject.name}] No 3D Collider found! Add a BoxCollider, SphereCollider, etc.");
        }
        else if (!use3DCollision && col2D == null)
        {
            Debug.LogError($"[{gameObject.name}] No 2D Collider found! Add a BoxCollider2D, CircleCollider2D, etc.");
        }

        if (use3DCollision && col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] Collider is not set to 'Is Trigger'. Setting it now.");
            col.isTrigger = true;
        }
        else if (!use3DCollision && col2D != null && !col2D.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] Collider2D is not set to 'Is Trigger'. Setting it now.");
            col2D.isTrigger = true;
        }
    }

    // For 3D colliders
    private void OnTriggerEnter(Collider other)
    {
        if (!use3DCollision) return;

        if (showDebugInfo) Debug.Log($"3D Trigger entered by: {other.gameObject.name} (Tag: {other.tag})");

        if (other.CompareTag("Player"))
        {
            TriggerDrunkEffect();
        }
    }

    // For 2D colliders
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (use3DCollision) return;

        if (showDebugInfo) Debug.Log($"2D Trigger entered by: {other.gameObject.name} (Tag: {other.tag})");

        if (other.CompareTag("Player"))
        {
            TriggerDrunkEffect();
        }
    }

    private void TriggerDrunkEffect()
    {
        if (DrunkEffectController.Instance != null)
        {
            DrunkEffectController.Instance.TriggerEffect(effectDuration);
            Debug.Log($"<color=green>Drunk effect triggered! Duration: {effectDuration}s</color>");
        }
        else
        {
            Debug.LogError("DrunkEffectController not found in scene! Make sure there's a GameObject with DrunkEffectController script.");
        }

        if (destroyOnPickup)
        {
            if (showDebugInfo) Debug.Log($"Destroying pickup: {gameObject.name}");
            Destroy(gameObject);
        }
    }
}