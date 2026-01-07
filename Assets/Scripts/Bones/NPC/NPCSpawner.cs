using UnityEngine;

/// <summary>
/// Spawns NPC characters with AI behavior
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private int npcCount = 3;
    [SerializeField] private float spawnSpacing = 3f;

    [Header("Visual Settings")]
    [SerializeField] private Material npcMaterial;
    [SerializeField] private Color npcColor = Color.red;

    [Header("Character Proportions")]
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private float voxelSize = 0.1f;
    [SerializeField] private bool useCombinedMesh = true;

    [Header("Physics Settings")]
    [SerializeField] private float characterMass = 1f;
    [SerializeField] private float colliderHeight = 2f;
    [SerializeField] private float colliderRadius = 0.3f;

    [Header("NPC Settings")]
    [SerializeField] private float npcHealth = 100f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float wanderRadius = 10f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnNPCs();
        }
    }

    public void SpawnNPCs()
    {
        for (int i = 0; i < npcCount; i++)
        {
            Vector3 offset = new Vector3(i * spawnSpacing, 0, 0);
            // FIXED: Use transform.position to make spawn relative to spawner
            Vector3 worldPosition = transform.position + spawnPosition + offset;
            SpawnSingleNPC(worldPosition);
        }
    }

    public GameObject SpawnSingleNPC(Vector3 position)
    {
        if (showDebugLogs) Debug.Log($"=== SPAWNING NPC at {position} ===");

        // Create root GameObject
        GameObject npcRoot = new GameObject("VoxelNPC");
        npcRoot.transform.position = position;
        npcRoot.transform.rotation = Quaternion.identity;

        // ASSIGN LAYER AT RUNTIME
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex == -1)
        {
            Debug.LogWarning("⚠ 'Enemy' layer doesn't exist! Creating with default layer.");
            Debug.LogWarning("   Please create 'Enemy' layer in Project Settings > Tags & Layers");
            npcRoot.layer = LayerMask.NameToLayer("Default");
        }
        else
        {
            npcRoot.layer = enemyLayerIndex;

            if (showDebugLogs)
            {
                Debug.Log($"✓ NPC assigned to 'Enemy' layer (index: {enemyLayerIndex})");
            }
        }

        npcRoot.tag = "Enemy"; // Also set tag

        // Build skeleton
        CubeSkeletonCharacter skeleton = npcRoot.AddComponent<CubeSkeletonCharacter>();
        skeleton.BuildSkeleton(cubeUnit, voxelSize, npcMaterial, useCombinedMesh);

        // Color the NPC
        ColorNPC(skeleton, npcColor);

        // Adjust skeleton position
        Transform skeletonVisuals = skeleton.transform.Find("SkeletonVisuals");
        if (skeletonVisuals != null)
        {
            Bounds bounds = CalculateBounds(skeletonVisuals);
            float offsetNeeded = position.y - bounds.min.y;
            skeletonVisuals.position += Vector3.up * offsetNeeded;
        }

        // Setup physics
        Rigidbody rb = npcRoot.AddComponent<Rigidbody>();
        rb.mass = characterMass;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        CapsuleCollider collider = npcRoot.AddComponent<CapsuleCollider>();
        collider.height = colliderHeight;
        collider.radius = colliderRadius;
        collider.center = new Vector3(0, colliderHeight / 2f, 0);

        // Add NPC components
        NPCHealth health = npcRoot.AddComponent<NPCHealth>();
        NPCAIController ai = npcRoot.AddComponent<NPCAIController>();
        NPCHitDetector hitDetector = npcRoot.AddComponent<NPCHitDetector>();

        CubeSkeletonAnimator animator = npcRoot.AddComponent<CubeSkeletonAnimator>();
        animator.Initialize(skeleton);

        if (showDebugLogs) Debug.Log("✓ NPC spawned successfully");

        return npcRoot;
    }

    private void ColorNPC(CubeSkeletonCharacter skeleton, Color color)
    {
        Transform skeletonVisuals = skeleton.transform.Find("SkeletonVisuals");
        if (skeletonVisuals != null)
        {
            Renderer[] renderers = skeletonVisuals.GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", color);

            foreach (Renderer renderer in renderers)
            {
                renderer.SetPropertyBlock(propBlock);
            }
        }
    }

    private Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        // FIXED: Draw gizmos relative to spawner position
        for (int i = 0; i < npcCount; i++)
        {
            Vector3 offset = new Vector3(i * spawnSpacing, 0, 0);
            Vector3 worldPosition = transform.position + spawnPosition + offset;
            Gizmos.DrawWireSphere(worldPosition, 0.3f);
        }

        // Draw spawner position
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
#endif
}