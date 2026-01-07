using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple 3D character movement controller for cube skeleton
/// Designed to work with runtime-instantiated characters
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SimpleCharacterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Sprint Settings")]
    [SerializeField] private bool enableSprinting = true;
    [SerializeField] private float sprintSpeedMultiplier = 1.75f;
    [SerializeField] private bool preventSprintWhileCrouching = true;
    [SerializeField] private bool preventSprintWhileAiming = true;

    [Header("Aiming Mode")]
    [SerializeField] private bool enableAimingMode = true;
    [SerializeField] private float aimRotationSpeed = 180f;
    [SerializeField] private float aimingSensitivity = 1f;
    [SerializeField] private bool invertAimDirection = false;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField] private bool preventJumpWhileCrouching = true;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    [SerializeField] private float jumpCooldown = 0.2f;
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private float groundCheckOffset = 0.05f;
    [SerializeField] private int groundCheckSamples = 4;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showInputDebug = false;
    [SerializeField] private bool showGroundCheck = false;

    private Rigidbody rb;
    private Camera mainCamera;
    private Vector2 movementInput;
    private bool isGrounded;
    private bool isCrouching;
    private bool isAiming;
    private bool isSprinting; // NEW: Sprint state
    private float lastGroundedTime;
    private int groundedFrameCount;
    private bool isInitialized = false;
    private float debugTimer = 0f;
    private PlayerInput playerInput;
    private bool isSubscribedToInput = false;
    private CubeSkeletonAnimator animator;
    private float lastJumpTime;
    private CapsuleCollider capsuleCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        if (!isSubscribedToInput)
        {
            TrySubscribeToInput();
        }
    }

    public void Initialize()
    {
        if (isInitialized) return;

        if (showDebugLogs) Debug.Log($"[{gameObject.name}] === INITIALIZING MOVEMENT ===");

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError($"[{gameObject.name}] ❌ No main camera found!");
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        animator = GetComponent<CubeSkeletonAnimator>();
        TrySubscribeToInput();

        isInitialized = true;
        lastJumpTime = -jumpCooldown;
        lastGroundedTime = 0f;
        groundedFrameCount = 0;
        isCrouching = false;
        isAiming = false;
        isSprinting = false; // NEW

        if (showDebugLogs) Debug.Log($"[{gameObject.name}] === INITIALIZATION COMPLETE ===");
    }

    private void TrySubscribeToInput()
    {
        if (isSubscribedToInput) return;

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput != null && playerInput.actions != null)
        {
            var moveAction = playerInput.actions.FindAction("Move");
            var jumpAction = playerInput.actions.FindAction("Jump");
            var crouchAction = playerInput.actions.FindAction("Crouch");
            var attackAction = playerInput.actions.FindAction("Attack");
            var aimAction = playerInput.actions.FindAction("Aim");
            var sprintAction = playerInput.actions.FindAction("Sprint"); // NEW

            if (moveAction != null)
            {
                moveAction.performed += OnMovePerformed;
                moveAction.canceled += OnMoveCanceled;
                moveAction.started += OnMoveStarted;
            }

            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
            }

            if (crouchAction != null)
            {
                crouchAction.started += OnCrouchStarted;
                crouchAction.canceled += OnCrouchCanceled;
            }

            if (attackAction != null)
            {
                attackAction.performed += OnAttackPerformed;
            }

            if (aimAction != null)
            {
                aimAction.started += OnAimStarted;
                aimAction.canceled += OnAimCanceled;
                if (showDebugLogs) Debug.Log($"[{gameObject.name}] ✓ Subscribed to Aim action!");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] ⚠ No 'Aim' action found! Add it to Input Actions (Right Mouse Button).");
            }

            // NEW: Sprint action subscription
            if (sprintAction != null)
            {
                sprintAction.started += OnSprintStarted;
                sprintAction.canceled += OnSprintCanceled;
                if (showDebugLogs) Debug.Log($"[{gameObject.name}] ✓ Subscribed to Sprint action!");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] ⚠ No 'Sprint' action found! Add it to Input Actions (Left Shift).");
            }

            isSubscribedToInput = true;
        }
    }

    void Update()
    {
        if (!isSubscribedToInput)
        {
            TrySubscribeToInput();
        }

        // Legacy input fallbacks
        if (Input.GetMouseButtonDown(0))
        {
            TriggerAttack();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            TriggerDeath();
        }

        // Legacy aim input (right mouse button)
        if (enableAimingMode && Input.GetMouseButtonDown(1))
        {
            StartAiming();
        }
        else if (Input.GetMouseButtonUp(1))
        {
            StopAiming();
        }

        // NEW: Legacy sprint input (Left Shift)
        if (enableSprinting && Input.GetKeyDown(KeyCode.LeftShift))
        {
            StartSprinting();
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            StopSprinting();
        }

        // Handle aiming rotation
        if (isAiming && enableAimingMode)
        {
            HandleAimRotation();
        }
    }

    void OnDestroy()
    {
        if (isSubscribedToInput && playerInput != null && playerInput.actions != null)
        {
            var moveAction = playerInput.actions.FindAction("Move");
            var jumpAction = playerInput.actions.FindAction("Jump");
            var crouchAction = playerInput.actions.FindAction("Crouch");
            var attackAction = playerInput.actions.FindAction("Attack");
            var aimAction = playerInput.actions.FindAction("Aim");
            var sprintAction = playerInput.actions.FindAction("Sprint"); // NEW

            if (moveAction != null)
            {
                moveAction.performed -= OnMovePerformed;
                moveAction.canceled -= OnMoveCanceled;
                moveAction.started -= OnMoveStarted;
            }

            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
            }

            if (crouchAction != null)
            {
                crouchAction.started -= OnCrouchStarted;
                crouchAction.canceled -= OnCrouchCanceled;
            }

            if (attackAction != null)
            {
                attackAction.performed -= OnAttackPerformed;
            }

            if (aimAction != null)
            {
                aimAction.started -= OnAimStarted;
                aimAction.canceled -= OnAimCanceled;
            }

            // NEW: Unsubscribe sprint
            if (sprintAction != null)
            {
                sprintAction.started -= OnSprintStarted;
                sprintAction.canceled -= OnSprintCanceled;
            }
        }
    }

    private void OnMoveStarted(InputAction.CallbackContext context)
    {
        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] ✓ Move action STARTED!");
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        movementInput = Vector2.zero;
    }

    private void OnCrouchStarted(InputAction.CallbackContext context)
    {
        isCrouching = true;

        // NEW: Cancel sprint when crouching
        if (preventSprintWhileCrouching && isSprinting)
        {
            StopSprinting();
        }

        if (animator != null)
        {
            animator.SetCrouching(true);
        }

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] ✓ CROUCH STARTED");
        }
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        isCrouching = false;
        if (animator != null)
        {
            animator.SetCrouching(false);
        }

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] ✓ CROUCH CANCELED");
        }
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        StartAiming();
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        StopAiming();
    }

    // NEW: Sprint input handlers
    private void OnSprintStarted(InputAction.CallbackContext context)
    {
        StartSprinting();
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        StopSprinting();
    }

    private void StartSprinting()
    {
        if (!enableSprinting) return;

        // Check restrictions
        if (isCrouching && preventSprintWhileCrouching)
        {
            if (showInputDebug)
            {
                Debug.Log($"[{gameObject.name}] ❌ Cannot sprint while crouching");
            }
            return;
        }

        if (isAiming && preventSprintWhileAiming)
        {
            if (showInputDebug)
            {
                Debug.Log($"[{gameObject.name}] ❌ Cannot sprint while aiming");
            }
            return;
        }

        isSprinting = true;

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] 🏃 SPRINT STARTED");
        }
    }

    private void StopSprinting()
    {
        isSprinting = false;

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] 🚶 SPRINT STOPPED");
        }
    }

    private void StartAiming()
    {
        if (!enableAimingMode) return;

        isAiming = true;

        // NEW: Cancel sprint when aiming
        if (preventSprintWhileAiming && isSprinting)
        {
            StopSprinting();
        }

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] 🎯 AIMING MODE STARTED");
        }
    }

    private void StopAiming()
    {
        isAiming = false;

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] 🎯 AIMING MODE STOPPED");
        }
    }

    private void HandleAimRotation()
    {
        float horizontalInput = movementInput.x;

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            float rotationDirection = invertAimDirection ? -horizontalInput : horizontalInput;
            float rotationAmount = rotationDirection * aimRotationSpeed * aimingSensitivity * Time.deltaTime;

            Quaternion targetRotation = rb.rotation * Quaternion.Euler(0, rotationAmount, 0);
            rb.rotation = targetRotation;

            if (showInputDebug && debugTimer <= 0f)
            {
                Debug.Log($"[Aiming] Rotating: {rotationAmount:F1}° | Input: {horizontalInput:F2}");
                debugTimer = 0.5f;
            }
        }
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        TriggerAttack();
    }

    private void TriggerAttack()
    {
        if (animator != null)
        {
            animator.TriggerAttack();
        }

        PlayerAttackHandler attackHandler = GetComponent<PlayerAttackHandler>();
        if (attackHandler != null)
        {
            attackHandler.TriggerAttack();
        }
    }

    private void TriggerDeath()
    {
        if (animator != null)
        {
            Debug.Log($"[{gameObject.name}] 💀 DEATH TRIGGERED!");
            animator.TriggerDeath(transform.position);
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] 🎮 JUMP INPUT RECEIVED!");
        }
        TryJump();
    }

    private void TryJump()
    {
        if (isCrouching && preventJumpWhileCrouching)
        {
            if (showInputDebug)
            {
                Debug.Log($"[{gameObject.name}] ❌ Cannot jump while crouching");
            }
            return;
        }

        float timeSinceGrounded = Time.time - lastGroundedTime;

        if (Time.time - lastJumpTime < jumpCooldown)
        {
            if (showInputDebug)
            {
                Debug.Log($"[{gameObject.name}] ❌ Jump on cooldown");
            }
            return;
        }

        if ((!isGrounded || groundedFrameCount < 2) && timeSinceGrounded > coyoteTime)
        {
            if (showInputDebug)
            {
                Debug.Log($"[{gameObject.name}] ❌ Cannot jump: grounded={isGrounded}, frames={groundedFrameCount}");
            }
            return;
        }

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        lastJumpTime = Time.time;
        groundedFrameCount = 0;

        if (animator != null)
        {
            animator.TriggerJump();
        }

        if (showInputDebug)
        {
            Debug.Log($"[{gameObject.name}] ✓✓✓ JUMPED! Force: {jumpForce}");
        }
    }

    void FixedUpdate()
    {
        if (!isInitialized) return;

        CheckGrounded();

        if (!isAiming)
        {
            MoveCharacter();
        }
        else
        {
            MoveCharacterWhileAiming();
        }

        if (showDebugLogs)
        {
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer > 2f)
            {
                Debug.Log($"[Movement] Grounded: {isGrounded}, Crouching: {isCrouching}, Aiming: {isAiming}, Sprinting: {isSprinting}, Velocity: {rb.linearVelocity}");
                debugTimer = 0f;
            }
        }
        else if (showInputDebug)
        {
            debugTimer -= Time.fixedDeltaTime;
        }
    }

    private void CheckGrounded()
    {
        if (capsuleCollider == null)
        {
            isGrounded = false;
            return;
        }

        float capsuleBottom = capsuleCollider.center.y - (capsuleCollider.height / 2f);
        Vector3 checkStart = transform.position + new Vector3(0, capsuleBottom + groundCheckRadius + groundCheckOffset, 0);

        bool anyHit = false;
        int hitCount = 0;

        if (Physics.SphereCast(checkStart, groundCheckRadius, Vector3.down, out RaycastHit centerHit, groundCheckDistance, groundLayer))
        {
            anyHit = true;
            hitCount++;
        }

        if (groundCheckSamples > 1)
        {
            float sampleOffset = groundCheckRadius * 0.5f;
            Vector3[] samplePositions = new Vector3[]
            {
                new Vector3(sampleOffset, 0, 0),
                new Vector3(-sampleOffset, 0, 0),
                new Vector3(0, 0, sampleOffset),
                new Vector3(0, 0, -sampleOffset)
            };

            for (int i = 0; i < Mathf.Min(groundCheckSamples - 1, samplePositions.Length); i++)
            {
                Vector3 sampleStart = checkStart + samplePositions[i];
                if (Physics.SphereCast(sampleStart, groundCheckRadius * 0.8f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
                {
                    anyHit = true;
                    hitCount++;
                }
            }
        }

        bool wasGrounded = isGrounded;
        isGrounded = anyHit;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            groundedFrameCount = Mathf.Min(groundedFrameCount + 1, 10);
        }
        else
        {
            groundedFrameCount = 0;
        }

        if (wasGrounded != isGrounded && showGroundCheck)
        {
            if (isGrounded)
            {
                Debug.Log($"[{gameObject.name}] ✓ GROUNDED (hits: {hitCount})");
            }
            else
            {
                Debug.Log($"[{gameObject.name}] ⚠ LEFT GROUND");
            }
        }

        if (animator != null)
        {
            animator.SetGrounded(isGrounded);
        }
    }

    private void MoveCharacter()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (movementInput.sqrMagnitude < 0.01f)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
            return;
        }

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = (cameraRight * movementInput.x + cameraForward * movementInput.y).normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            // NEW: Calculate speed with sprint multiplier
            float currentSpeed = CalculateCurrentSpeed();

            Vector3 targetVelocity = moveDirection * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void MoveCharacterWhileAiming()
    {
        if (mainCamera == null)
        {
            return;
        }

        if (movementInput.sqrMagnitude < 0.01f)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
            return;
        }

        float forwardInput = movementInput.y;

        if (Mathf.Abs(forwardInput) > 0.01f)
        {
            Vector3 moveDirection = transform.forward * forwardInput;

            // NEW: Calculate speed with sprint multiplier (even while aiming, if not prevented)
            float currentSpeed = CalculateCurrentSpeed();

            Vector3 targetVelocity = moveDirection * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;
        }
        else
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }
    }

    // NEW: Centralized speed calculation
    private float CalculateCurrentSpeed()
    {
        float currentSpeed = moveSpeed;

        // Apply crouch multiplier (highest priority - reduces speed)
        if (isCrouching)
        {
            currentSpeed *= crouchSpeedMultiplier;
        }
        // Apply sprint multiplier (only if not crouching)
        else if (isSprinting && enableSprinting)
        {
            // Check if sprinting is allowed in current state
            bool canSprint = true;

            if (preventSprintWhileAiming && isAiming)
            {
                canSprint = false;
            }

            if (canSprint)
            {
                currentSpeed *= sprintSpeedMultiplier;
            }
        }

        return currentSpeed;
    }

    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
    }

    public bool IsCrouching() => isCrouching;
    public bool IsAiming() => isAiming;
    public bool IsSprinting() => isSprinting; // NEW

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || capsuleCollider == null) return;

        float capsuleBottom = capsuleCollider.center.y - (capsuleCollider.height / 2f);
        Vector3 checkStart = transform.position + new Vector3(0, capsuleBottom + groundCheckRadius + groundCheckOffset, 0);
        Vector3 checkEnd = checkStart + Vector3.down * groundCheckDistance;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(checkStart, groundCheckRadius);
        Gizmos.DrawLine(checkStart, checkEnd);
        Gizmos.DrawWireSphere(checkEnd, groundCheckRadius);

        if (groundCheckSamples > 1)
        {
            Gizmos.color = isGrounded ? Color.green * 0.5f : Color.red * 0.5f;
            float sampleOffset = groundCheckRadius * 0.5f;
            Vector3[] samplePositions = new Vector3[]
            {
                checkStart + new Vector3(sampleOffset, 0, 0),
                checkStart + new Vector3(-sampleOffset, 0, 0),
                checkStart + new Vector3(0, 0, sampleOffset),
                checkStart + new Vector3(0, 0, -sampleOffset)
            };

            foreach (Vector3 pos in samplePositions)
            {
                Gizmos.DrawWireSphere(pos, groundCheckRadius * 0.8f);
                Gizmos.DrawLine(pos, pos + Vector3.down * groundCheckDistance);
            }
        }

        Gizmos.color = Color.cyan;
        Vector3 capsuleCenter = transform.position + capsuleCollider.center;
        float halfHeight = (capsuleCollider.height / 2f) - capsuleCollider.radius;
        Vector3 top = capsuleCenter + Vector3.up * halfHeight;
        Vector3 bottom = capsuleCenter + Vector3.down * halfHeight;
        Gizmos.DrawWireSphere(top, capsuleCollider.radius);
        Gizmos.DrawWireSphere(bottom, capsuleCollider.radius);

        // Draw aiming indicator
        if (isAiming)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 3f);
        }

        // NEW: Draw sprinting indicator
        if (isSprinting)
        {
            Gizmos.color = Color.magenta;
            Vector3 sprintIndicatorPos = transform.position + Vector3.up * 2f;
            Gizmos.DrawWireSphere(sprintIndicatorPos, 0.2f);
            Gizmos.DrawRay(sprintIndicatorPos, transform.forward * 2f);
        }
    }
}