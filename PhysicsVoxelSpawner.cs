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
    [SerializeField] private float characterMass = 70f; // More realistic mass
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

        // Setup main physics body (for the root/hips)
        SetupMainPhysics(characterRoot, character);

        // Add procedural leg controller
        ProceduralLegController legController = characterRoot.AddComponent<ProceduralLegController>();

        // Add movement controller
        PhysicsVoxelMovement movement = characterRoot.AddComponent<PhysicsVoxelMovement>();
        movement.moveSpeed = moveSpeed;

        // Add upper body ragdoll but DON'T initialize it yet
        UpperBodyRagdoll ragdoll = characterRoot.AddComponent<UpperBodyRagdoll>();
        // We'll initialize it manually later or disable it for now

        spawnedCharacter = character;

        if (showDebugLogs)
        {
            Debug.Log("=== ? PHYSICS VOXEL CHARACTER SPAWNED (Ragdoll disabled) ===");
        }

        return character;
    }

    private void SetupMainPhysics(GameObject root, PhysicsVoxelCharacter character)
    {
        // The main rigidbody should be on the HIPS, not the root
        // Remove any rigidbody from root first
        Rigidbody rootRb = root.GetComponent<Rigidbody>();
        if (rootRb != null)
        {
            Destroy(rootRb);
        }

        // Add rigidbody to the actual visual root or hips
        Transform hips = character.hips;
        if (hips == null)
        {
            Debug.LogError("Cannot setup physics - hips not found!");
            return;
        }

        // Make the hips kinematic (controlled by procedural animation)
        Rigidbody hipsRb = hips.gameObject.AddComponent<Rigidbody>();
        hipsRb.mass = characterMass;
        hipsRb.isKinematic = true; // Kinematic because procedural animation controls it
        hipsRb.interpolation = RigidbodyInterpolation.Interpolate;

        // Add main collider to root for overall collision
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = colliderHeight;
        collider.radius = colliderRadius;
        collider.center = new Vector3(0, colliderHeight / 2f, 0);
        collider.direction = 1; // Y-axis
        
        // Make sure root has a rigidbody for the collider
        Rigidbody mainRb = root.AddComponent<Rigidbody>();
        mainRb.mass = characterMass;
        mainRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        mainRb.interpolation = RigidbodyInterpolation.Interpolate;
        mainRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        mainRb.useGravity = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(spawnPosition, 0.3f);

        if (Application.isPlaying && spawnedCharacter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnedCharacter.transform.position, Vector3.one * 0.5f);
        }
    }
}