using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple movement controller for physics voxel character testing
/// Applies forces to move the character
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PhysicsVoxelMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    private Rigidbody rb;
    private Camera mainCamera;
    private Vector2 movementInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
    }

    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();
    }

    private void FixedUpdate()
    {
        if (mainCamera == null || movementInput == Vector2.zero) return;

        // Get camera-relative movement direction
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = (cameraRight * movementInput.x + cameraForward * movementInput.y).normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            // Apply velocity
            Vector3 targetVelocity = moveDirection * moveSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;

            // Rotate to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // Stop horizontal movement
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }
}