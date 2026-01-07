using UnityEngine;
using System.Collections;

/// <summary>
/// Health system for NPCs with hit reactions and death
/// </summary>
public class NPCHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Hit Reaction")]
    [SerializeField] private float freezeDuration = 0.15f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private int flashCount = 3;

    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 2f;

    private bool isDead = false;
    private bool isInvulnerable = false;
    private Rigidbody rb;
    private CubeSkeletonCharacter skeleton;
    private CubeSkeletonAnimator animator;
    private NPCAIController aiController;

    // For material flashing
    private Renderer[] allRenderers;
    private MaterialPropertyBlock[] originalPropertyBlocks;
    private Color[] originalColors;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        skeleton = GetComponent<CubeSkeletonCharacter>();
        animator = GetComponent<CubeSkeletonAnimator>();
        aiController = GetComponent<NPCAIController>();

        // Cache all renderers for flash effect
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        Transform skeletonVisuals = skeleton.transform.Find("SkeletonVisuals");
        if (skeletonVisuals != null)
        {
            allRenderers = skeletonVisuals.GetComponentsInChildren<Renderer>();
            originalPropertyBlocks = new MaterialPropertyBlock[allRenderers.Length];
            originalColors = new Color[allRenderers.Length];

            for (int i = 0; i < allRenderers.Length; i++)
            {
                originalPropertyBlocks[i] = new MaterialPropertyBlock();
                allRenderers[i].GetPropertyBlock(originalPropertyBlocks[i]);

                // Try to get the current color
                if (allRenderers[i].material.HasProperty("_Color"))
                {
                    originalColors[i] = allRenderers[i].material.color;
                }
                else
                {
                    originalColors[i] = Color.white;
                }
            }
        }
    }

    public void TakeDamage(float damage, Vector3 attackDirection)
    {
        if (isDead || isInvulnerable) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(HitReactionSequence(attackDirection));
        }
    }

    private IEnumerator HitReactionSequence(Vector3 attackDirection)
    {
        isInvulnerable = true;

        // Disable AI temporarily
        if (aiController != null)
        {
            aiController.enabled = false;
        }

        // 1. FREEZE EFFECT - Stop all movement
        if (rb != null)
        {
            Vector3 frozenVelocity = rb.linearVelocity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            yield return new WaitForSeconds(freezeDuration);

            // 2. KNOCKBACK
            Vector3 knockbackDir = attackDirection.normalized;
            knockbackDir.y = 0.2f; // Add slight upward component
            rb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
        }
        else
        {
            yield return new WaitForSeconds(freezeDuration);
        }

        // 3. WHITE FLASH EFFECT
        StartCoroutine(FlashWhite());

        // Wait for flash to complete
        yield return new WaitForSeconds(flashDuration);

        // Re-enable AI
        if (aiController != null)
        {
            aiController.enabled = true;
        }

        isInvulnerable = false;
    }

    private IEnumerator FlashWhite()
    {
        float flashInterval = flashDuration / (flashCount * 2);

        for (int i = 0; i < flashCount; i++)
        {
            // Flash to white
            SetAllRenderersColor(Color.white);
            yield return new WaitForSeconds(flashInterval);

            // Flash back to original
            RestoreOriginalColors();
            yield return new WaitForSeconds(flashInterval);
        }

        // Ensure we end on original color
        RestoreOriginalColors();
    }

    private void SetAllRenderersColor(Color color)
    {
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        foreach (Renderer renderer in allRenderers)
        {
            propBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propBlock);
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", originalColors[i]);
            allRenderers[i].SetPropertyBlock(propBlock);
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // Disable AI
        if (aiController != null)
        {
            aiController.enabled = false;
        }

        // Trigger death animation
        if (animator != null)
        {
            animator.TriggerDeath();
        }

        // Trigger skeleton shatter
        if (skeleton != null)
        {
            skeleton.TriggerDeath(transform.position);
        }

        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Destroy after delay
        Destroy(gameObject, deathDelay);
    }

    public bool IsDead() => isDead;
    public float GetHealthPercentage() => currentHealth / maxHealth;
}