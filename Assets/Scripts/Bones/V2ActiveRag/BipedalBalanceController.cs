using UnityEngine;

/// <summary>
/// SINGLE SOURCE OF TRUTH for balance
/// FIXED: Added COM positioning over support base to prevent backward falling
/// </summary>
public class BipedalBalanceController : MonoBehaviour
{
    [Header("References")]
    public RagdollJoint hips;
    public RagdollJoint leftUpperLeg;
    public RagdollJoint leftLowerLeg;
    public RagdollJoint leftFoot;
    public RagdollJoint rightUpperLeg;
    public RagdollJoint rightLowerLeg;
    public RagdollJoint rightFoot;

    [Header("Hip Balance")]
    [SerializeField] private float hipBalanceTorque = 1500f;
    [SerializeField] private float hipBalanceDamping = 200f;

    [Header("Leg Stiffness (Prevent Knee Buckling)")]
    [SerializeField] private float legStiffnessTorque = 3000f;
    [SerializeField] private float legDamping = 300f;
    [SerializeField] private float targetKneeAngle = 5f;
    [SerializeField] private float maxKneeTorque = 5000f;

    [Header("COM Balance (Prevent Tipping)")]
    [SerializeField] private float comBalanceTorque = 800f;  // NEW: Shift COM over feet
    [SerializeField] private float comBalanceDamping = 150f;
    [SerializeField] private float comTargetOffsetForward = 0.1f;  // Slight forward lean

    [Header("Foot Ground Forces")]
    [SerializeField] private float footPushForce = 500f;
    [SerializeField] private float footDamping = 100f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logForces = true;
    [SerializeField] private int logInterval = 30;

    private Rigidbody hipsRb;
    private Rigidbody leftUpperLegRb, leftLowerLegRb, leftFootRb;
    private Rigidbody rightUpperLegRb, rightLowerLegRb, rightFootRb;

    private bool leftFootGrounded;
    private bool rightFootGrounded;
    private float targetHeight;
    private bool initialized = false;
    private int frameCount = 0;

    public void Initialize()
    {
        if (hips == null || leftFoot == null || rightFoot == null)
        {
            Debug.LogError("[BipedalBalance] Missing joint references!");
            enabled = false;
            return;
        }

        hipsRb = hips.GetComponent<Rigidbody>();

        leftUpperLegRb = leftUpperLeg.GetComponent<Rigidbody>();
        leftLowerLegRb = leftLowerLeg.GetComponent<Rigidbody>();
        leftFootRb = leftFoot.GetComponent<Rigidbody>();

        rightUpperLegRb = rightUpperLeg.GetComponent<Rigidbody>();
        rightLowerLegRb = rightLowerLeg.GetComponent<Rigidbody>();
        rightFootRb = rightFoot.GetComponent<Rigidbody>();

        if (hipsRb == null || leftFootRb == null || rightFootRb == null)
        {
            Debug.LogError("[BipedalBalance] Missing Rigidbody components!");
            enabled = false;
            return;
        }

        targetHeight = hipsRb.position.y;
        initialized = true;

        Debug.Log($"[BipedalBalance] ✓ Initialized at height: {targetHeight:F2}m");
        Debug.Log($"[BipedalBalance] Target knee angle: {targetKneeAngle}°");
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        frameCount++;
        bool shouldLog = logForces && (frameCount % logInterval == 0);

        CheckGroundContact();

        if (!leftFootGrounded && !rightFootGrounded)
        {
            return; // Airborne
        }

        // Core balance - ORDER MATTERS!
        BalanceCOMOverFeet(shouldLog);  // 1. NEW: Prevent tipping by shifting COM
        StraightenLegs(shouldLog);      // 2. Prevent knee buckling
        KeepHipsUpright(shouldLog);     // 3. Torso balance
        AnchorFeet();                   // 4. Keep feet planted
    }

    private void CheckGroundContact()
    {
        leftFootGrounded = Physics.Raycast(
            leftFootRb.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );

        rightFootGrounded = Physics.Raycast(
            rightFootRb.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );
    }

