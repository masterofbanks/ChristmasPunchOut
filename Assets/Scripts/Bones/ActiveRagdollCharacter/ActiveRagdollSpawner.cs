using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns physics-based active ragdoll character
/// FIXED: Don't override balancer's foot targets - let it calibrate naturally
/// </summary>
public class ActiveRagdollSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Visual Settings")]
    [SerializeField] private Material voxelMaterial;
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private float voxelSize = 0.1f;

    [Header("Physics Settings")]
    [SerializeField] private float boneMass = 1f;
    [SerializeField] private float jointSpring = 3000f; // REDUCED from 5000 - less springy
    [SerializeField] private float jointDamper = 1000f; // INCREASED from 500 - more damping

    [Header("Animation Settings")]
    [SerializeField] private bool enableProceduralAnimation = false;
    [SerializeField] private float animationInfluence = 0.5f;

    [Header("Ground Detection")]
    [SerializeField] private float groundRaycastHeight = 10f;
    [SerializeField] private float footSpacing = 0.5f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float characterHeight = 8f;

    [Header("Camera Setup")]
    [SerializeField] private bool setupCameraFollow = true;
    [SerializeField] private bool createCameraIfMissing = false;

    [Header("Input System - REQUIRED")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Leave empty to use default scheme")]
    [SerializeField] private string controlScheme = "";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showGroundDetectionGizmos = true;

    private Vector3 detectedGroundPosition;

    private void Start()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ ActiveRagdollSpawner: No Input Actions assigned!");
            return;
        }

        if (spawnOnStart)
        {
            SpawnCharacter();
        }
    }

    public ActiveRagdollCharacter SpawnCharacter()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ Cannot spawn character: No Input Actions asset assigned!");
            return null;
        }

        if (showDebugLogs) Debug.Log("=== SPAWNING ACTIVE RAGDOLL CHARACTER ===");

        Vector3 spawnPosition = transform.position + spawnOffset;
        DetectGroundPosition(spawnPosition);

        // Calculate root position so feet touch ground
        float feetToHipsDistance = characterHeight * 0.5f;
        Vector3 targetRootPosition = detectedGroundPosition + Vector3.up * feetToHipsDistance;

        if (showDebugLogs)
        {
            Debug.Log($"Ground level: {detectedGroundPosition.y:F2}");
            Debug.Log($"Root positioned at: {targetRootPosition}");
        }

        // Build character
        GameObject characterRoot = new GameObject("ActiveRagdollCharacter");
        characterRoot.transform.position = targetRootPosition;
        characterRoot.transform.rotation = transform.rotation;
        characterRoot.tag = "Player";

        ActiveRagdollCharacter character = characterRoot.AddComponent<ActiveRagdollCharacter>();
        character.BuildCharacter();

        // Verify positions
        if (showDebugLogs && character.leftFoot != null)
        {
            Debug.Log($"After build:");
            Debug.Log($"  Hips at Y={character.hips.transform.position.y:F2}");
            Debug.Log($"  Left foot at Y={character.leftFoot.transform.position.y:F2}");
            Debug.Log($"  Ground at Y={detectedGroundPosition.y:F2}");
            float footOffset = character.leftFoot.transform.position.y - detectedGroundPosition.y;
            Debug.Log($"  Foot offset from ground: {footOffset:F2}m");
        }

        SetupInputSystem(characterRoot);
        StartCoroutine(InitializeCharacterPhysics(character));

        return character;
    }

    private void SetupCamera(Transform hipsTransform)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null && createCameraIfMissing)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();
        }

        if (mainCamera != null)
        {
            CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null)
            {
                cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
            }
            cameraFollow.SetTarget(hipsTransform);
        }
    }

    private void SetupInputSystem(GameObject characterRoot)
    {
        PlayerInput playerInput = characterRoot.AddComponent<PlayerInput>();
        playerInput.actions = inputActions;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        playerInput.defaultActionMap = "Player";

        if (!string.IsNullOrEmpty(controlScheme))
        {
            playerInput.defaultControlScheme = controlScheme;
        }

        var playerActionMap = playerInput.actions.FindActionMap("Player");
        if (playerActionMap != null)
        {
            playerActionMap.Enable();
        }

        playerInput.ActivateInput();

        if (showDebugLogs)
        {
            Debug.Log($"✓ Input system configured");
        }
    }

    private System.Collections.IEnumerator InitializeCharacterPhysics(ActiveRagdollCharacter character)
    {
        // Wait for physics to settle
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        GameObject hipsObject = character.hips.gameObject;

        // Add balancer - it will calibrate foot targets itself!
        ActiveRagdollBalancer balancer = hipsObject.AddComponent<ActiveRagdollBalancer>();

        // REMOVED: Don't set foot targets here! Let the balancer find ground itself
        // This was causing conflicts where spawner set targets, then balancer recalculated them

        yield return new WaitForFixedUpdate();

        // Add movement
        ActiveRagdollMovement movement = hipsObject.AddComponent<ActiveRagdollMovement>();

        // Add procedural animation
        if (enableProceduralAnimation)
        {
            ProceduralLegAnimator animator = hipsObject.AddComponent<ProceduralLegAnimator>();
            animator.SetAnimationInfluence(animationInfluence);

            if (showDebugLogs)
            {
                Debug.Log("✓ Procedural leg animator added");
            }
        }

        // Setup camera
        if (setupCameraFollow)
        {
            SetupCamera(character.hips.transform);
        }

        if (showDebugLogs)
        {
            Debug.Log("=== ✓ ACTIVE RAGDOLL SPAWNED - Balancer will calibrate to ground ===");
        }
    }

    private void DetectGroundPosition(Vector3 spawnPosition)
    {
        Vector3 rayStart = spawnPosition + Vector3.up * groundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit centerHit, groundRaycastHeight * 2f, groundLayer))
        {
            detectedGroundPosition = centerHit.point;

            if (showDebugLogs)
            {
                Debug.Log($"✓ Ground detected at Y={detectedGroundPosition.y:F2}");
            }
        }
        else
        {
            detectedGroundPosition = spawnPosition;
            Debug.LogWarning($"⚠ No ground detected! Using spawn position");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGroundDetectionGizmos) return;

        // Draw spawner position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        if (Application.isPlaying)
        {
            // Draw detected ground
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(detectedGroundPosition, 0.4f);

            // Draw expected root position
            float feetToHipsDistance = characterHeight * 0.5f;
            Vector3 expectedRootPos = detectedGroundPosition + Vector3.up * feetToHipsDistance;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(expectedRootPos, Vector3.one * 0.3f);
            Gizmos.DrawLine(detectedGroundPosition, expectedRootPos);
        }
    }
}