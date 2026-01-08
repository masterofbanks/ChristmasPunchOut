using UnityEngine;

/// <summary>
/// Balance controller with proper grounding
/// FIXED: Layer masks, grounding anchor, reduced forces
/// </summary>
public class BalanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActiveRagdollV2 ragdoll;

    [Header("Balance Settings")]
    [SerializeField] private float balanceStrength = 200f;  // REDUCED
    [SerializeField] private float balanceDamping = 50f;

    [Header("Grounding - NEW")]
    [SerializeField] private float groundAnchorForce = 500f;
    [SerializeField] private float targetHipsHeight = 2.5f;
    [SerializeField] private float groundTolerance = 0.1f;

    [Header("Foot Anchoring")]
    [SerializeField] private float footAnchorStrength = 100f; // REDUCED from 500
    [SerializeField] private float footMaxDistance = 1f;      // NEW: Distance check
    [SerializeField] private bool leftFootAnchored = true;
    [SerializeField] private bool rightFootAnchored = true;

    [Header("Ground Detection - FIXED")]
    [SerializeField] private LayerMask groundLayer = 1 << 0; // Default layer only
    [SerializeField] private float groundCheckDistance = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private Rigidbody hipsRb;
    private float currentGroundLevel = 0f;
    private Collider[] characterColliders;

    private void Start()
    {
        if (ragdoll == null) ragdoll = GetComponent<ActiveRagdollV2>();

        if (ragdoll.hips != null)
        {
            hipsRb = ragdoll.hips.GetComponent<Rigidbody>();
        }

        // Cache character colliders for raycast filtering
        characterColliders = ragdoll.GetComponentsInChildren<Collider>();

        InitializeGroundLevel();
    }

    private void InitializeGroundLevel()
    {
        if (ragdoll.leftFoot == null || ragdoll.rightFoot == null) return;

        // Find ground below feet
        Vector3 leftFootGroundPos = FindGround(ragdoll.leftFoot.transform.position);
        Vector3 rightFootGroundPos = FindGround(ragdoll.rightFoot.transform.position);

        currentGroundLevel = (leftFootGroundPos.y + rightFootGroundPos.y) / 2f;

        if (showDebug)
        {
            Debug.Log($"[BalanceController] Ground level: {currentGroundLevel:F2}");
        }
    }

    private void FixedUpdate()
    {
        if (hipsRb == null) return;

        MaintainUpright();
        AnchorToGroundHeight();
        AnchorFeet();
    }

    private void MaintainUpright()
    {
        Vector3 currentUp = hipsRb.transform.up;
        Vector3 axis = Vector3.Cross(currentUp, Vector3.up);
        float angle = Vector3.Angle(currentUp, Vector3.up);

        if (angle > 1f)
        {
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * balanceStrength);
            Vector3 damping = -hipsRb.angularVelocity * balanceDamping;
            hipsRb.AddTorque(torque + damping, ForceMode.Force);
        }
    }

    /// <summary>
    /// NEW: Anchor hips to target height above ground
    /// </summary>
    private void AnchorToGroundHeight()
    {
        float currentHeight = hipsRb.position.y - currentGroundLevel;
        float heightError = currentHeight - targetHipsHeight;

        // Only apply force if outside tolerance
        if (Mathf.Abs(heightError) > groundTolerance)
        {
            float force = -heightError * groundAnchorForce;
            hipsRb.AddForce(Vector3.up * force, ForceMode.Force);

            // Dampen vertical velocity
            if (Mathf.Abs(hipsRb.linearVelocity.y) > 0.1f)
            {
                hipsRb.AddForce(Vector3.down * hipsRb.linearVelocity.y * 100f, ForceMode.Force);
            }

            if (showDebug && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[Grounding] Height: {currentHeight:F2}m, Error: {heightError:F2}m, Force: {force:F0}N");
            }
        }
    }

    private void AnchorFeet()
    {
        if (leftFootAnchored && ragdoll.leftFoot != null)
        {
            AnchorFoot(ragdoll.leftFoot, "Left");
        }

        if (rightFootAnchored && ragdoll.rightFoot != null)
        {
            AnchorFoot(ragdoll.rightFoot, "Right");
        }
    }

    private void AnchorFoot(RagdollJoint foot, string footName)
    {
        Rigidbody footRb = foot.GetComponent<Rigidbody>();
        if (footRb == null) return;

        Vector3 groundPos = FindGround(foot.transform.position);
        Vector3 toGround = groundPos - footRb.position;

        // NEW: Only apply force if foot is close to ground
        if (toGround.magnitude > footMaxDistance)
        {
            if (showDebug && Time.frameCount % 120 == 0)
            {
                Debug.Log($"[{footName} Foot] Too far from ground: {toGround.magnitude:F2}m");
            }
            return;
        }

        // Apply spring force to ground
        Vector3 force = toGround * footAnchorStrength;
        Vector3 damping = -footRb.linearVelocity * (footAnchorStrength * 0.3f);

        footRb.AddForce(force + damping, ForceMode.Force);

        // Keep foot level
        Vector3 footUp = footRb.transform.up;
        Vector3 axis = Vector3.Cross(footUp, Vector3.up);
        float angle = Vector3.Angle(footUp, Vector3.up);

        if (angle > 1f)
        {
            footRb.AddTorque(axis.normalized * angle * 50f, ForceMode.Force);
        }
    }

    /// <summary>
    /// FIXED: Find ground with proper layer filtering
    /// </summary>
    private Vector3 FindGround(Vector3 fromPosition)
    {
        Vector3 rayStart = fromPosition + Vector3.up * 0.5f;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, groundCheckDistance, groundLayer);

        // Filter out character colliders
        foreach (RaycastHit hit in hits)
        {
            bool isCharacterCollider = false;

            foreach (Collider col in characterColliders)
            {
                if (hit.collider == col)
                {
                    isCharacterCollider = true;
                    break;
                }
            }

            if (!isCharacterCollider)
            {
                return hit.point + Vector3.up * 0.05f; // Slight offset
            }
        }

        // Fallback: return position slightly below current
        return new Vector3(fromPosition.x, currentGroundLevel, fromPosition.z);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || ragdoll == null) return;

        // Draw ground plane
        if (hipsRb != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Vector3 groundCenter = new Vector3(hipsRb.position.x, currentGroundLevel, hipsRb.position.z);
            Gizmos.DrawCube(groundCenter, new Vector3(3f, 0.02f, 3f));

            // Draw target hips height
            Gizmos.color = Color.magenta;
            float targetY = currentGroundLevel + targetHipsHeight;
            Gizmos.DrawWireCube(new Vector3(groundCenter.x, targetY, groundCenter.z), new Vector3(0.5f, 0.1f, 0.5f));
        }

        // Draw foot ground targets
        if (ragdoll.leftFoot != null)
        {
            Vector3 leftGround = FindGround(ragdoll.leftFoot.transform.position);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftGround, 0.1f);
            Gizmos.DrawLine(ragdoll.leftFoot.transform.position, leftGround);
        }

        if (ragdoll.rightFoot != null)
        {
            Vector3 rightGround = FindGround(ragdoll.rightFoot.transform.position);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rightGround, 0.1f);
            Gizmos.DrawLine(ragdoll.rightFoot.transform.position, rightGround);
        }
    }
}