    /// <summary>
    /// NEW: Balance Center of Mass over support base
    /// Prevents the entire body from tipping forward/backward
    /// </summary>
    private void BalanceCOMOverFeet(bool log)
    {
        // Calculate center of support (average of grounded feet)
        Vector3 supportCenter = Vector3.zero;
        int groundedCount = 0;

        if (leftFootGrounded)
        {
            supportCenter += leftFootRb.position;
            groundedCount++;
        }

        if (rightFootGrounded)
        {
            supportCenter += rightFootRb.position;
            groundedCount++;
        }

        if (groundedCount == 0) return;

        supportCenter /= groundedCount;

        // Get COM (Center of Mass) - for simplicity, use hips as approximation
        Vector3 com = hipsRb.worldCenterOfMass;

        // Project both to ground plane (XZ only)
        Vector3 supportCenterXZ = new Vector3(supportCenter.x, 0, supportCenter.z);
        Vector3 comXZ = new Vector3(com.x, 0, com.z);

        // Calculate error (how far COM is from support center)
        Vector3 comError = supportCenterXZ - comXZ;

        // Add slight forward bias to prevent falling backward
        Vector3 forwardBias = hipsRb.transform.forward * comTargetOffsetForward;
        comError += forwardBias;

        if (log && comError.magnitude > 0.05f)
        {
            Debug.Log($"[COM BALANCE]");
            Debug.Log($"  COM XZ: {comXZ}");
            Debug.Log($"  Support center: {supportCenterXZ}");
            Debug.Log($"  COM error: {comError.magnitude:F3}m");
            Debug.Log($"  Direction: {comError.normalized}");
        }

        // Apply corrective torque to hips to shift COM
        // This is the "ankle strategy" - lean the body to shift weight
        Vector3 balanceTorque = Vector3.Cross(Vector3.up, comError) * comBalanceTorque;
        Vector3 dampingTorque = -hipsRb.angularVelocity * comBalanceDamping;

        Vector3 totalTorque = balanceTorque + dampingTorque;

        hipsRb.AddTorque(totalTorque, ForceMode.Force);

        if (log && comError.magnitude > 0.1f)
        {
            Debug.Log($"  Balance torque: {balanceTorque.magnitude:F0}Nm");
            Debug.Log($"  Total torque: {totalTorque.magnitude:F0}Nm");

            if (comError.z < -0.2f)
            {
                Debug.LogWarning($"  ⚠️ COM TOO FAR BACK: {comError.z:F2}m - FALLING BACKWARD!");
            }
        }
    }

    private void StraightenLegs(bool log)
    {
        if (leftFootGrounded)
        {
            StraightenKnee(leftLowerLeg, leftLowerLegRb, leftUpperLegRb, targetKneeAngle, "LEFT", log);
        }

        if (rightFootGrounded)
        {
            StraightenKnee(rightLowerLeg, rightLowerLegRb, rightUpperLegRb, targetKneeAngle, "RIGHT", log);
        }
    }

    private void StraightenKnee(RagdollJoint lowerLeg, Rigidbody lowerLegRb, Rigidbody upperLegRb, float targetAngle, string side, bool log)
    {
        ConfigurableJoint kneeJoint = lowerLeg.GetJoint();
        if (kneeJoint == null || kneeJoint.connectedBody == null) return;

        Quaternion localRot = Quaternion.Inverse(upperLegRb.rotation) * lowerLegRb.rotation;
        Vector3 eulerAngles = localRot.eulerAngles;
        float currentAngle = eulerAngles.x;

        if (currentAngle > 180f) currentAngle -= 360f;

        float angleError = targetAngle - currentAngle;

        if (log)
        {
            Debug.Log($"[{side} KNEE RAW]");
            Debug.Log($"  Current angle: {currentAngle:F1}°");
            Debug.Log($"  Target: {targetAngle:F1}°");
            Debug.Log($"  Error: {angleError:F1}°");
        }

        Vector3 kneeAxis = upperLegRb.transform.right;
        float torqueMagnitude = angleError * Mathf.Deg2Rad * legStiffnessTorque;
        torqueMagnitude = Mathf.Clamp(torqueMagnitude, -maxKneeTorque, maxKneeTorque);

        Vector3 straighteningTorque = kneeAxis * torqueMagnitude;

        Vector3 relativeAngVel = lowerLegRb.angularVelocity - upperLegRb.angularVelocity;
        float angVelOnAxis = Vector3.Dot(relativeAngVel, kneeAxis);
        Vector3 dampingTorque = -kneeAxis * angVelOnAxis * legDamping;

        Vector3 totalTorque = straighteningTorque + dampingTorque;

        lowerLegRb.AddTorque(totalTorque, ForceMode.Force);
        upperLegRb.AddTorque(-totalTorque, ForceMode.Force);

        if (log)
        {
            Debug.Log($"  Total torque: {totalTorque.magnitude:F0}Nm");

            if (currentAngle < -30f)
            {
                Debug.LogWarning($"  ⚠️ {side} KNEE SEVERELY BENT: {currentAngle:F1}°");
            }
        }
    }

