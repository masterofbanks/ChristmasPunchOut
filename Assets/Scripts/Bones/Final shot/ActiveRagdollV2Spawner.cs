using UnityEngine;

/// <summary>
/// Complete spawner for ActiveRagdollV2 bipedal system
/// FIXED: Prevents double-initialization and ensures proper spawn height
/// </summary>
public class ActiveRagdollV2Spawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Character Settings")]
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private float boneMass = 1f;

    [Header("Ground Detection")]
    [SerializeField] private float groundRaycastHeight = 10f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float spawnHeightAboveGround = 5f;  // NEW: Spawn character this high above ground

    [Header("Balance Tuning")]
    [SerializeField] private float hipBalanceTorque = 1500f;
    [SerializeField] private float legStiffnessTorque = 3000f;
    [SerializeField] private float comBalanceTorque = 800f;
    [SerializeField] private float comTargetOffsetForward = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showGroundDetectionGizmos = true;

    private Vector3 detectedGroundPosition;
    private ActiveRagdollV2 spawnedCharacter;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnCharacter();
        }
    }

    /// <summary>
    /// Spawns a complete bipedal character at the spawner's position
    /// FIXED: Proper spawn height and prevents double-initialization
    /// </summary>
    public ActiveRagdollV2 SpawnCharacter()
    {
        if (showDebugLogs) Debug.Log("=== SPAWNING ACTIVE RAGDOLL V2 ===");

        // 1. Detect ground position
        Vector3 spawnPosition = transform.position + spawnOffset;
        DetectGroundPosition(spawnPosition);

        // 2. Calculate proper spawn position (above ground, not at ground level)
        Vector3 characterSpawnPosition = detectedGroundPosition + Vector3.up * spawnHeightAboveGround;

        if (showDebugLogs)
        {
            Debug.Log($"Ground level: Y={detectedGroundPosition.y:F2}");
            Debug.Log($"Spawning character at: Y={characterSpawnPosition.y:F2} ({spawnHeightAboveGround}m above ground)");
        }

        // 3. Create root GameObject
        GameObject characterRoot = new GameObject("ActiveRagdollV2_Character");
        characterRoot.transform.position = characterSpawnPosition;
        characterRoot.transform.rotation = transform.rotation;
        characterRoot.tag = "Player";

        // 4. Add ActiveRagdollV2 component
        ActiveRagdollV2 ragdoll = characterRoot.AddComponent<ActiveRagdollV2>();

        // 5. CRITICAL: Set configuration BEFORE building
        SetPrivateField(ragdoll, "cubeUnit", cubeUnit);
        SetPrivateField(ragdoll, "bodyMaterial", bodyMaterial);
        SetPrivateField(ragdoll, "boneMass", boneMass);

        // 6. MANUALLY call BuildCharacter() - don't rely on Start()
        ragdoll.BuildCharacter();

        if (showDebugLogs)
        {
            Debug.Log($"✓ Character built");
            Debug.Log($"  Hips: {(ragdoll.hips != null ? "OK" : "NULL")}");
            Debug.Log($"  Hips position: {ragdoll.hips.transform.position}");
            Debug.Log($"  Left foot: {(ragdoll.leftFoot != null ? "OK" : "NULL")}");
            Debug.Log($"  Right foot: {(ragdoll.rightFoot != null ? "OK" : "NULL")}");
        }

        // 7. CRITICAL: Destroy the balance controller that was created in BuildCharacter
        // We'll create our own with custom settings
        BipedalBalanceController existingBalancer = ragdoll.GetComponent<BipedalBalanceController>();
        if (existingBalancer != null)
        {
            Destroy(existingBalancer);
            if (showDebugLogs) Debug.Log("  Removed auto-created balance controller");
        }

        // 8. Wait for physics to settle, then configure balance
        StartCoroutine(InitializeBalanceSystem(ragdoll));

        spawnedCharacter = ragdoll;

        return ragdoll;
    }

    private System.Collections.IEnumerator InitializeBalanceSystem(ActiveRagdollV2 ragdoll)
    {
        // Wait for one physics frame to let rigidbodies initialize
        yield return new WaitForFixedUpdate();

        if (showDebugLogs)
        {
            Debug.Log("--- Initializing Balance System ---");
            Debug.Log($"  Hips position: {ragdoll.hips.transform.position}");
            Debug.Log($"  Left foot position: {ragdoll.leftFoot.transform.position}");
            Debug.Log($"  Right foot position: {ragdoll.rightFoot.transform.position}");
        }

        // Create NEW balance controller with spawner's settings
        BipedalBalanceController balancer = ragdoll.gameObject.AddComponent<BipedalBalanceController>();

        // Set references
        balancer.hips = ragdoll.hips;
        balancer.leftUpperLeg = ragdoll.leftUpperLeg;
        balancer.leftLowerLeg = ragdoll.leftLowerLeg;
        balancer.leftFoot = ragdoll.leftFoot;
        balancer.rightUpperLeg = ragdoll.rightUpperLeg;
        balancer.rightLowerLeg = ragdoll.rightLowerLeg;
        balancer.rightFoot = ragdoll.rightFoot;

        // Apply spawner's tuning parameters BEFORE initialization
        SetPrivateField(balancer, "hipBalanceTorque", hipBalanceTorque);
        SetPrivateField(balancer, "legStiffnessTorque", legStiffnessTorque);
        SetPrivateField(balancer, "comBalanceTorque", comBalanceTorque);
        SetPrivateField(balancer, "comTargetOffsetForward", comTargetOffsetForward);

        if (showDebugLogs)
        {
            Debug.Log("  Created and configured balance controller");
            Debug.Log($"    Hip balance: {hipBalanceTorque}");
            Debug.Log($"    Leg stiffness: {legStiffnessTorque}");
            Debug.Log($"    COM balance: {comBalanceTorque}");
        }

        // Initialize balance controller
        balancer.Initialize();

        // Wait another frame for initialization to complete
        yield return new WaitForFixedUpdate();

        if (showDebugLogs)
        {
            Debug.Log("=== ✓ ACTIVE RAGDOLL V2 READY ===");
            Debug.Log($"  Balance quality: {balancer.GetBalanceQuality():F2}");
        }

        // Verify all rigidbodies are active
        VerifyPhysicsSetup(ragdoll);
    }

    private void VerifyPhysicsSetup(ActiveRagdollV2 ragdoll)
    {
        if (!showDebugLogs) return;

        Debug.Log("--- Physics Verification ---");

        // Check all body parts have rigidbodies
        RagdollJoint[] bodyParts = new RagdollJoint[]
        {
            ragdoll.hips, ragdoll.spine, ragdoll.chest,
            ragdoll.leftUpperLeg, ragdoll.leftLowerLeg, ragdoll.leftFoot,
            ragdoll.rightUpperLeg, ragdoll.rightLowerLeg, ragdoll.rightFoot
        };

        int validCount = 0;
        int totalCount = bodyParts.Length;

        foreach (var part in bodyParts)
        {
            if (part == null)
            {
                Debug.LogError($"  ❌ NULL body part!");
                continue;
            }

            Rigidbody rb = part.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError($"  ❌ {part.name}: Missing Rigidbody");
            }
            else if (rb.isKinematic)
            {
                Debug.LogWarning($"  ⚠ {part.name}: Rigidbody is kinematic!");
            }
            else
            {
                validCount++;
                Debug.Log($"  ✓ {part.name}: Rigidbody OK (mass={rb.mass:F1})");
            }
        }

        Debug.Log($"Physics validation: {validCount}/{totalCount} parts OK");
    }

    private void DetectGroundPosition(Vector3 spawnPosition)
    {
        Vector3 rayStart = spawnPosition + Vector3.up * groundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastHeight * 2f, groundLayer))
        {
            detectedGroundPosition = hit.point;

            if (showDebugLogs)
            {
                Debug.Log($"✓ Ground detected at Y={detectedGroundPosition.y:F2} ({hit.collider.name})");
            }
        }
        else
        {
            detectedGroundPosition = spawnPosition;
            Debug.LogWarning($"⚠ No ground detected! Using spawn position as ground level");
        }
    }

    /// <summary>
    /// Helper to set private/serialized fields via reflection
    /// </summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }

    /// <summary>
    /// Spawns character at a specific world position
    /// </summary>
    public ActiveRagdollV2 SpawnCharacterAt(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        return SpawnCharacter();
    }

    /// <summary>
    /// Despawns the current character
    /// </summary>
    public void DespawnCharacter()
    {
        if (spawnedCharacter != null)
        {
            Destroy(spawnedCharacter.gameObject);
            spawnedCharacter = null;

            if (showDebugLogs)
            {
                Debug.Log("Character despawned");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGroundDetectionGizmos) return;

        // Draw spawner position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + spawnOffset, 0.3f);
        Gizmos.DrawRay(transform.position + spawnOffset, Vector3.up * 0.5f);

        // Draw spawn label
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + spawnOffset + Vector3.up * 0.8f,
            "ActiveRagdoll V2\nSpawner",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.cyan },
                alignment = TextAnchor.MiddleCenter
            });
