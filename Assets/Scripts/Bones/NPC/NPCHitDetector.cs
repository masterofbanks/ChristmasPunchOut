using UnityEngine;

/// <summary>
/// Detects when NPC is hit by player attacks
/// Attach to NPC root object
/// </summary>
public class NPCHitDetector : MonoBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private float damagePerHit = 25f;

    private NPCHealth healthComponent;

    void Awake()
    {
        healthComponent = GetComponent<NPCHealth>();
        if (healthComponent == null)
        {
            Debug.LogError($"NPCHitDetector on {gameObject.name} requires NPCHealth component!");
        }
    }

    // Call this from player's attack system
    public void OnHitByPlayer(Vector3 attackDirection, float damage = -1)
    {
        if (healthComponent == null) return;

        float actualDamage = damage > 0 ? damage : damagePerHit;

        // Calculate attack direction from attacker to this NPC
        Vector3 hitDirection = attackDirection.normalized;

        healthComponent.TakeDamage(actualDamage, hitDirection);

        Debug.Log($"NPC {gameObject.name} hit for {actualDamage} damage!");
    }

    // For simple collision-based attacks
    private void OnTriggerEnter(Collider other)
    {
        // Check if hit by player attack
        if (other.CompareTag("PlayerAttack"))
        {
            Vector3 attackDir = (transform.position - other.transform.position).normalized;
            OnHitByPlayer(attackDir);
        }
    }
}