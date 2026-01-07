using UnityEngine;

/// <summary>
/// Simple AI controller for NPCs - handles idle and wandering behavior
/// </summary>
public class NPCAIController : MonoBehaviour
{
    [Header("AI Behavior")]
    [SerializeField] private AIState currentState = AIState.Idle;
    [SerializeField] private float idleTimeMin = 2f;
    [SerializeField] private float idleTimeMax = 5f;
    [SerializeField] private float walkTimeMin = 3f;
    [SerializeField] private float walkTimeMax = 7f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float wanderRadius = 10f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private enum AIState
    {
        Idle,
        Walking
    }

    private Rigidbody rb;
    private CubeSkeletonAnimator animator;
    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private float stateTimer;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<CubeSkeletonAnimator>();
        spawnPosition = transform.position;
    }

    void Start()
    {
        ChangeState(AIState.Idle);
    }

    void Update()
    {
        CheckGrounded();

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Switch states
            if (currentState == AIState.Idle)
            {
                ChangeState(AIState.Walking);
            }
            else
            {
                ChangeState(AIState.Idle);
            }
        }
    }

    void FixedUpdate()
    {
        if (currentState == AIState.Walking && isGrounded)
        {
            MoveTowardsTarget();
        }
    }

    private void ChangeState(AIState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case AIState.Idle:
                stateTimer = Random.Range(idleTimeMin, idleTimeMax);
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                }
                break;

            case AIState.Walking:
                stateTimer = Random.Range(walkTimeMin, walkTimeMax);
                PickNewWanderTarget();
                break;
        }
    }

    private void PickNewWanderTarget()
    {
        // Pick random point within wander radius of spawn
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        targetPosition = spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Make sure target is at ground level
        targetPosition.y = transform.position.y;
    }

    private void MoveTowardsTarget()
    {
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0; // Keep movement horizontal

        // Check if we've reached the target
        if (direction.magnitude < 0.5f)
        {
            ChangeState(AIState.Idle);
            return;
        }

        direction.Normalize();

        // Rotate towards target
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        // Move forward
        Vector3 movement = direction * walkSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    private void CheckGrounded()
    {
        // Simple ground check
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance + 0.1f, groundLayer);

        if (animator != null)
        {
            animator.SetGrounded(isGrounded);
        }
    }

    public bool IsMoving()
    {
        return currentState == AIState.Walking && rb != null && rb.linearVelocity.magnitude > 0.1f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw wander radius
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);

        // Draw target position
        if (Application.isPlaying && currentState == AIState.Walking)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
#endif
}