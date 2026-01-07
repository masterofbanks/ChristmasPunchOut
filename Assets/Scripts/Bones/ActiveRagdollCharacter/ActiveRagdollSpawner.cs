using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns physics-based active ragdoll character
/// Uses the SAME input actions as CubeSkeletonCharacter
/// FIXED: Root GameObject follows hips so camera can track properly
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
    [SerializeField] private float jointSpring = 5000f;
    [SerializeField] private float jointDamper = 500f;

    [Header("Balance Settings")]
    [SerializeField] private float balanceStrength = 50f;
    [SerializeField] private float hipHeightTarget = 1.5f;

    [Header("Ground Detection")]
    [SerializeField] private float groundRaycastHeight = 10f;
    [SerializeField] private float footSpacing = 0.5f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float footHeightOffset = 0.05f;

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
    private Vector3 leftFootGroundPos;
    private Vector3 rightFootGroundPos;

    private void Start()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ ActiveRagdollSpawner: No Input Actions assigned!");
            Debug.LogError("   Assign the SAME InputActionAsset used by CubePlayerSpawner");
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
        DetectGroundAndFootPositions(spawnPosition);

        GameObject characterRoot = new GameObject("ActiveRagdollCharacter");
        characterRoot.transform.position = detectedGroundPosition;
        characterRoot.transform.rotation = transform.rotation;
        characterRoot.tag = "Player";

        if (showDebugLogs)
        {
            Debug.Log($"Spawner position: {transform.position}");
            Debug.Log($"Ground detected at: {detectedGroundPosition}");
        }

        // Build character FIRST
        ActiveRagdollCharacter character = characterRoot.AddComponent<ActiveRagdollCharacter>();
        character.BuildCharacter();

        // NEW: Add RootFollower to make root follow hips
        RootFollower rootFollower = characterRoot.AddComponent<RootFollower>();
        // It will auto-find hips, but we can also set it explicitly:
        if (character.hips != null)
        {
            rootFollower.SetTarget(character.hips.transform);
            if (showDebugLogs)
            {
                Debug.Log("✓ RootFollower added - root will follow hips");
            }
        }

        // Setup input system BEFORE movement component
        SetupInputSystem(characterRoot);

        // Setup camera to follow root (which now follows hips!)
        if (setupCameraFollow)
        {
            SetupCamera(characterRoot.transform);
        }

        // Initialize physics (this adds ActiveRagdollBalancer and ActiveRagdollMovement)
        StartCoroutine(InitializeCharacterPhysics(character));

        return character;
    }

    private void SetupCamera(Transform playerTransform)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null && createCameraIfMissing)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            if (showDebugLogs)
            {
                Debug.Log("✓ Created new camera");
            }
        }

        if (mainCamera != null)
        {
            CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();

            if (cameraFollow == null)
            {
                cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
                if (showDebugLogs)
                {
                    Debug.Log("✓ Added CameraFollow component");
                }
            }

            // Camera follows root (which follows hips via RootFollower)
            cameraFollow.SetTarget(playerTransform);

            if (showDebugLogs)
            {
                Debug.Log("✓ Camera setup complete - following root (which follows hips)");
            }
        }
        else
        {
            Debug.LogWarning("⚠ No main camera found!");
        }
    }

    private void SetupInputSystem(GameObject characterRoot)
    {
        if (showDebugLogs)
        {
            Debug.Log("=== SETTING UP INPUT SYSTEM ===");
            Debug.Log($"Input Actions Asset: {inputActions.name}");
        }

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

            var moveAction = playerActionMap.FindAction("Move");
            if (moveAction != null)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"✓ Found 'Move' action");
                    Debug.Log($"  Move action enabled: {moveAction.enabled}");
                    Debug.Log($"  Move action bindings: {moveAction.bindings.Count}");
                }
            }
            else
            {
                Debug.LogError("❌ 'Move' action not found in 'Player' action map!");
            }

            if (showDebugLogs)
            {
                Debug.Log("✓ Input action map 'Player' enabled");
            }
        }
        else
        {
            Debug.LogError("❌ 'Player' action map not found in InputActionAsset!");
        }

        playerInput.ActivateInput();

        if (showDebugLogs)
        {
            Debug.Log($"✓ PlayerInput activated");
            Debug.Log($"  Current action map: {playerInput.currentActionMap?.name ?? "NULL"}");
        }
    }

    private System.Collections.IEnumerator InitializeCharacterPhysics(ActiveRagdollCharacter character)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        PositionCharacterAboveGround(character);

        ActiveRagdollBalancer balancer = character.gameObject.AddComponent<ActiveRagdollBalancer>();

        yield return new WaitForFixedUpdate();

        Vector3 leftFootTargetWithOffset = leftFootGroundPos + Vector3.up * footHeightOffset;
        Vector3 rightFootTargetWithOffset = rightFootGroundPos + Vector3.up * footHeightOffset;

        balancer.UpdateFootTarget(true, leftFootTargetWithOffset);
        balancer.UpdateFootTarget(false, rightFootTargetWithOffset);

        ActiveRagdollMovement movement = character.gameObject.AddComponent<ActiveRagdollMovement>();

        if (showDebugLogs)
        {
            Debug.Log("✓ Character physics initialized");
            Debug.Log("=== ✓ ACTIVE RAGDOLL SPAWNED SUCCESSFULLY ===");
        }
    }

    private void DetectGroundAndFootPositions(Vector3 spawnPosition)
    {
        Vector3 rayStart = spawnPosition + Vector3.up * groundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit centerHit, groundRaycastHeight * 2f, groundLayer))
        {
            detectedGroundPosition = centerHit.point;

            if (showDebugLogs)
            {
                Debug.Log($"Ground hit at {centerHit.point}");
            }
        }
        else
        {
            detectedGroundPosition = spawnPosition;
            Debug.LogWarning($"No ground detected beneath spawn position!");
        }

        Vector3 leftFootOffset = transform.right * -footSpacing;
        Vector3 leftRayStart = detectedGroundPosition + leftFootOffset + Vector3.up * 2f;

        if (Physics.Raycast(leftRayStart, Vector3.down, out RaycastHit leftHit, 5f, groundLayer))
        {
            leftFootGroundPos = leftHit.point;
        }
        else
        {
            leftFootGroundPos = detectedGroundPosition + leftFootOffset;
        }

        Vector3 rightFootOffset = transform.right * footSpacing;
        Vector3 rightRayStart = detectedGroundPosition + rightFootOffset + Vector3.up * 2f;

        if (Physics.Raycast(rightRayStart, Vector3.down, out RaycastHit rightHit, 5f, groundLayer))
        {
            rightFootGroundPos = rightHit.point;
        }
        else
        {
            rightFootGroundPos = detectedGroundPosition + rightFootOffset;
        }
    }

    private void PositionCharacterAboveGround(ActiveRagdollCharacter character)
    {
        if (character == null || character.hips == null) return;

        float avgFootHeight = (leftFootGroundPos.y + rightFootGroundPos.y) / 2f;
        float leftFootCurrentY = character.leftFoot.transform.position.y;
        float rightFootCurrentY = character.rightFoot.transform.position.y;
        float avgCurrentFootY = (leftFootCurrentY + rightFootCurrentY) / 2f;

        float footHeightOffsetCalc = avgFootHeight - avgCurrentFootY + this.footHeightOffset;
        character.transform.position += Vector3.up * footHeightOffsetCalc;

        if (showDebugLogs)
        {
            Debug.Log($"Character lifted by {footHeightOffsetCalc:F2} units");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGroundDetectionGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        Gizmos.color = Color.blue;
        Vector3 spawnPos = transform.position + spawnOffset;
        Gizmos.DrawWireSphere(spawnPos, 0.2f);
        Gizmos.DrawLine(transform.position, spawnPos);

        Vector3 rayStart = spawnPos + Vector3.up * groundRaycastHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * groundRaycastHeight * 2f);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(detectedGroundPosition, 0.4f);

            Gizmos.color = Color.red;
            Vector3 leftTarget = leftFootGroundPos + Vector3.up * footHeightOffset;
            Vector3 rightTarget = rightFootGroundPos + Vector3.up * footHeightOffset;

            Gizmos.DrawWireSphere(leftTarget, 0.15f);
            Gizmos.DrawWireSphere(rightTarget, 0.15f);
        }
    }
}