#endif

        if (Application.isPlaying)
        {
            // Draw detected ground
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(detectedGroundPosition, 0.4f);

            // Draw actual spawn position (above ground)
            Vector3 spawnPos = detectedGroundPosition + Vector3.up * spawnHeightAboveGround;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(spawnPos, 0.5f);
            Gizmos.DrawLine(detectedGroundPosition, spawnPos);

            // Draw ground plane
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawCube(detectedGroundPosition, new Vector3(3f, 0.02f, 3f));

            // Draw spawned character indicator
            if (spawnedCharacter != null && spawnedCharacter.hips != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(spawnedCharacter.hips.transform.position, 0.2f);
                Gizmos.DrawLine(detectedGroundPosition, spawnedCharacter.hips.transform.position);
            }
        }
    }

    private void OnValidate()
    {
        // Clamp values to sane ranges
        cubeUnit = Mathf.Clamp(cubeUnit, 0.1f, 5f);
        boneMass = Mathf.Clamp(boneMass, 0.1f, 10f);
        spawnHeightAboveGround = Mathf.Clamp(spawnHeightAboveGround, 1f, 20f);
        hipBalanceTorque = Mathf.Clamp(hipBalanceTorque, 100f, 5000f);
        legStiffnessTorque = Mathf.Clamp(legStiffnessTorque, 500f, 10000f);
        comBalanceTorque = Mathf.Clamp(comBalanceTorque, 100f, 3000f);
        comTargetOffsetForward = Mathf.Clamp(comTargetOffsetForward, -0.5f, 0.5f);
    }
}