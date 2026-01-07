using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movement input for active ragdoll
/// Uses camera-relative movement controls
/// FIXED: Reads input value directly every frame
/// </summary>
public class ActiveRagdollMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showFrameByFrameLogs = false;

    private ActiveRagdollBalancer balancer;
    private Camera mainCamera;
    private Vector2 movementInput;
    private bool isInitialized = false;
    private PlayerInput playerInput;
    private InputAction moveAction; // NEW: Direct reference to move action
    private int logFrameCounter = 0;

    private void Awake()
    {
        Debug.Log("=== [ActiveRagdollMovement] AWAKE ===");

        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ PlayerInput component NOT FOUND on " + gameObject.name);
        }
        else
        {
            Debug.Log($"[ActiveRagdollMovement] ✓ PlayerInput component found");
            Debug.Log($"  - Actions: {(playerInput.actions != null ? playerInput.actions.name : "NULL")}");
            Debug.Log($"  - Current Action Map: {(playerInput.currentActionMap != null ? playerInput.currentActionMap.name : "NULL")}");
            Debug.Log($"  - Notification Behavior: {playerInput.notificationBehavior}");

            if (playerInput.actions != null)
            {
                // NEW: Get direct reference to Move action
                moveAction = playerInput.actions.FindAction("Move");
                if (moveAction != null)
                {
                    Debug.Log($"  - Move Action Found: {moveAction.name}");
                    Debug.Log($"    - Enabled: {moveAction.enabled}");
                    Debug.Log($"    - Action Type: {moveAction.type}");
                    Debug.Log($"    - Bindings: {moveAction.bindings.Count}");

                    // List all bindings
                    for (int i = 0; i < moveAction.bindings.Count; i++)
                    {
                        var binding = moveAction.bindings[i];
                        Debug.Log($"      [{i}] {binding.path} (isComposite: {binding.isComposite}, isPartOfComposite: {binding.isPartOfComposite})");
                    }
                }
                else
                {
                    Debug.LogError("[ActiveRagdollMovement] ❌ 'Move' action NOT FOUND in actions!");
                }
            }
        }
    }

    private void Start()
    {
        Debug.Log("=== [ActiveRagdollMovement] START ===");

        balancer = GetComponent<ActiveRagdollBalancer>();
        mainCamera = Camera.main;

        if (balancer == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ ActiveRagdollBalancer not found!");
        }
        else
        {
            Debug.Log("[ActiveRagdollMovement] ✓ ActiveRagdollBalancer found");
        }

        if (mainCamera == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ No main camera found!");
        }
        else
        {
            Debug.Log($"[ActiveRagdollMovement] ✓ Camera found: {mainCamera.name}");
        }

        isInitialized = true;
        Debug.Log("[ActiveRagdollMovement] ✓ Initialized and ready for input");
        Debug.Log("=== PRESS WASD TO TEST INPUT ===");
    }

    // This callback is OPTIONAL now - we're reading directly
    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();

        if (showDebugLogs)
        {
            Debug.Log($"🎮 [ActiveRagdollMovement] OnMove CALLBACK FIRED! Input: {movementInput}");
        }
    }

    private void Update()
    {
        logFrameCounter++;

        if (!isInitialized)
        {
            if (showFrameByFrameLogs)
            {
                Debug.LogWarning("[ActiveRagdollMovement] Update() called but not initialized!");
            }
            return;
        }

        if (balancer == null)
        {
            if (showFrameByFrameLogs && logFrameCounter % 60 == 0)
            {
                Debug.LogWarning("[ActiveRagdollMovement] Balancer is NULL!");
            }
            return;
        }

        // If camera is missing, try to find it
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // NEW: Read input value DIRECTLY from the action every frame
        if (moveAction != null && moveAction.enabled)
        {
            movementInput = moveAction.ReadValue<Vector2>();
        }

        // Log every 60 frames to show current input state
        if (showFrameByFrameLogs && logFrameCounter % 60 == 0)
        {
            Debug.Log($"[ActiveRagdollMovement] Update tick - Input: {movementInput}, Balancer: {(balancer != null ? "OK" : "NULL")}");
        }

        // Convert input to world-space movement
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;

        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 targetVel = (right * movementInput.x + forward * movementInput.y) * moveSpeed;

        // Log whenever there's input
        if (movementInput.magnitude > 0.01f && showDebugLogs)
        {
            Debug.Log($"📍 [ActiveRagdollMovement] Processing input: {movementInput}");
            Debug.Log($"   Camera Forward: {forward}, Right: {right}");
            Debug.Log($"   Target Velocity: {targetVel} (magnitude: {targetVel.magnitude})");
            Debug.Log($"   Sending to balancer NOW...");
        }

        balancer.SetTargetVelocity(targetVel);
    }

    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
        if (showDebugLogs)
        {
            Debug.Log($"[ActiveRagdollMovement] Camera set to: {camera.name}");
        }
    }

    private void OnEnable()
    {
        Debug.Log($"[ActiveRagdollMovement] ✓ Component ENABLED on {gameObject.name}");

        // Double-check PlayerInput when enabled
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput != null && playerInput.actions != null)
        {
            Debug.Log($"[ActiveRagdollMovement] PlayerInput state on enable:");
            Debug.Log($"  - Action Map: {playerInput.currentActionMap?.name ?? "NULL"}");
            Debug.Log($"  - Input Active: {playerInput.inputIsActive}");

            // Re-get move action reference
            if (moveAction == null)
            {
                moveAction = playerInput.actions.FindAction("Move");
                Debug.Log($"  - Move Action: {(moveAction != null ? "Found" : "NULL")}");
            }
        }
    }

    private void OnDisable()
    {
        Debug.Log($"[ActiveRagdollMovement] Component DISABLED on {gameObject.name}");
    }
}