using UnityEngine;

/// <summary>
/// Two-bone IK solver for procedural foot placement
/// Ensures feet are planted on the ground and follow terrain
/// </summary>
public class FootIKController : MonoBehaviour
{
    [Header("IK Chain")]
    [SerializeField] private Transform upperLeg;
    [SerializeField] private Transform lowerLeg;
    [SerializeField] private Transform foot;

    [Header("IK Settings")]
    [SerializeField] private float stepHeight = 0.3f;
    [SerializeField] private float stepDistance = 0.8f;
    [SerializeField] private float stepSpeed = 8f;
    [SerializeField] private float footOffset = 0.1f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private Vector3 currentFootTarget;
    private Vector3 lastFootPosition;
    private bool isMoving = false;
    private float movementProgress = 0f;
    private bool isInitialized = false;

    public bool IsMoving => isMoving;
    public Vector3 FootTarget => currentFootTarget;

    private void Start()
    {
        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            InitializeFootPosition();
        }
        else
        {
            Debug.LogError($"[FootIKController] Missing bone references on {gameObject.name}!");
        }
    }

    public void Initialize(Transform upper, Transform lower, Transform footBone)
    {
        upperLeg = upper;
        lowerLeg = lower;
        foot = footBone;

        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            InitializeFootPosition();
        }
    }

    private void InitializeFootPosition()
    {
        // Find ground beneath foot at startup
        Vector3 rayStart = foot.position + Vector3.up * 2f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, groundLayer))
        {
            currentFootTarget = hit.point + Vector3.up * footOffset;
        }
        else
        {
            // No ground found, use current foot position
            currentFootTarget = foot.position;
        }

        lastFootPosition = currentFootTarget;
        isInitialized = true;

        Debug.Log($"[FootIK] Initialized at {currentFootTarget}");
    }

    /// <summary>
    /// Check if foot should take a step based on distance from target
    /// </summary>
    public bool ShouldStep(Vector3 desiredFootPosition)
    {
        if (isMoving) return false;

        float distance = Vector3.Distance(currentFootTarget, desiredFootPosition);
        return distance > stepDistance;
    }

    /// <summary>
    /// Initiate a step to a new target position
    /// </summary>
    public void Step(Vector3 targetPosition)
    {
        if (isMoving) return;

        // Raycast to find ground
        Vector3 rayStart = targetPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, groundLayer))
        {
            lastFootPosition = currentFootTarget;
            currentFootTarget = hit.point + Vector3.up * footOffset;
            isMoving = true;
            movementProgress = 0f;
        }
        else
        {
            // No ground found, step to desired position at current height
            lastFootPosition = currentFootTarget;
            currentFootTarget = new Vector3(targetPosition.x, currentFootTarget.y, targetPosition.z);
            isMoving = true;
            movementProgress = 0f;
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Apply IK to reach target
        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            Vector3 ikTarget = currentFootTarget;

            // During step, interpolate target
            if (isMoving)
            {
                movementProgress += Time.deltaTime * stepSpeed;

                if (movementProgress >= 1f)
                {
                    movementProgress = 1f;
                    isMoving = false;
                }

                // Smooth step curve with arc
                float t = movementProgress;
                float heightCurve = Mathf.Sin(t * Mathf.PI);

                Vector3 basePosition = Vector3.Lerp(lastFootPosition, currentFootTarget, t);
                Vector3 stepArc = Vector3.up * (heightCurve * stepHeight);

                ikTarget = basePosition + stepArc;
            }

            // Solve IK
            SolveTwoBoneIK(ikTarget);
        }
    }

    /// <summary>
    /// Two-bone IK solver using proper leg joint constraints
    /// </summary>
    private void SolveTwoBoneIK(Vector3 target)
    {
        if (upperLeg == null || lowerLeg == null || foot == null) return;

        Vector3 upperPos = upperLeg.position;
        Vector3 lowerPos = lowerLeg.position;
        Vector3 footPos = foot.position;

        // Get bone lengths from original hierarchy
        float upperLength = (lowerPos - upperPos).magnitude;
        float lowerLength = (footPos - lowerPos).magnitude;
        float totalLength = upperLength + lowerLength;

        // Safety check
        if (upperLength < 0.01f || lowerLength < 0.01f)
        {
            Debug.LogWarning("[FootIK] Invalid bone lengths!");
            return;
        }

        // Direction to target
        Vector3 toTarget = target - upperPos;
        float targetDistance = toTarget.magnitude;

        // Clamp target distance to prevent overextension
        if (targetDistance > totalLength * 0.98f)
        {
            targetDistance = totalLength * 0.98f;
            target = upperPos + toTarget.normalized * targetDistance;
            toTarget = target - upperPos;
        }

        // Calculate angles using law of cosines
        float a = upperLength;
        float b = lowerLength;
        float c = targetDistance;

        // Angle at upperLeg (hip/knee)
        float cosUpperAngle = (a * a + c * c - b * b) / (2f * a * c);
        cosUpperAngle = Mathf.Clamp(cosUpperAngle, -1f, 1f);
        float upperAngle = Mathf.Acos(cosUpperAngle);

        // Angle at lowerLeg (knee bend)
        float cosLowerAngle = (a * a + b * b - c * c) / (2f * a * b);
        cosLowerAngle = Mathf.Clamp(cosLowerAngle, -1f, 1f);
        float lowerAngle = Mathf.Acos(cosLowerAngle);

        // Calculate knee position (pole vector for 2-bone IK)
        Vector3 dirToTarget = toTarget.normalized;

        // Rotate around the character's right axis for knee bend
        Vector3 poleDirection = Vector3.Cross(dirToTarget, upperLeg.parent.right).normalized;

        // Calculate upper leg rotation
        Quaternion upperRotation = Quaternion.LookRotation(dirToTarget, poleDirection);
        upperRotation *= Quaternion.Euler(-upperAngle * Mathf.Rad2Deg, 0, 0);
        upperLeg.rotation = upperRotation;

        // Calculate lower leg rotation (knee joint)
        Vector3 upperToLower = lowerLeg.position - upperLeg.position;
        Quaternion lowerRotation = Quaternion.LookRotation(upperToLower.normalized, poleDirection);
        float kneeBend = 180f - (lowerAngle * Mathf.Rad2Deg);
        lowerRotation *= Quaternion.Euler(-kneeBend, 0, 0);
        lowerLeg.rotation = lowerRotation;

        // Keep foot aligned with ground (level)
        foot.rotation = Quaternion.Euler(0, upperLeg.parent.eulerAngles.y, 0);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            // Draw IK chain
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(upperLeg.position, lowerLeg.position);
            Gizmos.DrawLine(lowerLeg.position, foot.position);

            // Draw joints
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(upperLeg.position, 0.05f);
            Gizmos.DrawWireSphere(lowerLeg.position, 0.05f);
            Gizmos.DrawWireSphere(foot.position, 0.05f);
        }

        if (isInitialized)
        {
            // Draw target
            Gizmos.color = isMoving ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(currentFootTarget, 0.1f);
            Gizmos.DrawLine(currentFootTarget, currentFootTarget + Vector3.up * 0.2f);

            // Draw step trigger radius
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(currentFootTarget, stepDistance);
        }
    }
}