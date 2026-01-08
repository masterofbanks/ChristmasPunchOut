using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movement controller using virtual muscle system
/// Drives movement by adjusting target poses
/// </summary>
public class RagdollMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActiveRagdollV2 ragdoll;
    [SerializeField] private BalanceController balanceController;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float leanAmount = 15f;

    [Header("Step Settings")]
    [SerializeField] private float stepHeight = 0.3f;
    [SerializeField] private float stepLength = 0.8f;
    [SerializeField] private float stepDuration = 0.5f;

    private Vector2 moveInput;
    private Camera mainCamera;
    private Rigidbody hipsRb;

    // Step state
    private bool isSteppingLeft = false;
    private bool isSteppingRight = false;
    private float stepTimer = 0f;

    private void Start()
    {
        if (ragdoll == null) ragdoll = GetComponent<ActiveRagdollV2>();
        if (balanceController == null) balanceController = GetComponent<BalanceController>();

        mainCamera = Camera.main;

        if (ragdoll.hips != null)
        {
            hipsRb = ragdoll.hips.GetComponent<Rigidbody>();
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void FixedUpdate()
    {
        if (hipsRb == null || moveInput.magnitude < 0.1f) return;

        HandleMovement();
        HandleStepping();
    }

    private void HandleMovement()
    {
        // Get camera-relative direction
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            // Apply force to hips
            Vector3 force = moveDir * walkSpeed * hipsRb.mass;
            hipsRb.AddForce(force, ForceMode.Force);

            // Lean into movement
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            Quaternion leanRotation = Quaternion.Euler(leanAmount, 0, 0);
            ragdoll.hips.SetTargetRotation(targetRotation * leanRotation);
        }
    }

    private void HandleStepping()
    {
        // Simplified stepping - alternate feet
        if (!isSteppingLeft && !isSteppingRight)
        {
            // Start new step
            if (moveInput.magnitude > 0.1f)
            {
                isSteppingLeft = true;
                stepTimer = 0f;
            }
        }
        else
        {
            stepTimer += Time.fixedDeltaTime;

            if (stepTimer >= stepDuration)
            {
                // Switch feet
                isSteppingLeft = !isSteppingLeft;
                isSteppingRight = !isSteppingRight;
                stepTimer = 0f;
            }
        }
    }
}