using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movement input for active ragdoll
/// Uses camera-relative movement controls
/// FIXED: Finds PlayerInput on root GameObject, not local
/// </summary>
public class ActiveRagdollMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false; // Turn off by default
    [SerializeField] private bool showFrameByFrameLogs = false;

    private ActiveRagdollBalancer balancer;
    private Camera mainCamera;
    private Vector2 movementInput;
    private bool isInitialized = false;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private int logFrameCounter = 0;

    private void Awake()
    {
        if (showDebugLogs) Debug.Log("=== [ActiveRagdollMovement] AWAKE ===");

        // CRITICAL FIX: PlayerInput is on ROOT, not on this GameObject (hips)
        // Search up the hierarchy to find it
        playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput == null)
        {
            // If not in parent, try to find it anywhere in scene
            playerInput = FindObjectOfType<PlayerInput>();
        }

        if (playerInput == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ PlayerInput component NOT FOUND anywhere!");
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ActiveRagdollMovement] ✓ PlayerInput found on: {playerInput.gameObject.name}");
                Debug.Log($"  - Actions: {(playerInput.actions != null ? playerInput.actions.name : "NULL")}");
            }

            if (playerInput.actions != null)
            {
                moveAction = playerInput.actions.FindAction("Move");
                if (moveAction != null)
                {
                    if (showDebugLogs)
                    {
                        Debug.Log($"  - Move Action Found: {moveAction.name}");
                        Debug.Log($"    - Enabled: {moveAction.enabled}");
                    }
                }
                else
                {
                    Debug.LogError("[ActiveRagdollMovement] ❌ 'Move' action NOT FOUND!");
                }
            }
        }
    }

    private void Start()
    {
        if (showDebugLogs) Debug.Log("=== [ActiveRagdollMovement] START ===");

        balancer = GetComponent<ActiveRagdollBalancer>();
        mainCamera = Camera.main;

        if (balancer == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ ActiveRagdollBalancer not found on " + gameObject.name);
        }

        if (mainCamera == null)
        {
            Debug.LogError("[ActiveRagdollMovement] ❌ No main camera found!");
        }

        isInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log("[ActiveRagdollMovement] ✓ Initialized and ready for input");
        }
    }

    private void Update()
    {
        logFrameCounter++;

        if (!isInitialized) return;
        if (balancer == null) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Read input value DIRECTLY from the action every frame
        if (moveAction != null && moveAction.enabled)
        {
            movementInput = moveAction.ReadValue<Vector2>();
        }

        // Convert input to world-space movement
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;

        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 targetVel = (right * movementInput.x + forward * movementInput.y) * moveSpeed;

        balancer.SetTargetVelocity(targetVel);
    }

    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
    }

    private void OnEnable()
    {
        // Re-find PlayerInput when enabled
        if (playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = FindObjectOfType<PlayerInput>();
            }
        }

        if (playerInput != null && playerInput.actions != null && moveAction == null)
        {
            moveAction = playerInput.actions.FindAction("Move");
        }
    }
}