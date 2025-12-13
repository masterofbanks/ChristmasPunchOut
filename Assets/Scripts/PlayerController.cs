using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Camera playerCamera;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private Vector2 movementInput;
    private Rigidbody2D rb;


    private void Start()
    {
        playerCamera = Camera.main;
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        MovePlayer();
    }

    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();
    }

    private void MovePlayer()
    {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (right * movementInput.x + forward * movementInput.y).normalized;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }
}
