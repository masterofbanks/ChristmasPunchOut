using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller that follows a target with orthographic view
/// Supports orbital rotation around target with Q/E keys
/// No position interpolation to maintain orthographic illusion
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Camera Mode")]
    [SerializeField] private bool useOrthographic = true;
    [SerializeField] private float orthographicSize = 10f;

    [Header("Orbital Rotation")]
    [SerializeField] private bool enableOrbitalRotation = true;
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float orbitDistance = 15f;
    [SerializeField] private float orbitHeight = 10f;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 1.5f, 0);

    [Header("Input")]
    [SerializeField] private bool useNewInputSystem = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showGizmos = true;

    private Camera cam;
    private float currentOrbitAngle = 0f;
    private float targetOrbitAngle = 0f;
    private bool isInitialized = false;
    private PlayerInput playerInput;
    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }

        // Set camera mode
        if (useOrthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }
    }

    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        SetupInput();
    }

    void OnDisable()
    {
        CleanupInput();
    }

    void LateUpdate()
    {
        if (!isInitialized || target == null)
        {
            if (autoFindPlayer)
            {
                TryFindTarget();
            }
            return;
        }

        // Handle input for rotation (if not using new input system events)
        if (!useNewInputSystem)
        {
            HandleLegacyInput();
        }

        // Smoothly interpolate orbit angle ONLY
        if (Mathf.Abs(targetOrbitAngle - currentOrbitAngle) > 0.01f)
        {
            currentOrbitAngle = Mathf.LerpAngle(currentOrbitAngle, targetOrbitAngle, rotationSpeed * Time.deltaTime);
        }
        else
        {
            currentOrbitAngle = targetOrbitAngle; // Snap when close
        }

        // Update camera position - NO INTERPOLATION for position
        UpdateCameraPosition();

        // Update camera rotation to look at target
        UpdateCameraRotation();

        if (showDebugInfo)
        {
            LogDebugInfo();
        }
    }

    private void Initialize()
    {
        if (target == null && autoFindPlayer)
        {
            TryFindTarget();
        }

        if (target != null)
        {
            // Initialize camera position to target + offset
            transform.position = GetTargetPosition();
            isInitialized = true;

            if (showDebugInfo)
            {
                Debug.Log("[CameraFollow] Initialized with target: " + target.name);
            }
        }
    }

    private void SetupInput()
    {
        if (!enableOrbitalRotation) return;

        if (useNewInputSystem)
        {
            // Try to find PlayerInput on target or in scene
            if (target != null)
            {
                playerInput = target.GetComponent<PlayerInput>();
            }

            if (playerInput == null)
            {
                playerInput = FindObjectOfType<PlayerInput>();
            }

            if (playerInput != null && playerInput.actions != null)
            {
                rotateLeftAction = playerInput.actions.FindAction("RotateCameraLeft");
                rotateRightAction = playerInput.actions.FindAction("RotateCameraRight");

                if (rotateLeftAction != null)
                {
                    rotateLeftAction.performed += OnRotateLeft;
                    if (showDebugInfo) Debug.Log("[CameraFollow] ✓ Bound RotateCameraLeft action");
                }
                else
                {
                    Debug.LogWarning("[CameraFollow] ⚠ 'RotateCameraLeft' action not found! Add it to your Input Actions.");
                }

                if (rotateRightAction != null)
                {
                    rotateRightAction.performed += OnRotateRight;
                    if (showDebugInfo) Debug.Log("[CameraFollow] ✓ Bound RotateCameraRight action");
                }
                else
                {
                    Debug.LogWarning("[CameraFollow] ⚠ 'RotateCameraRight' action not found! Add it to your Input Actions.");
                }
            }
            else
            {
                Debug.LogWarning("[CameraFollow] ⚠ PlayerInput not found! Falling back to legacy input (Q/E will still work).");
                useNewInputSystem = false; // Fallback to legacy
            }
        }
    }

    private void CleanupInput()
    {
        if (rotateLeftAction != null)
        {
            rotateLeftAction.performed -= OnRotateLeft;
        }

        if (rotateRightAction != null)
        {
            rotateRightAction.performed -= OnRotateRight;
        }
    }

    private void OnRotateLeft(InputAction.CallbackContext context)
    {
        RotateCamera(-rotationAngle);
        if (showDebugInfo) Debug.Log("[CameraFollow] ◀ Rotate LEFT triggered");
    }

    private void OnRotateRight(InputAction.CallbackContext context)
    {
        RotateCamera(rotationAngle);
        if (showDebugInfo) Debug.Log("[CameraFollow] ▶ Rotate RIGHT triggered");
    }

    private void HandleLegacyInput()
    {
        if (!enableOrbitalRotation) return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotateCamera(-rotationAngle);
            if (showDebugInfo) Debug.Log("[CameraFollow] Q pressed - Rotate LEFT");
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotateCamera(rotationAngle);
            if (showDebugInfo) Debug.Log("[CameraFollow] E pressed - Rotate RIGHT");
        }
    }

    /// <summary>
    /// Rotates the camera around the target by the specified angle
    /// </summary>
    public void RotateCamera(float angleDelta)
    {
        targetOrbitAngle += angleDelta;

        // Normalize angle to 0-360 range
        while (targetOrbitAngle >= 360f) targetOrbitAngle -= 360f;
        while (targetOrbitAngle < 0f) targetOrbitAngle += 360f;

        if (showDebugInfo)
        {
            Debug.Log($"[CameraFollow] 🔄 Rotating by {angleDelta}° → Target angle: {targetOrbitAngle}°");
        }
    }

    private void TryFindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            var skeletonChar = FindObjectOfType<CubeSkeletonCharacter>();
            if (skeletonChar != null)
            {
                player = skeletonChar.gameObject;
            }
        }

        if (player != null)
        {
            SetTarget(player.transform);
        }
    }

    private void UpdateCameraPosition()
    {
        // NO INTERPOLATION - Direct follow for orthographic illusion
        transform.position = GetTargetPosition();
    }

    private void UpdateCameraRotation()
    {
        if (target == null) return;

        Vector3 lookTarget = target.position + lookAtOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);

        // Smooth rotation for camera angle changes
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private Vector3 GetTargetPosition()
    {
        if (target == null) return transform.position;

        if (useOrthographic && enableOrbitalRotation)
        {
            // Calculate orbital position around target
            float angleRad = currentOrbitAngle * Mathf.Deg2Rad;

            Vector3 orbitOffset = new Vector3(
                Mathf.Sin(angleRad) * orbitDistance,
                orbitHeight,
                Mathf.Cos(angleRad) * orbitDistance
            );

            return target.position + orbitOffset;
        }
        else
        {
            // Fallback: fixed offset
            return target.position + new Vector3(0, orbitHeight, -orbitDistance);
        }
    }

    /// <summary>
    /// Sets the target for the camera to follow
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[CameraFollow] ✓ Target set to: {target.name}");
            }

            isInitialized = true;

            // Try to setup input if we have a new target
            if (useNewInputSystem)
            {
                CleanupInput();
                SetupInput();
            }

            // Snap to target position immediately
            transform.position = GetTargetPosition();

            // Set rotation immediately
            Vector3 lookTarget = target.position + lookAtOffset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }

    /// <summary>
    /// Immediately snaps camera to target position (no smoothing)
    /// </summary>
    public void SnapToTarget()
    {
        if (target != null)
        {
            transform.position = GetTargetPosition();

            Vector3 lookTarget = target.position + lookAtOffset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }

    /// <summary>
    /// Sets the camera orbit angle directly (0-360 degrees)
    /// </summary>
    public void SetOrbitAngle(float angle)
    {
        targetOrbitAngle = angle;
        currentOrbitAngle = angle;
    }

    /// <summary>
    /// Gets the current orbit angle
    /// </summary>
    public float GetOrbitAngle()
    {
        return currentOrbitAngle;
    }

    public Transform GetTarget()
    {
        return target;
    }

    private void LogDebugInfo()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);
        Debug.Log($"[CameraFollow] Angle: {currentOrbitAngle:F1}° (Target: {targetOrbitAngle:F1}°), Distance: {distance:F2}");
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || target == null) return;

        // Draw orbit path
        if (enableOrbitalRotation && useOrthographic)
        {
            Gizmos.color = Color.cyan;
            DrawWireCircle(target.position + Vector3.up * orbitHeight, orbitDistance, Vector3.up);

            // Draw current camera angle indicator
            float angleRad = currentOrbitAngle * Mathf.Deg2Rad;
            Vector3 anglePos = target.position + new Vector3(
                Mathf.Sin(angleRad) * orbitDistance,
                orbitHeight,
                Mathf.Cos(angleRad) * orbitDistance
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position + Vector3.up * orbitHeight, anglePos);
            Gizmos.DrawWireSphere(anglePos, 0.5f);

            // Draw target angle indicator (where camera is moving to)
            if (Mathf.Abs(targetOrbitAngle - currentOrbitAngle) > 1f)
            {
                float targetAngleRad = targetOrbitAngle * Mathf.Deg2Rad;
                Vector3 targetAnglePos = target.position + new Vector3(
                    Mathf.Sin(targetAngleRad) * orbitDistance,
                    orbitHeight,
                    Mathf.Cos(targetAngleRad) * orbitDistance
                );

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(targetAnglePos, 0.3f);
            }

            // Draw cardinal directions
            Gizmos.color = Color.red;
            Vector3 north = target.position + Vector3.forward * orbitDistance * 0.5f + Vector3.up * orbitHeight;
            Gizmos.DrawLine(target.position + Vector3.up * orbitHeight, north);

            Gizmos.color = Color.blue;
            Vector3 east = target.position + Vector3.right * orbitDistance * 0.5f + Vector3.up * orbitHeight;
            Gizmos.DrawLine(target.position + Vector3.up * orbitHeight, east);
        }

        // Draw look-at line
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, target.position + lookAtOffset);
    }

    private void DrawWireCircle(Vector3 center, float radius, Vector3 normal, int segments = 32)
    {
        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        Vector3 right = Vector3.Cross(normal, forward).normalized * radius;
        forward = Vector3.Cross(right, normal).normalized * radius;

        Vector3 lastPoint = center + right;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 nextPoint = center + right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}