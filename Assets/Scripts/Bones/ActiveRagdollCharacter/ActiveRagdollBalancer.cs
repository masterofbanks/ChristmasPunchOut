using UnityEngine;

/// <summary>
/// Active balance controller for ragdoll character
/// IMPROVED: Reduced force magnitudes, better smoothing, animator coordination, optimized raycasts
/// </summary>
public class ActiveRagdollBalancer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActiveRagdollCharacter character;
    [SerializeField] private ProceduralLegAnimator animator; // NEW: Reference for coordination

    [Header("Balance Settings")]
    [SerializeField] private float balanceStrength = 300f; // REDUCED from 500
    [SerializeField] private float balanceDamping = 100f;
    [SerializeField] private float maxAngularVelocity = 10f;

    [Header("Grounding")]
    [SerializeField] private float groundingForce = 300f; // REDUCED from 500
    [SerializeField] private float maxGroundDistance = 0.2f;
    [SerializeField] private float targetHipsHeight = 2.5f;

    [Header("Feet Settings")]
    [SerializeField] private float footPlantForce = 300f; // REDUCED from 1000
    [SerializeField] private float footDamping = 200f; // INCREASED from 150
    [SerializeField] private float footMaxForce = 500f; // NEW: Force clamp
    [SerializeField] private float footLevelingTorque = 200f;
    [SerializeField] private float footSpacing = 0.5f;
    [SerializeField] private float footHeightOffset = 0.05f;
    [SerializeField] private float footFriction = 10f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundRaycastDistance = 10f;
    [SerializeField] private float footUpdateInterval = 0.1f;
    [SerializeField] private bool autoUpdateFootTargets = true;

    [Header("Movement")]
    [SerializeField] private Vector3 targetVelocity;
    [SerializeField] private float movementForce = 300f;

    [Header("Stabilization")]
    [SerializeField] private bool enableVelocityLimits = true;
    [SerializeField] private float maxVelocity = 10f;
    [SerializeField] private float initialStabilizationTime = 0.5f;

    [Header("Smoothing - IMPROVED")]
    [SerializeField] private float earlySmoothingFactor = 0.05f; // NEW: Aggressive smoothing for first 100 frames
    [SerializeField] private float normalSmoothingFactor = 0.1f; // Standard smoothing after stabilization
    [SerializeField] private float maxTargetJump = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool logInitialization = false; // Disabled by default
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private bool logGrounding = false;
    [SerializeField] private bool logFootIK = false;
    [SerializeField] private bool logForces = false;
    [SerializeField] private bool logRaycast = false;
    [SerializeField] private int logFrameInterval = 30;

    private Vector3 leftFootTarget;
    private Vector3 rightFootTarget;
    private bool leftFootPlanted = true;
    private bool rightFootPlanted = true;
    private bool isInitialized = false;
    private float footUpdateTimer = 0f;
    private Vector3 lastHipsPosition;
    private float stabilizationTimer = 0f;
    private bool isStabilizing = true;
    private float currentGroundLevel = 0f;

    private int frameCount = 0;
    private Collider[] characterColliders;

    // Track jumping/jitter
    private float lastHipsY = 0f;
    private float maxVerticalVelocity = 0f;
    private int jumpDetectionCount = 0;

    // NEW: Raycast cache for optimization
    private RaycastHit[] raycastHitCache = new RaycastHit[10];

    private void Start()
    {
        if (character == null)
        {
            character = GetComponentInParent<ActiveRagdollCharacter>();

            if (character == null)
            {
                Debug.LogError("[ActiveRagdollBalancer] ❌ ActiveRagdollCharacter not found!");
                return;
            }
        }

        // NEW: Auto-find animator if not assigned
        if (animator == null)
        {
            animator = GetComponent<ProceduralLegAnimator>();
        }

        CacheCharacterColliders();
        InitializeRigidbodySettings();
        InitializeFootTargets();

        if (character.hips != null)
        {
            lastHipsPosition = character.hips.transform.position;
            lastHipsY = character.hips.transform.position.y;
        }

        isInitialized = true;
        stabilizationTimer = 0f;
        isStabilizing = true;

        Debug.Log("[ActiveRagdollBalancer] ✓ Initialized with improved force control");
    }

    private void CacheCharacterColliders()
    {
        characterColliders = character.GetComponentsInChildren<Collider>();

        if (logInitialization)
        {
            Debug.Log($"[CacheCharacterColliders] Found {characterColliders.Length} colliders:");
            foreach (Collider col in characterColliders)
            {
                Debug.Log($"  - {col.gameObject.name} ({col.GetType().Name})");
            }
        }
    }

    private void InitializeFootTargets()
    {
        if (character.hips == null)
        {
            Debug.LogError("[InitializeFootTargets] ❌ Hips is NULL!");
            return;
        }

        Vector3 hipsPos = character.hips.transform.position;
        Vector3 hipsRight = character.hips.transform.right;

        Vector3 leftFootDesiredPos = hipsPos + hipsRight * -footSpacing;
        Vector3 rightFootDesiredPos = hipsPos + hipsRight * footSpacing;

        leftFootTarget = FindGroundBelowHips(leftFootDesiredPos, "LEFT INIT");
        rightFootTarget = FindGroundBelowHips(rightFootDesiredPos, "RIGHT INIT");

        currentGroundLevel = (leftFootTarget.y + rightFootTarget.y) / 2f;
        targetHipsHeight = hipsPos.y - currentGroundLevel;

        if (logInitialization)
        {
            Debug.Log($"========== INITIALIZATION COMPLETE ==========");
            Debug.Log($"Ground level: Y={currentGroundLevel:F2}");
            Debug.Log($"Target hips height: {targetHipsHeight:F2}m");
            Debug.Log($"Left foot target: {leftFootTarget}");
            Debug.Log($"Right foot target: {rightFootTarget}");
            Debug.Log($"============================================");
        }
    }

    private void InitializeRigidbodySettings()
    {
        RagdollBone[] allBones = character.GetComponentsInChildren<RagdollBone>();

        foreach (RagdollBone bone in allBones)
        {
            Rigidbody rb = bone.GetRigidbody();
            if (rb != null)
            {
                rb.maxAngularVelocity = maxAngularVelocity;
                rb.maxLinearVelocity = maxVelocity;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 3f;
                rb.solverIterations = 20;
                rb.solverVelocityIterations = 20;
                rb.useGravity = true;

                // NEW: Explicit center of mass control for better stability
                rb.centerOfMass = Vector3.zero;
                rb.automaticCenterOfMass = false;
            }
        }

        // NEW: Lower hips center of mass for improved stability
        if (character.hips != null)
        {
            Rigidbody hipsRb = character.hips.GetRigidbody();
            if (hipsRb != null)
            {
                hipsRb.centerOfMass = new Vector3(0, -0.2f, 0);
            }
        }

        Debug.Log($"[ActiveRagdollBalancer] ✓ Configured {allBones.Length} bones with improved COM");
    }

    private void FixedUpdate()
    {
        frameCount++;

        if (character == null || !isInitialized) return;

        bool shouldLog = logEveryFrame && (frameCount % logFrameInterval == 0);

        if (shouldLog)
        {
            Debug.Log($"\n========== FRAME {frameCount} (Time: {Time.time:F2}s) ==========");
        }

        // Track vertical movement for jump detection
        if (character.hips != null)
        {
            Rigidbody hipsRb = character.hips.GetRigidbody();
            float currentHipsY = hipsRb.position.y;
            float verticalVelocity = hipsRb.linearVelocity.y;
            float deltaY = currentHipsY - lastHipsY;

            if (Mathf.Abs(verticalVelocity) > maxVerticalVelocity)
            {
                maxVerticalVelocity = Mathf.Abs(verticalVelocity);
            }

            // Detect "jumping" - rapid upward movement
            if (deltaY > 0.1f && verticalVelocity > 1f)
            {
                jumpDetectionCount++;
                Debug.LogWarning($"⚠ JUMP DETECTED! Frame {frameCount}:");
                Debug.LogWarning($"  Delta Y: +{deltaY:F3}m in one frame");
                Debug.LogWarning($"  Vertical velocity: {verticalVelocity:F2} m/s");
                Debug.LogWarning($"  Total jumps detected: {jumpDetectionCount}");
            }

            lastHipsY = currentHipsY;

            if (shouldLog)
            {
                Debug.Log($"[HIPS STATUS]");
                Debug.Log($"  Position: {hipsRb.position}");
                Debug.Log($"  Velocity: {hipsRb.linearVelocity} (vertical: {verticalVelocity:F2})");
                Debug.Log($"  Max vertical velocity seen: {maxVerticalVelocity:F2} m/s");
            }
        }

        if (isStabilizing)
        {
            stabilizationTimer += Time.fixedDeltaTime;
            if (stabilizationTimer >= initialStabilizationTime)
            {
                isStabilizing = false;
                Debug.Log("[ActiveRagdollBalancer] ✓ Stabilization complete");
            }
            else
            {
                if (shouldLog) Debug.Log($"[STABILIZING] {stabilizationTimer:F2}/{initialStabilizationTime:F2}s");
                ApplyGentleStabilization(shouldLog);
                ApplyGrounding(shouldLog);
                return;
            }
        }

        if (autoUpdateFootTargets)
        {
            footUpdateTimer += Time.fixedDeltaTime;
            if (footUpdateTimer >= footUpdateInterval)
            {
                if (shouldLog) Debug.Log("[UPDATING FOOT TARGETS]");
                UpdateFootTargetsFromHips(shouldLog);
                footUpdateTimer = 0f;
            }
        }

        MaintainUpright(shouldLog);
        ApplyGrounding(shouldLog);
        ControlFeet(shouldLog);
        ApplyMovement(shouldLog);

        if (enableVelocityLimits)
        {
            LimitVelocities(shouldLog);
        }

        lastHipsPosition = character.hips.transform.position;

        if (shouldLog)
        {
            Debug.Log($"========== END FRAME {frameCount} ==========\n");
        }
    }

    private void ApplyGrounding(bool log)
    {
        if (character.hips == null) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        float currentHipsHeight = hipsRb.position.y - currentGroundLevel;
        float heightError = currentHipsHeight - targetHipsHeight;

        if (log && logGrounding)
        {
            Debug.Log($"[GROUNDING]");
            Debug.Log($"  Hips Y: {hipsRb.position.y:F3}");
            Debug.Log($"  Ground level: {currentGroundLevel:F3}");
            Debug.Log($"  Current height: {currentHipsHeight:F3}m, Target: {targetHipsHeight:F3}m");
            Debug.Log($"  Error: {heightError:F3}m");
        }

        if (heightError > maxGroundDistance)
        {
            float forceMagnitude = heightError * groundingForce;
            Vector3 downForce = Vector3.down * forceMagnitude;
            hipsRb.AddForce(downForce, ForceMode.Force);

            if (log && logGrounding)
            {
                Debug.Log($"  ⬇ TOO HIGH! Force: {forceMagnitude:F0}N");
            }

            // Dampen upward velocity
            if (hipsRb.linearVelocity.y > 0)
            {
                Vector3 dampForce = Vector3.down * hipsRb.linearVelocity.y * 200f;
                hipsRb.AddForce(dampForce, ForceMode.Force);
            }
        }
        else if (heightError < -maxGroundDistance)
        {
            float forceMagnitude = -heightError * groundingForce * 0.5f;
            Vector3 upForce = Vector3.up * forceMagnitude;
            hipsRb.AddForce(upForce, ForceMode.Force);

            if (log && logGrounding)
            {
                Debug.Log($"  ⬆ TOO LOW! Force: {forceMagnitude:F0}N");
            }
        }
        else if (log && logGrounding)
        {
            Debug.Log($"  ✓ Height OK");
        }
    }

    private void ApplyGentleStabilization(bool log)
    {
        if (character.hips == null) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        hipsRb.linearVelocity *= 0.95f;
        hipsRb.angularVelocity *= 0.95f;

        Vector3 currentUp = hipsRb.transform.up;
        Vector3 axis = Vector3.Cross(currentUp, Vector3.up);
        float angle = Vector3.Angle(currentUp, Vector3.up);

        if (angle > 5f)
        {
            Vector3 torque = axis.normalized * angle * 10f;
            hipsRb.AddTorque(torque, ForceMode.Force);
        }
    }

    private void LimitVelocities(bool log)
    {
        RagdollBone[] allBones = new RagdollBone[]
        {
            character.hips, character.spine, character.chest,
            character.leftUpperLeg, character.leftLowerLeg, character.leftFoot,
            character.rightUpperLeg, character.rightLowerLeg, character.rightFoot
        };

        int clampedCount = 0;
        foreach (RagdollBone bone in allBones)
        {
            if (bone == null) continue;

            Rigidbody rb = bone.GetRigidbody();
            if (rb == null) continue;

            bool wasClamped = false;

            if (rb.linearVelocity.magnitude > maxVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;
                wasClamped = true;
            }

            if (rb.angularVelocity.magnitude > maxAngularVelocity)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
                wasClamped = true;
            }

            if (wasClamped) clampedCount++;
        }

        if (log && clampedCount > 0)
        {
            Debug.Log($"[VELOCITY LIMIT] Clamped {clampedCount} bones");
        }
    }

    private void UpdateFootTargetsFromHips(bool log)
    {
        if (character.hips == null) return;

        Vector3 hipsPos = character.hips.transform.position;
        Vector3 hipsRight = character.hips.transform.right;

        Vector3 leftFootHorizontalPos = hipsPos + hipsRight * -footSpacing;
        Vector3 rightFootHorizontalPos = hipsPos + hipsRight * footSpacing;

        Vector3 oldLeftTarget = leftFootTarget;
        Vector3 oldRightTarget = rightFootTarget;
        float oldGroundLevel = currentGroundLevel;

        Vector3 newLeftTarget = FindGroundBelowHips(leftFootHorizontalPos, "LEFT UPDATE");
        Vector3 newRightTarget = FindGroundBelowHips(rightFootHorizontalPos, "RIGHT UPDATE");

        // IMPROVED: Adaptive smoothing based on frame count
        float smoothingFactor = frameCount < 100 ? earlySmoothingFactor : normalSmoothingFactor;
        float minTargetHeight = -10f;
        float maxTargetHeight = hipsPos.y - 1f;

        // Validate and smooth left target
        if (newLeftTarget.y < minTargetHeight || newLeftTarget.y > maxTargetHeight)
        {
            Debug.LogError($"[LEFT TARGET REJECTED] Invalid height: {newLeftTarget.y:F2}");
            newLeftTarget = oldLeftTarget;
        }
        else if (Vector3.Distance(newLeftTarget, oldLeftTarget) > maxTargetJump)
        {
            if (log)
            {
                Debug.LogWarning($"[LEFT TARGET SMOOTHED] Jump: {Vector3.Distance(newLeftTarget, oldLeftTarget):F2}m, factor: {smoothingFactor:P0}");
            }
            newLeftTarget = Vector3.Lerp(oldLeftTarget, newLeftTarget, smoothingFactor);
        }

        // Validate and smooth right target
        if (newRightTarget.y < minTargetHeight || newRightTarget.y > maxTargetHeight)
        {
            Debug.LogError($"[RIGHT TARGET REJECTED] Invalid height: {newRightTarget.y:F2}");
            newRightTarget = oldRightTarget;
        }
        else if (Vector3.Distance(newRightTarget, oldRightTarget) > maxTargetJump)
        {
            if (log)
            {
                Debug.LogWarning($"[RIGHT TARGET SMOOTHED] Jump: {Vector3.Distance(newRightTarget, oldRightTarget):F2}m, factor: {smoothingFactor:P0}");
            }
            newRightTarget = Vector3.Lerp(oldRightTarget, newRightTarget, smoothingFactor);
        }

        leftFootTarget = newLeftTarget;
        rightFootTarget = newRightTarget;

        currentGroundLevel = (leftFootTarget.y + rightFootTarget.y) / 2f;

        if (log)
        {
            Debug.Log($"[FOOT TARGET UPDATE]");
            Debug.Log($"  Left delta: {(leftFootTarget.y - oldLeftTarget.y):F3}m");
            Debug.Log($"  Right delta: {(rightFootTarget.y - oldRightTarget.y):F3}m");
            Debug.Log($"  Ground level delta: {(currentGroundLevel - oldGroundLevel):F3}m");
        }
    }

    // OPTIMIZED: Uses RaycastNonAlloc to avoid memory allocation
    private Vector3 FindGroundBelowHips(Vector3 horizontalPosition, string debugLabel)
    {
        if (character.hips == null) return horizontalPosition;

        Vector3 rayStart = new Vector3(
            horizontalPosition.x,
            character.hips.transform.position.y,
            horizontalPosition.z
        );

        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, raycastHitCache, groundRaycastDistance, groundLayer);

        if (logRaycast)
        {
            Debug.Log($"[RAYCAST {debugLabel}] Origin: {rayStart}, Hits: {hitCount}");
        }

        // Sort hits by distance (closest first) - only sort the valid portion
        System.Array.Sort(raycastHitCache, 0, hitCount, System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

        // Find the first hit that is NOT part of the character
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHitCache[i];
            bool isCharacterCollider = false;

            foreach (Collider characterCol in characterColliders)
            {
                if (hit.collider == characterCol)
                {
                    isCharacterCollider = true;
                    if (logRaycast)
                    {
                        Debug.Log($"    ⊗ Ignoring {hit.collider.gameObject.name}");
                    }
                    break;
                }
            }

            if (!isCharacterCollider)
            {
                Vector3 groundPos = hit.point + Vector3.up * footHeightOffset;

                if (logRaycast)
                {
                    Debug.Log($"    ✓ GROUND: {hit.collider.gameObject.name} at {groundPos}");
                }

                return groundPos;
            }
        }

        // No ground found - fallback
        Vector3 fallback = rayStart + Vector3.down * 3f;
        Debug.LogError($"[{debugLabel}] ❌ NO GROUND FOUND! Using fallback: {fallback}");

        return fallback;
    }

    private void MaintainUpright(bool log)
    {
        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        Vector3 currentUp = hipsRb.transform.up;
        Vector3 targetUp = Vector3.up;

        Vector3 axis = Vector3.Cross(currentUp, targetUp);
        float angle = Vector3.Angle(currentUp, targetUp);

        if (angle > 0.5f)
        {
            Vector3 torque = axis.normalized * angle * balanceStrength;
            Vector3 damping = hipsRb.angularVelocity * balanceDamping;
            hipsRb.AddTorque(torque - damping, ForceMode.Force);

            if (log && angle > 10f)
            {
                Debug.Log($"[BALANCE] Hips tilted {angle:F1}°");
            }
        }

        // Spine balance
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

        // Chest balance
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

    private void ControlFeet(bool log)
    {
        ApplyFootIK(character.leftFoot, leftFootTarget, ref leftFootPlanted, true, log);
        ApplyFootIK(character.rightFoot, rightFootTarget, ref rightFootPlanted, false, log);
    }

    // IMPROVED: Checks if animator is controlling the foot before applying forces
    private void ApplyFootIK(RagdollBone foot, Vector3 target, ref bool planted, bool isLeftFoot, bool log)
    {
        if (foot == null) return;

        Rigidbody footRb = foot.GetRigidbody();
        if (footRb == null) return;

        // NEW: Check if animator is controlling this foot (prevents force conflicts)
        if (animator != null && animator.enabled && animator.IsFootStepping(isLeftFoot))
        {
            if (log && logFootIK)
            {
                Debug.Log($"[FOOT IK - {(isLeftFoot ? "LEFT" : "RIGHT")}] ⊗ Animator in control, skipping IK");
            }
            return;
        }

        Vector3 toTarget = target - footRb.position;
        float distance = toTarget.magnitude;
        float verticalOffset = footRb.position.y - target.y;

        if (log && logFootIK)
        {
            Debug.Log($"[FOOT IK - {(isLeftFoot ? "LEFT" : "RIGHT")}]");
            Debug.Log($"  Distance: {distance:F3}m, Vertical offset: {verticalOffset:F3}m");
        }

        if (planted)
        {
            Vector3 plantForce = toTarget * footPlantForce;
            Vector3 dampingForce = -footRb.linearVelocity * footDamping;

            // NEW: Clamp total force to prevent spikes
            Vector3 totalForce = plantForce + dampingForce;
            totalForce = Vector3.ClampMagnitude(totalForce, footMaxForce);

            footRb.AddForce(totalForce, ForceMode.Force);

            if (log && logFootIK && logForces)
            {
                Debug.Log($"  Total force: {totalForce.magnitude:F0}N (clamped at {footMaxForce}N)");

                if (totalForce.magnitude >= footMaxForce * 0.9f)
                {
                    Debug.LogWarning($"    ⚠ Force near maximum!");
                }
            }

            // Horizontal friction
            Vector3 horizontalVel = new Vector3(footRb.linearVelocity.x, 0, footRb.linearVelocity.z);
            if (horizontalVel.magnitude > 0.1f)
            {
                Vector3 frictionForce = -horizontalVel.normalized * footFriction;
                footRb.AddForce(frictionForce, ForceMode.Force);
            }

            // Foot leveling
            Vector3 footUp = footRb.transform.up;
            Vector3 axis = Vector3.Cross(footUp, Vector3.up);
            float angle = Vector3.Angle(footUp, Vector3.up);

            if (angle > 0.5f)
            {
                Vector3 levelingTorque = axis.normalized * angle * footLevelingTorque;
                Vector3 angularDamping = -footRb.angularVelocity * footDamping;
                footRb.AddTorque(levelingTorque + angularDamping, ForceMode.Force);
            }
        }
    }

    private void ApplyMovement(bool log)
    {
        if (targetVelocity.magnitude < 0.01f) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null || hipsRb.isKinematic) return;

        Vector3 currentHorizontalVel = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentHorizontalVel;

        Vector3 force = velocityError * movementForce;
        hipsRb.AddForce(force, ForceMode.Force);

        // Lean into movement
        Vector3 moveDir = targetVelocity.normalized;
        float leanAngle = targetVelocity.magnitude * 0.3f;
        Vector3 leanTorque = Vector3.Cross(hipsRb.transform.up, moveDir) * leanAngle;
        hipsRb.AddTorque(leanTorque, ForceMode.Force);

        if (log)
        {
            Debug.Log($"[MOVEMENT] Force: {force.magnitude:F0}N");
        }
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

        currentGroundLevel = (leftFootTarget.y + rightFootTarget.y) / 2f;
    }

    public void ForceUpdateFootTargets()
    {
        UpdateFootTargetsFromHips(true);
    }

    // NEW: Public method to check if foot is being controlled by balancer
    public bool IsFootControlledByBalancer(bool isLeftFoot)
    {
        return isLeftFoot ? leftFootPlanted : rightFootPlanted;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isInitialized || character == null) return;

        // Ground plane
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Vector3 groundCenter = character.hips != null ?
            new Vector3(character.hips.transform.position.x, currentGroundLevel, character.hips.transform.position.z) :
            Vector3.zero;
        Gizmos.DrawCube(groundCenter, new Vector3(5f, 0.02f, 5f));

        // Target height
        Gizmos.color = Color.magenta;
        float targetY = currentGroundLevel + targetHipsHeight;
        Gizmos.DrawWireCube(new Vector3(groundCenter.x, targetY, groundCenter.z), new Vector3(1f, 0.1f, 1f));

        // Foot targets
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(leftFootTarget, 0.1f);
        Gizmos.DrawLine(leftFootTarget, leftFootTarget + Vector3.up * 0.2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rightFootTarget, 0.1f);
        Gizmos.DrawLine(rightFootTarget, rightFootTarget + Vector3.up * 0.2f);

        if (character.leftFoot != null && character.rightFoot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(character.leftFoot.transform.position, 0.08f);
            Gizmos.DrawWireSphere(character.rightFoot.transform.position, 0.08f);

            Gizmos.color = character.leftFoot.transform.position.y > leftFootTarget.y ? Color.green : Color.red;
            Gizmos.DrawLine(character.leftFoot.transform.position, leftFootTarget);

            Gizmos.color = character.rightFoot.transform.position.y > rightFootTarget.y ? Color.green : Color.red;
            Gizmos.DrawLine(character.rightFoot.transform.position, rightFootTarget);

            if (character.hips != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(character.hips.transform.position, 0.15f);

                if (targetVelocity.magnitude > 0.01f)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(character.hips.transform.position, targetVelocity.normalized * 2f);
                }
            }
        }
    }
}