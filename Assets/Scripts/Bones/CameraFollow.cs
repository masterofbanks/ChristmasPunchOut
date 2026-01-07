using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller that follows a target with orthographic view
/// Supports orbital rotation around target with Q/E keys
/// No position interpolation to maintain orthographic illusion
/// ENHANCED: Better target finding for ActiveRagdoll
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
    private float targetSearchTimer = 0f; // NEW
    private const float targetSearchInterval = 0.5f; // NEW: Search every 0.5s instead of every frame

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
        // NEW: If no target, try to find one periodically
        if (target == null && autoFindPlayer)
        {
            targetSearchTimer += Time.deltaTime;
            if (targetSearchTimer >= targetSearchInterval)
            {
                TryFindTarget();
                targetSearchTimer = 0f;
            }
            return; // Don't update camera if no target
        }

        if (!isInitialized) return;

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
            currentOrbitAngle = targetOrbitAngle;
        }

        // Update camera position - NO INTERPOLATION for position
        UpdateCameraPosition();

        // Update camera rotation to look at target
        UpdateCameraRotation();

        if (showDebugInfo && Time.frameCount % 60 == 0) // Log once per second
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
                Debug.Log("[CameraFollow] ✓ Initialized with target: " + target.name);
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

                if (rotateRightAction != null)
                {
                    rotateRightAction.performed += OnRotateRight;
                    if (showDebugInfo) Debug.Log("[CameraFollow] ✓ Bound RotateCameraRight action");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("[CameraFollow] PlayerInput not found - using legacy input");
                }
                useNewInputSystem = false;
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
        if (showDebugInfo) Debug.Log("[CameraFollow] ◀ Rotate LEFT");
    }

    private void OnRotateRight(InputAction.CallbackContext context)
    {
        RotateCamera(rotationAngle);
        if (showDebugInfo) Debug.Log("[CameraFollow] ▶ Rotate RIGHT");
    }

    private void HandleLegacyInput()
    {
        if (!enableOrbitalRotation) return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotateCamera(-rotationAngle);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotateCamera(rotationAngle);
        }
    }

    public void RotateCamera(float angleDelta)
    {
        targetOrbitAngle += angleDelta;

        while (targetOrbitAngle >= 360f) targetOrbitAngle -= 360f;
        while (targetOrbitAngle < 0f) targetOrbitAngle += 360f;
    }

    private void TryFindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        // NEW: Also try to find ActiveRagdollCharacter
        if (player == null)
        {
            var activeRagdoll = FindObjectOfType<ActiveRagdollCharacter>();
            if (activeRagdoll != null)
            {
                player = activeRagdoll.gameObject;
                Debug.Log("[CameraFollow] Found ActiveRagdollCharacter: " + player.name);
            }
        }

        if (player == null)
        {
            var skeletonChar = FindObjectOfType<CubeSkeletonCharacter>();
            if (skeletonChar != null)
            {
                player = skeletonChar.gameObject;
                Debug.Log("[CameraFollow] Found CubeSkeletonCharacter: " + player.name);
            }
        }

        if (player != null)
        {
            SetTarget(player.transform);
        }
    }

    private void UpdateCameraPosition()
    {
        // Direct follow - no interpolation
        transform.position = GetTargetPosition();
    }

    private void UpdateCameraRotation()
    {
        if (target == null) return;

        Vector3 lookTarget = target.position + lookAtOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);

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
            return target.position + new Vector3(0, orbitHeight, -orbitDistance);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            Debug.Log($"[CameraFollow] ✓ Target set to: {target.name}");

            isInitialized = true;

            if (useNewInputSystem)
            {
                CleanupInput();
                SetupInput();
            }

            // Snap to target immediately
            transform.position = GetTargetPosition();

            Vector3 lookTarget = target.position + lookAtOffset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }

    public void SnapToTarget()
    {
        if (target != null)
        {
            transform.position = GetTargetPosition();

            Vector3 lookTarget = target.position + lookAtOffset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }

    public void SetOrbitAngle(float angle)
    {
        targetOrbitAngle = angle;
        currentOrbitAngle = angle;
    }

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
        if (target == null)
        {
            Debug.Log("[CameraFollow] No target assigned");
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        Debug.Log($"[CameraFollow] Target: {target.name}, Angle: {currentOrbitAngle:F1}°, Distance: {distance:F2}");
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || target == null) return;

        if (enableOrbitalRotation && useOrthographic)
        {
            Gizmos.color = Color.cyan;
            DrawWireCircle(target.position + Vector3.up * orbitHeight, orbitDistance, Vector3.up);

            float angleRad = currentOrbitAngle * Mathf.Deg2Rad;
            Vector3 anglePos = target.position + new Vector3(
                Mathf.Sin(angleRad) * orbitDistance,
                orbitHeight,
                Mathf.Cos(angleRad) * orbitDistance
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position + Vector3.up * orbitHeight, anglePos);
            Gizmos.DrawWireSphere(anglePos, 0.5f);

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
        }

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