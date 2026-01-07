using UnityEngine;

/// <summary>
/// Handles player attack detection and applies damage to NPCs
/// Attach to player character
/// </summary>
public class PlayerAttackHandler : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackAngle = 60f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;

    private CubeSkeletonAnimator animator;

    void Awake()
    {
        animator = GetComponent<CubeSkeletonAnimator>();
    }

    // NEW: Public methods to configure at runtime
    public void SetEnemyLayer(LayerMask layer)
    {
        enemyLayer = layer;
    }

    public void SetAttackSettings(float range, float angle, float damage)
    {
        attackRange = range;
        attackAngle = angle;
        attackDamage = damage;
    }

    // Call this method when attack animation plays
    public void PerformAttack()
    {
        // Find all enemies in range
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        foreach (Collider hit in hits)
        {
            // Check if enemy is in front of player
            Vector3 directionToEnemy = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToEnemy);

            if (angle < attackAngle / 2f)
            {
                // Hit detected!
                NPCHitDetector npcHit = hit.GetComponent<NPCHitDetector>();
                if (npcHit != null)
                {
                    Vector3 attackDirection = directionToEnemy;
                    npcHit.OnHitByPlayer(attackDirection, attackDamage);

                    if (showDebugRays)
                    {
                        Debug.DrawRay(transform.position, attackDirection * attackRange, Color.red, 1f);
                    }
                }
            }
        }
    }

    // Call this from animation event or input callback
    public void TriggerAttack()
    {
        if (animator != null)
        {
            animator.TriggerAttack();
        }

        // Perform attack check after short delay (when punch extends)
        Invoke(nameof(PerformAttack), 0.15f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw attack cone
        Vector3 forward = transform.forward * attackRange;
        Quaternion leftRotation = Quaternion.Euler(0, -attackAngle / 2f, 0);
        Quaternion rightRotation = Quaternion.Euler(0, attackAngle / 2f, 0);

        Vector3 leftBoundary = leftRotation * forward;
        Vector3 rightBoundary = rightRotation * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftBoundary);
        Gizmos.DrawRay(transform.position, rightBoundary);
    }
#endif
}