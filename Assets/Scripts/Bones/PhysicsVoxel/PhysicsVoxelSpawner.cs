using UnityEngine;

/// <summary>
/// Spawns and configures a physics-based voxel character
/// Handles skeleton building, IK setup, and ragdoll initialization
/// </summary>
public class PhysicsVoxelSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Visual Settings")]
    [SerializeField] private Material voxelMaterial;
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private float voxelSize = 0.1f;
    [SerializeField] private bool useCombinedMesh = true;

    [Header("Physics Settings")]
    [SerializeField] private float characterMass = 70f;
    [SerializeField] private float colliderHeight = 2f;
    [SerializeField] private float colliderRadius = 0.4f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private PhysicsVoxelCharacter spawnedCharacter;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnCharacter();
        }
    }

    public PhysicsVoxelCharacter SpawnCharacter()
    {
        if (showDebugLogs) Debug.Log("=== SPAWNING PHYSICS VOXEL CHARACTER ===");

        // Create root GameObject
        GameObject characterRoot = new GameObject("PhysicsVoxelPlayer");
        characterRoot.transform.position = spawnPosition;
        characterRoot.transform.rotation = Quaternion.identity;
        characterRoot.tag = "Player";

        // Add character component and build skeleton FIRST
        PhysicsVoxelCharacter character = characterRoot.AddComponent<PhysicsVoxelCharacter>();
        character.BuildCharacter();

        // IMPORTANT: Wait for skeleton to build before setting up physics
        // This ensures hips transform exists
        if (character.hips == null)
        {
            Debug.LogError("Failed to build character - hips not found!");
            return null;
        }

        // Setup physics on the ROOT (not hips)
        SetupRootPhysics(characterRoot, character);

        // Add procedural leg controller
        ProceduralLegController legController = characterRoot.AddComponent<ProceduralLegController>();

        // Add movement controller
        PhysicsVoxelMovement movement = characterRoot.AddComponent<PhysicsVoxelMovement>();
        movement.moveSpeed = moveSpeed;

        // Add upper body ragdoll but keep it disabled
        UpperBodyRagdoll ragdoll = characterRoot.AddComponent<UpperBodyRagdoll>();

        spawnedCharacter = character;

        if (showDebugLogs)
        {
            Debug.Log("=== ✓ PHYSICS VOXEL CHARACTER SPAWNED ===");
        }

        return character;
    }

    private void SetupRootPhysics(GameObject root, PhysicsVoxelCharacter character)
    {
        // Add main rigidbody to ROOT (this will carry the character)
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = characterMass;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = true;

        // Add capsule collider for collision
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = colliderHeight;
        collider.radius = colliderRadius;
        collider.center = new Vector3(0, colliderHeight / 2f, 0);
        collider.direction = 1; // Y-axis

        // Make hips follow root position (NOT kinematic, but connected)
        // We'll do this in ProceduralLegController by making hips follow root

        if (showDebugLogs)
        {
            Debug.Log($"✓ Physics setup: Mass={characterMass}, Collider H={colliderHeight}, R={colliderRadius}");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(spawnPosition, 0.3f);

        if (Application.isPlaying && spawnedCharacter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnedCharacter.transform.position, Vector3.one * 0.5f);

            // Draw collider visualization
            CapsuleCollider col = spawnedCharacter.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                Gizmos.color = Color.green;
                Vector3 center = spawnedCharacter.transform.position + col.center;
                Gizmos.DrawWireSphere(center + Vector3.up * (col.height / 2f - col.radius), col.radius);
                Gizmos.DrawWireSphere(center + Vector3.down * (col.height / 2f - col.radius), col.radius);
            }
        }
    }
}