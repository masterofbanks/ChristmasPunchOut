using UnityEngine;

/// <summary>
/// Active balance controller for ragdoll character
/// Uses physics forces to maintain upright posture
/// Dynamically updates foot targets when character is moved
/// </summary>
public class ActiveRagdollBalancer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActiveRagdollCharacter character;

    [Header("Balance Settings")]
    [SerializeField] private float balanceStrength = 500f; // INCREASED from 50
    [SerializeField] private float balanceDamping = 100f; // INCREASED from 20
    [SerializeField] private float hipHeightTarget = 3.0f;
    [SerializeField] private float hipHeightForce = 500f; // INCREASED from 100
    [SerializeField] private float maxAngularVelocity = 10f;

    [Header("Feet Settings")]
    [SerializeField] private float footPlantForce = 500f; // INCREASED from 100
    [SerializeField] private float footDamping = 100f; // INCREASED from 50
    [SerializeField] private float footLevelingTorque = 200f; // INCREASED from 50
    [SerializeField] private float footSpacing = 0.5f;
    [SerializeField] private float footHeightOffset = 0.05f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundRaycastDistance = 5f;
    [SerializeField] private float footUpdateInterval = 0.2f;
    [SerializeField] private bool autoUpdateFootTargets = true;

    [Header("Movement")]
    [SerializeField] private Vector3 targetVelocity;
    [SerializeField] private float movementForce = 100f; // REDUCED from 200 to prevent tipping

    [Header("Stabilization")]
    [SerializeField] private bool enableVelocityLimits = true;
    [SerializeField] private float maxVelocity = 10f;
    [SerializeField] private float initialStabilizationTime = 0.5f;

    [Header("Debug Movement")]
    [SerializeField] private bool logMovementForces = false; // Turn off spam

    private Vector3 leftFootTarget;
    private Vector3 rightFootTarget;
    private bool leftFootPlanted = true;
    private bool rightFootPlanted = true;
    private bool isInitialized = false;
    private float footUpdateTimer = 0f;
    private Vector3 lastHipsPosition;
    private float positionChangeThreshold = 0.5f;
    private float stabilizationTimer = 0f;
    private bool isStabilizing = true;

    private void Start()
    {
        if (character == null)
            character = GetComponent<ActiveRagdollCharacter>();

        InitializeRigidbodySettings();

        if (character.leftFoot != null)
            leftFootTarget = character.leftFoot.transform.position;

        if (character.rightFoot != null)
            rightFootTarget = character.rightFoot.transform.position;

        if (character.hips != null)
            lastHipsPosition = character.hips.transform.position;

        isInitialized = true;
        stabilizationTimer = 0f;
        isStabilizing = true;

        Debug.Log("[ActiveRagdollBalancer] Initialized - stabilization period starting");
    }

    private void InitializeRigidbodySettings()
    {
        RagdollBone[] allBones = GetComponentsInChildren<RagdollBone>();

        foreach (RagdollBone bone in allBones)
        {
            Rigidbody rb = bone.GetRigidbody();
            if (rb != null)
            {
                rb.maxAngularVelocity = maxAngularVelocity;
                rb.maxLinearVelocity = maxVelocity;
                rb.linearDamping = 1f; // INCREASED from 0.5 for more stability
                rb.angularDamping = 5f; // INCREASED from 2 to reduce spinning
                rb.solverIterations = 20; // INCREASED from 10 for better joint stability
                rb.solverVelocityIterations = 20; // INCREASED from 10
            }
        }
    }

    private void FixedUpdate()
    {
        if (character == null || !isInitialized) return;

        // Stabilization period after spawn
        if (isStabilizing)
        {
            stabilizationTimer += Time.fixedDeltaTime;
            if (stabilizationTimer >= initialStabilizationTime)
            {
                isStabilizing = false;
                Debug.Log("[ActiveRagdollBalancer] ✓ Stabilization complete - FULL movement enabled");
            }
            else
            {
                ApplyGentleStabilization();
                ApplyMovement();
                return;
            }
        }

        CheckForPositionChange();

        if (autoUpdateFootTargets)
        {
            footUpdateTimer += Time.fixedDeltaTime;
            if (footUpdateTimer >= footUpdateInterval)
            {
                UpdateFootTargetsFromGround();
                footUpdateTimer = 0f;
            }
        }

        // IMPORTANT: Apply balance and foot control BEFORE velocity limits
        // This ensures balance forces aren't clamped
        MaintainUpright();
        MaintainHipHeight();
        ControlFeet();
        ApplyMovement();

        // Apply velocity limits LAST
        if (enableVelocityLimits)
        {
            LimitVelocities();
        }
    }

    private void ApplyGentleStabilization()
    {
        if (character.hips == null) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        Vector3 horizontalVel = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        Vector3 verticalVel = new Vector3(0, hipsRb.linearVelocity.y, 0);

        hipsRb.linearVelocity = horizontalVel + verticalVel * 0.9f;
        hipsRb.angularVelocity *= 0.95f;

        Vector3 currentUp = hipsRb.transform.up;
        Vector3 axis = Vector3.Cross(currentUp, Vector3.up);
        float angle = Vector3.Angle(currentUp, Vector3.up);

        if (angle > 5f)
        {
            hipsRb.AddTorque(axis.normalized * angle * 10f, ForceMode.Force); // INCREASED from 5
        }
    }

    private void LimitVelocities()
    {
        RagdollBone[] allBones = new RagdollBone[]
        {
            character.hips, character.spine, character.chest,
            character.leftUpperLeg, character.leftLowerLeg, character.leftFoot,
            character.rightUpperLeg, character.rightLowerLeg, character.rightFoot
        };

        foreach (RagdollBone bone in allBones)
        {
            if (bone == null) continue;

            Rigidbody rb = bone.GetRigidbody();
            if (rb == null) continue;

            if (rb.linearVelocity.magnitude > maxVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;
            }

            if (rb.angularVelocity.magnitude > maxAngularVelocity)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
            }
        }
    }

    private void CheckForPositionChange()
    {
        if (character.hips == null) return;

        Vector3 currentHipsPos = character.hips.transform.position;
        float distanceMoved = Vector3.Distance(currentHipsPos, lastHipsPosition);

        if (distanceMoved > positionChangeThreshold)
        {
            Debug.Log($"[ActiveRagdollBalancer] Character moved {distanceMoved:F2} units - resetting stabilization");

            ResetAllVelocities();
            UpdateFootTargetsFromGround();
            footUpdateTimer = 0f;

            isStabilizing = true;
            stabilizationTimer = 0f;
        }

        lastHipsPosition = currentHipsPos;
    }

    private void ResetAllVelocities()
    {
        RagdollBone[] allBones = GetComponentsInChildren<RagdollBone>();

        foreach (RagdollBone bone in allBones)
        {
            Rigidbody rb = bone.GetRigidbody();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void UpdateFootTargetsFromGround()
    {
        if (character.hips == null) return;

        Vector3 hipsPos = character.hips.transform.position;
        Vector3 hipsRight = character.hips.transform.right;

        Vector3 leftFootDesiredPos = hipsPos + hipsRight * -footSpacing;
        Vector3 rightFootDesiredPos = hipsPos + hipsRight * footSpacing;

        Vector3 newLeftTarget = FindGroundPosition(leftFootDesiredPos);
        Vector3 newRightTarget = FindGroundPosition(rightFootDesiredPos);

        if (newLeftTarget != Vector3.zero)
        {
            leftFootTarget = newLeftTarget + Vector3.up * footHeightOffset;
        }

        if (newRightTarget != Vector3.zero)
        {
            rightFootTarget = newRightTarget + Vector3.up * footHeightOffset;
        }
    }

    private Vector3 FindGroundPosition(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 2f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundLayer))
        {
            return hit.point;
        }

        if (character.hips != null)
        {
            rayStart = new Vector3(position.x, character.hips.transform.position.y + 1f, position.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit2, groundRaycastDistance, groundLayer))
            {
                return hit2.point;
            }
        }

        return Vector3.zero;
    }

    private void MaintainUpright()
    {
        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        Vector3 currentUp = hipsRb.transform.up;
        Vector3 targetUp = Vector3.up;

        Vector3 axis = Vector3.Cross(currentUp, targetUp);
        float angle = Vector3.Angle(currentUp, targetUp);

        // Apply torque even for small angles for constant correction
        if (angle > 0.5f) // REDUCED threshold from 2f
        {
            Vector3 torque = axis.normalized * angle * balanceStrength;
            Vector3 damping = hipsRb.angularVelocity * balanceDamping;
            hipsRb.AddTorque(torque - damping, ForceMode.Force);
        }

        // Also balance spine and chest for better stability
        if (character.spine != null)
        {
            Rigidbody spineRb = character.spine.GetRigidbody();
            Vector3 spineUp = spineRb.transform.up;
            Vector3 spineAxis = Vector3.Cross(spineUp, targetUp);
            float spineAngle = Vector3.Angle(spineUp, targetUp);

            if (spineAngle > 0.5f)
            {
                Vector3 spineTorque = spineAxis.normalized * spineAngle * balanceStrength * 0.5f;
                spineRb.AddTorque(spineTorque - spineRb.angularVelocity * balanceDamping, ForceMode.Force);
            }
        }

        // NEW: Also stabilize chest
        if (character.chest != null)
        {
            Rigidbody chestRb = character.chest.GetRigidbody();
            Vector3 chestUp = chestRb.transform.up;
            Vector3 chestAxis = Vector3.Cross(chestUp, targetUp);
            float chestAngle = Vector3.Angle(chestUp, targetUp);

            if (chestAngle > 0.5f)
            {
                Vector3 chestTorque = chestAxis.normalized * chestAngle * balanceStrength * 0.3f;
                chestRb.AddTorque(chestTorque - chestRb.angularVelocity * balanceDamping, ForceMode.Force);
            }
        }
    }

    private void MaintainHipHeight()
    {
        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        float leftFootActualY = character.leftFoot.transform.position.y;
        float rightFootActualY = character.rightFoot.transform.position.y;
        float avgFootHeight = (leftFootActualY + rightFootActualY) / 2f;

        float targetHeight = avgFootHeight + hipHeightTarget;
        float currentHeight = hipsRb.position.y;
        float heightError = targetHeight - currentHeight;

        float forceMagnitude = Mathf.Clamp(heightError * hipHeightForce, -hipHeightForce * 2f, hipHeightForce * 2f);
        Vector3 heightForce = Vector3.up * forceMagnitude;

        Vector3 verticalDamping = Vector3.up * hipsRb.linearVelocity.y * balanceDamping * 0.5f; // Increased damping multiplier

        hipsRb.AddForce(heightForce - verticalDamping, ForceMode.Force);
    }

    private void ControlFeet()
    {
        ApplyFootIK(character.leftFoot, leftFootTarget, ref leftFootPlanted);
        ApplyFootIK(character.rightFoot, rightFootTarget, ref rightFootPlanted);
    }

    private void ApplyFootIK(RagdollBone foot, Vector3 target, ref bool planted)
    {
        if (foot == null) return;

        Rigidbody footRb = foot.GetRigidbody();
        Vector3 toTarget = target - footRb.position;
        float distance = toTarget.magnitude;

        if (planted && distance < 2f)
        {
            Vector3 plantForce = toTarget * footPlantForce;
            Vector3 dampingForce = -footRb.linearVelocity * footDamping;

            footRb.AddForce(plantForce + dampingForce, ForceMode.Force);

            Vector3 footUp = footRb.transform.up;
            Vector3 axis = Vector3.Cross(footUp, Vector3.up);
            float angle = Vector3.Angle(footUp, Vector3.up);

            if (angle > 0.5f) // REDUCED from 2f for constant correction
            {
                Vector3 levelingTorque = axis.normalized * angle * footLevelingTorque;
                Vector3 angularDamping = -footRb.angularVelocity * footDamping;

                footRb.AddTorque(levelingTorque + angularDamping, ForceMode.Force);
            }
        }
    }

    private void ApplyMovement()
    {
        if (targetVelocity.magnitude < 0.01f)
        {
            return;
        }

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null || hipsRb.isKinematic) return;

        // Apply movement force
        Vector3 currentHorizontalVel = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentHorizontalVel;

        Vector3 force = velocityError * movementForce;
        hipsRb.AddForce(force, ForceMode.Force);

        // REDUCED lean to prevent tipping
        Vector3 moveDir = targetVelocity.normalized;
        float leanAngle = targetVelocity.magnitude * 0.5f; // REDUCED from 2f
        Vector3 leanTorque = Vector3.Cross(hipsRb.transform.up, moveDir) * leanAngle;
        hipsRb.AddTorque(leanTorque, ForceMode.Force);
    }

    public void SetTargetVelocity(Vector3 velocity)
    {
        targetVelocity = velocity;
    }

    public void UpdateFootTarget(bool leftFoot, Vector3 position)
    {
        if (leftFoot)
        {
            leftFootTarget = position;
        }
        else
        {
            rightFootTarget = position;
        }
    }

    public void ForceUpdateFootTargets()
    {
        UpdateFootTargetsFromGround();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isInitialized) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(leftFootTarget, 0.1f);
        Gizmos.DrawLine(leftFootTarget, leftFootTarget + Vector3.up * 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rightFootTarget, 0.1f);
        Gizmos.DrawLine(rightFootTarget, rightFootTarget + Vector3.up * 0.2f);

        if (character != null && character.leftFoot != null && character.rightFoot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(character.leftFoot.transform.position, 0.08f);
            Gizmos.DrawWireSphere(character.rightFoot.transform.position, 0.08f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(character.leftFoot.transform.position, leftFootTarget);
            Gizmos.DrawLine(character.rightFoot.transform.position, rightFootTarget);

            if (character.hips != null)
            {
                Vector3 hipsRight = character.hips.transform.right;
                Vector3 leftRayStart = character.hips.transform.position + hipsRight * -footSpacing + Vector3.up * 2f;
                Vector3 rightRayStart = character.hips.transform.position + hipsRight * footSpacing + Vector3.up * 2f;

                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawLine(leftRayStart, leftRayStart + Vector3.down * groundRaycastDistance);
                Gizmos.DrawLine(rightRayStart, rightRayStart + Vector3.down * groundRaycastDistance);

                if (targetVelocity.magnitude > 0.01f)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(character.hips.transform.position, targetVelocity.normalized * 2f);
                }
            }
        }

        if (character != null && character.hips != null)
        {
            float leftFootY = character.leftFoot.transform.position.y;
            float rightFootY = character.rightFoot.transform.position.y;
            float avgFootHeight = (leftFootY + rightFootY) / 2f;

            Vector3 targetHipPos = new Vector3(
                character.hips.transform.position.x,
                avgFootHeight + hipHeightTarget,
                character.hips.transform.position.z
            );

            Gizmos.color = isStabilizing ? Color.red : Color.green;
            Gizmos.DrawWireCube(targetHipPos, Vector3.one * 0.2f);

            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawLine(character.hips.transform.position, targetHipPos);
        }
    }
}