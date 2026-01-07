using UnityEngine;

/// <summary>
/// Controls procedural walking animation for both legs
/// Alternates steps and syncs with movement speed
/// Makes hips follow root position while legs handle IK
/// </summary>
public class ProceduralLegController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PhysicsVoxelCharacter character;
    [SerializeField] private Transform hips;
    [SerializeField] private Transform root; // NEW: The main character root

    [Header("IK Controllers")]
    [SerializeField] private FootIKController leftFootIK;
    [SerializeField] private FootIKController rightFootIK;

    [Header("Walking Parameters")]
    [SerializeField] private float strideLength = 1f;
    [SerializeField] private float stepTriggerDistance = 0.6f;
    [SerializeField] private float hipSway = 0.05f;
    [SerializeField] private float hipSwaySpeed = 5f;
    [SerializeField] private float hipHeightAboveGround = 1.5f; // NEW: How high hips should be above feet

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 2f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private Vector3 lastRootPosition;
    private bool leftFootStepping = false;
    private bool rightFootStepping = false;
    private float swayTime = 0f;
    private bool isInitialized = false;

    public Vector3 Velocity { get; private set; }
    public float Speed => Velocity.magnitude;

    private void Start()
    {
        if (character == null)
            character = GetComponent<PhysicsVoxelCharacter>();

        if (hips == null)
            hips = character.hips;

        // Root is the main GameObject with Rigidbody
        root = transform;

        if (hips == null)
        {
            Debug.LogError("[ProceduralLegController] Hips not found!");
            return;
        }

        // Wait a frame for IK controllers to initialize
        Invoke(nameof(InitializeIKControllers), 0.1f);

        lastRootPosition = root.position;

        Debug.Log("✓ Procedural leg controller started");
    }

    private void InitializeIKControllers()
    {
        // Auto-setup IK controllers
        if (leftFootIK == null)
        {
            GameObject leftIK = new GameObject("LeftFootIK");
            leftIK.transform.SetParent(transform);
            leftFootIK = leftIK.AddComponent<FootIKController>();
            leftFootIK.Initialize(character.leftUpperLeg, character.leftLowerLeg, character.leftFoot);
        }

        if (rightFootIK == null)
        {
            GameObject rightIK = new GameObject("RightFootIK");
            rightIK.transform.SetParent(transform);
            rightFootIK = rightIK.AddComponent<FootIKController>();
            rightFootIK.Initialize(character.rightUpperLeg, character.rightLowerLeg, character.rightFoot);
        }

        isInitialized = true;
        Debug.Log("✓ IK controllers initialized");
    }

    private void Update()
    {
        if (!isInitialized || hips == null) return;

        CalculateVelocity();
        PositionHips(); // NEW: Position hips based on root + feet
        UpdateLegs();
        AnimateHips();
    }

    private void CalculateVelocity()
    {
        Vector3 currentPos = root.position;
        Velocity = (currentPos - lastRootPosition) / Time.deltaTime;
        lastRootPosition = currentPos;
    }

    /// <summary>
    /// NEW: Position hips at proper height above ground/feet
    /// </summary>
    private void PositionHips()
    {
        // Get average foot position
        Vector3 leftFootPos = leftFootIK != null ? leftFootIK.FootTarget : root.position;
        Vector3 rightFootPos = rightFootIK != null ? rightFootIK.FootTarget : root.position;
        Vector3 averageFootPos = (leftFootPos + rightFootPos) / 2f;

        // Calculate target hip position (above feet, following root XZ)
        Vector3 targetHipPos = new Vector3(
            root.position.x,
            averageFootPos.y + hipHeightAboveGround,
            root.position.z
        );

        // Smoothly move hips toward target
        hips.position = Vector3.Lerp(hips.position, targetHipPos, Time.deltaTime * 10f);
    }

    private void UpdateLegs()
    {
        if (Speed < 0.1f) return; // Not moving

        Vector3 movementDir = new Vector3(Velocity.x, 0, Velocity.z).normalized;

        // Calculate desired foot positions relative to hips
        Vector3 leftDesiredPos = hips.position + movementDir * strideLength * 0.5f + hips.right * -0.3f;
        Vector3 rightDesiredPos = hips.position + movementDir * strideLength * 0.5f + hips.right * 0.3f;

        // Raycast down to find actual ground
        leftDesiredPos = FindGroundPosition(leftDesiredPos);
        rightDesiredPos = FindGroundPosition(rightDesiredPos);

        // Check if either foot should step
        leftFootStepping = leftFootIK != null && leftFootIK.IsMoving;
        rightFootStepping = rightFootIK != null && rightFootIK.IsMoving;

        // Alternate steps - only one foot moves at a time
        if (!leftFootStepping && !rightFootStepping)
        {
            if (leftFootIK != null && leftFootIK.ShouldStep(leftDesiredPos))
            {
                leftFootIK.Step(leftDesiredPos);
            }
            else if (rightFootIK != null && rightFootIK.ShouldStep(rightDesiredPos))
            {
                rightFootIK.Step(rightDesiredPos);
            }
        }
    }

    /// <summary>
    /// Find ground position beneath a target point
    /// </summary>
    private Vector3 FindGroundPosition(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 2f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            return hit.point;
        }

        // No ground found, return position at root's Y level
        return new Vector3(position.x, root.position.y, position.z);
    }

    private void AnimateHips()
    {
        if (hips == null) return;

        if (Speed > 0.1f)
        {
            // Sway hips side-to-side during walking
            swayTime += Time.deltaTime * hipSwaySpeed;
            float sway = Mathf.Sin(swayTime) * hipSway;

            // Apply sway as local offset
            Vector3 swayOffset = hips.right * sway;
            hips.position += swayOffset * Time.deltaTime;
        }
    }

    public void SetStrideLength(float length)
    {
        strideLength = length;
    }

    public void SetHipHeight(float height)
    {
        hipHeightAboveGround = height;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isInitialized) return;

        // Draw hip target position
        if (hips != null && root != null)
        {
            Vector3 leftFootPos = leftFootIK != null ? leftFootIK.FootTarget : root.position;
            Vector3 rightFootPos = rightFootIK != null ? rightFootIK.FootTarget : root.position;
            Vector3 averageFootPos = (leftFootPos + rightFootPos) / 2f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(averageFootPos, averageFootPos + Vector3.up * hipHeightAboveGround);
            Gizmos.DrawWireSphere(averageFootPos + Vector3.up * hipHeightAboveGround, 0.1f);
        }
    }
}