    private void KeepHipsUpright(bool log)
    {
        Vector3 currentUp = hipsRb.transform.up;
        Vector3 targetUp = Vector3.up;

        Vector3 axis = Vector3.Cross(currentUp, targetUp);
        float angle = Vector3.Angle(currentUp, targetUp);

        if (angle > 1f)
        {
            Vector3 torque = axis.normalized * angle * Mathf.Deg2Rad * hipBalanceTorque;
            Vector3 damping = -hipsRb.angularVelocity * hipBalanceDamping;

            hipsRb.AddTorque(torque + damping, ForceMode.Force);

            if (log && angle > 10f)
            {
                Debug.Log($"[HIP BALANCE] Tilt: {angle:F1}°, Torque: {(torque + damping).magnitude:F0}Nm");
            }
        }
    }

    private void AnchorFeet()
    {
        if (leftFootGrounded)
        {
            AnchorFoot(leftFootRb);
        }

        if (rightFootGrounded)
        {
            AnchorFoot(rightFootRb);
        }
    }

    private void AnchorFoot(Rigidbody footRb)
    {
        // Keep foot level
        Vector3 currentUp = footRb.transform.up;
        Vector3 axis = Vector3.Cross(currentUp, Vector3.up);
        float angle = Vector3.Angle(currentUp, Vector3.up);

        if (angle > 0.5f)
        {
            Vector3 levelingTorque = axis.normalized * angle * Mathf.Deg2Rad * 200f;
            Vector3 damping = -footRb.angularVelocity * 50f;
            footRb.AddTorque(levelingTorque + damping, ForceMode.Force);
        }

        // Reduce sliding
        Vector3 horizontalVel = new Vector3(footRb.linearVelocity.x, 0, footRb.linearVelocity.z);
        if (horizontalVel.magnitude > 0.1f)
        {
            footRb.AddForce(-horizontalVel * 50f, ForceMode.Force);
        }

        // Slight upward force to help support
        if (footRb.linearVelocity.y < 0)
        {
            footRb.AddForce(Vector3.up * footPushForce, ForceMode.Force);
        }
    }

    public float GetBalanceQuality()
    {
        if (hipsRb == null) return 0f;

        float heightRatio = Mathf.Clamp01(hipsRb.position.y / targetHeight);
        float uprightness = Vector3.Dot(hipsRb.transform.up, Vector3.up);
        float stability = Mathf.Clamp01(1f - hipsRb.linearVelocity.magnitude / 5f);

        return (heightRatio + uprightness + stability) / 3f;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !initialized) return;

        // Draw COM
        if (hipsRb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(hipsRb.worldCenterOfMass, 0.15f);
        }

        // Draw support center
        Vector3 supportCenter = Vector3.zero;
        int groundedCount = 0;

        if (leftFootGrounded && leftFootRb != null)
        {
            supportCenter += leftFootRb.position;
            groundedCount++;
        }

        if (rightFootGrounded && rightFootRb != null)
        {
            supportCenter += rightFootRb.position;
            groundedCount++;
        }

        if (groundedCount > 0)
        {
            supportCenter /= groundedCount;

            // Draw support center
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(supportCenter, 0.12f);

            // Draw line from COM to support center
            if (hipsRb != null)
            {
                Vector3 com = hipsRb.worldCenterOfMass;
                Vector3 comXZ = new Vector3(com.x, supportCenter.y, com.z);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(supportCenter, comXZ);

                // Draw COM error magnitude
                float errorDist = Vector3.Distance(new Vector3(supportCenter.x, 0, supportCenter.z), new Vector3(comXZ.x, 0, comXZ.z));
                if (errorDist > 0.1f)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.green;
                }
                Gizmos.DrawWireSphere(comXZ, 0.1f);
            }
        }

        // Draw foot ground checks
        if (leftFootRb != null)
        {
            Gizmos.color = leftFootGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(leftFootRb.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
        }

        if (rightFootRb != null)
        {
            Gizmos.color = rightFootGrounded ? Color.green : Color.red;
            Gizmos.DrawRay(rightFootRb.position + Vector3.up * 0.1f, Vector3.down * groundCheckDistance);
        }

        // Draw knee torque direction
        if (leftUpperLegRb != null && leftLowerLegRb != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 kneePos = leftLowerLegRb.position;
            Vector3 kneeAxis = leftUpperLegRb.transform.right;
            Gizmos.DrawRay(kneePos, kneeAxis * 0.3f);
        }

        if (rightUpperLegRb != null && rightLowerLegRb != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 kneePos = rightLowerLegRb.position;
            Vector3 kneeAxis = rightUpperLegRb.transform.right;
            Gizmos.DrawRay(kneePos, kneeAxis * 0.3f);
        }
    }
}