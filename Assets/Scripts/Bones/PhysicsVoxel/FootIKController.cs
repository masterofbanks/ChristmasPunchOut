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

    private Vector3 upperLegOriginalPos;
    private Vector3 lowerLegOriginalPos;
    private Vector3 footOriginalPos;

    public bool IsMoving => isMoving;
    public Vector3 FootTarget => currentFootTarget;

    private void Start()
    {
        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            // Store original positions relative to parent
            upperLegOriginalPos = upperLeg.localPosition;
            lowerLegOriginalPos = lowerLeg.localPosition;
            footOriginalPos = foot.localPosition;

            // Initialize foot target
            currentFootTarget = foot.position;
            lastFootPosition = currentFootTarget;
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
            upperLegOriginalPos = upperLeg.localPosition;
            lowerLegOriginalPos = lowerLeg.localPosition;
            footOriginalPos = foot.localPosition;

            currentFootTarget = foot.position;
            lastFootPosition = currentFootTarget;
        }
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
    }

    private void Update()
    {
        if (isMoving)
        {
            AnimateStep();
        }

        // Apply IK
        if (upperLeg != null && lowerLeg != null && foot != null)
        {
            SolveTwoBoneIK();
        }
    }

    private void AnimateStep()
    {
        movementProgress += Time.deltaTime * stepSpeed;

        if (movementProgress >= 1f)
        {
            movementProgress = 1f;
            isMoving = false;
        }

        // Smooth step curve
        float t = movementProgress;
        float heightCurve = Mathf.Sin(t * Mathf.PI); // Arc for step

        // Interpolate position with arc
        Vector3 basePosition = Vector3.Lerp(lastFootPosition, currentFootTarget, t);
        Vector3 stepArc = Vector3.up * (heightCurve * stepHeight);

        foot.position = basePosition + stepArc;
    }

    /// <summary>
    /// Two-bone IK solver (simplified for legs)
    /// </summary>
    private void SolveTwoBoneIK()
    {
        Vector3 target = isMoving ? foot.position : currentFootTarget;

        Vector3 upperPos = upperLeg.position;
        Vector3 lowerPos = lowerLeg.position;
        Vector3 footPos = foot.position;

        // Calculate limb lengths
        float upperLength = Vector3.Distance(upperPos, lowerPos);
        float lowerLength = Vector3.Distance(lowerPos, footPos);
        float totalLength = upperLength + lowerLength;

        // Direction to target
        Vector3 toTarget = target - upperPos;
        float targetDistance = toTarget.magnitude;

        // Clamp target distance to limb length
        if (targetDistance > totalLength * 0.99f)
        {
            targetDistance = totalLength * 0.99f;
            target = upperPos + toTarget.normalized * targetDistance;
        }

        // Calculate angles using law of cosines
        float upperAngle = Mathf.Acos(
            Mathf.Clamp(
                (upperLength * upperLength + targetDistance * targetDistance - lowerLength * lowerLength) /
                (2f * upperLength * targetDistance),
                -1f, 1f
            )
        );

        float lowerAngle = Mathf.Acos(
            Mathf.Clamp(
                (upperLength * upperLength + lowerLength * lowerLength - targetDistance * targetDistance) /
                (2f * upperLength * lowerLength),
                -1f, 1f
            )
        );

        // Apply rotations
        Vector3 dirToTarget = (target - upperPos).normalized;
        Quaternion upperRot = Quaternion.LookRotation(Vector3.forward, dirToTarget) * Quaternion.Euler(upperAngle * Mathf.Rad2Deg, 0, 0);
        upperLeg.rotation = upperRot;

        // Lower leg bends based on upper leg orientation
        Vector3 kneeDir = upperLeg.TransformDirection(Vector3.down);
        Quaternion lowerRot = Quaternion.LookRotation(Vector3.forward, kneeDir) * Quaternion.Euler((180f - lowerAngle * Mathf.Rad2Deg), 0, 0);
        lowerLeg.rotation = lowerRot;

        // Keep foot level
        foot.rotation = Quaternion.identity;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || upperLeg == null || lowerLeg == null || foot == null) return;

        // Draw IK chain
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(upperLeg.position, lowerLeg.position);
        Gizmos.DrawLine(lowerLeg.position, foot.position);

        // Draw target
        Gizmos.color = isMoving ? Color.green : Color.blue;
        Gizmos.DrawWireSphere(currentFootTarget, 0.1f);

        // Draw step trigger radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(currentFootTarget, stepDistance);
    }
}