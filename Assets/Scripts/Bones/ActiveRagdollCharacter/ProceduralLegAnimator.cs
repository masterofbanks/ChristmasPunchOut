using UnityEngine;

/// <summary>
/// Procedural leg animation system for active ragdoll
/// ENHANCED: Terrain adaptation, dynamic gait, IK constraints, obstacle avoidance,
/// directional movement, foot rotation, audio, and event system
/// FIXED: Only updates balancer with GROUND targets, not mid-air step positions
/// </summary>
public class ProceduralLegAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActiveRagdollCharacter character;
    [SerializeField] private ActiveRagdollBalancer balancer;

    [Header("Step Settings")]
    [SerializeField] private float baseStepDistance = 0.6f;
    [SerializeField] private float baseStepHeight = 0.25f;
    [SerializeField] private float baseStepDuration = 0.25f;
    [SerializeField] private float stepSpeed = 10f;
    [SerializeField] private float footPlantTime = 0.15f;

    [Header("Hip Movement")]
    [SerializeField] private bool enableHipSway = true;
    [SerializeField] private float hipSwayAmount = 0.05f;
    [SerializeField] private float hipSwaySpeed = 2f;
    [SerializeField] private float hipBobAmount = 0.03f;
    [SerializeField] private float hipBobSpeed = 4f;

    [Header("Foot Placement")]
    [SerializeField] private float footForwardOffset = 0.4f;
    [SerializeField] private float footLateralSpacing = 0.4f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundRayDistance = 3f;

    [Header("Animation Blend")]
    [SerializeField] private float animationInfluence = 0.5f;
    [SerializeField] private float minVelocityToAnimate = 0.1f;

    // ========== ENHANCEMENT 1: TERRAIN ADAPTATION ==========
    [Header("1. Terrain Adaptation")]
    [SerializeField] private bool adaptToTerrainSlope = true;
    [SerializeField] private float maxStepHeightDifference = 0.5f;
    [SerializeField] private float slopeCompensation = 1.5f;

    // ========== ENHANCEMENT 2: DYNAMIC GAIT SYSTEM ==========
    [Header("2. Dynamic Gait System")]
    [SerializeField] private bool enableDynamicGait = true;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private AnimationCurve stepDistanceCurve = AnimationCurve.Linear(0, 0.4f, 1, 0.8f);
    [SerializeField] private AnimationCurve stepHeightCurve = AnimationCurve.EaseInOut(0, 0.15f, 1, 0.35f);
    [SerializeField] private AnimationCurve stepDurationCurve = AnimationCurve.Linear(0, 0.35f, 1, 0.2f);

    // ========== ENHANCEMENT 3: IK CONSTRAINTS ==========
    [Header("3. IK Constraints")]
    [SerializeField] private bool enforceIKLimits = true;
    [SerializeField] private float maxLegExtension = 2.5f;
    [SerializeField] private float minLegExtension = 0.8f;
    [SerializeField] private float hipJointOffsetX = 0.3f;

    // ========== ENHANCEMENT 4: PREDICTIVE STEP PLANNING ==========
    [Header("4. Prediction")]
    [SerializeField] private bool enablePrediction = true;
    [SerializeField] private float predictionTime = 0.3f;
    [SerializeField] private float predictionStepMultiplier = 1.2f;

    // ========== ENHANCEMENT 5: OBSTACLE AVOIDANCE ==========
    [Header("5. Obstacle Detection")]
    [SerializeField] private bool enableObstacleDetection = true;
    [SerializeField] private float obstacleDetectionDistance = 0.8f;
    [SerializeField] private float obstacleStepOverHeight = 0.4f;
    [SerializeField] private float obstacleClearance = 0.1f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    // ========== ENHANCEMENT 6: DIRECTIONAL MOVEMENT ==========
    [Header("6. Directional Movement")]
    [SerializeField] private bool enableBackwardWalk = true;
    [SerializeField] private float backwardStepReduction = 0.7f;
    [SerializeField] private bool enableStrafing = true;
    [SerializeField] private float strafeAngleThreshold = 45f;
    [SerializeField] private float strafeStanceMultiplier = 1.3f;

    // ========== ENHANCEMENT 7: FOOT ROTATION ALIGNMENT ==========
    [Header("7. Foot Rotation")]
    [SerializeField] private bool alignFootToGround = true;
    [SerializeField] private float footRotationSpeed = 10f;

    // ========== ENHANCEMENT 8: AUDIO SYSTEM ==========
    [Header("8. Audio")]
    [SerializeField] private bool enableFootstepAudio = true;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float footstepVolume = 0.5f;
    [SerializeField] private float footstepPitchVariation = 0.1f;
    private AudioSource audioSource;

    // ========== ENHANCEMENT 9: EVENT SYSTEM ==========
    [Header("9. Events")]
    [SerializeField] private bool enableEvents = true;
    public event System.Action<bool, Vector3> OnFootPlanted; // isLeftFoot, position
    public event System.Action<bool> OnFootLift; // isLeftFoot
    public event System.Action<GaitType> OnGaitChanged;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logStepEvents = false;
    [SerializeField] private bool logGaitChanges = false;
    [SerializeField] private bool logObstacles = false;
    [SerializeField] private bool logBalancerUpdates = false; // NEW: Debug balancer interference

    // Step state
    private enum LegState { Planted, Stepping }
    private LegState leftLegState = LegState.Planted;
    private LegState rightLegState = LegState.Planted;

    public enum GaitType { Idle, Walk, Run, Sprint }
    private GaitType currentGait = GaitType.Idle;

    private Vector3 leftFootPlantedPosition;
    private Vector3 rightFootPlantedPosition;
    private Vector3 leftFootStepTarget;
    private Vector3 rightFootStepTarget;

    private float leftStepProgress = 0f;
    private float rightStepProgress = 0f;

    // Dynamic parameters (modified by gait system)
    private float stepDistance;
    private float stepHeight;
    private float stepDuration;

    private Vector3 lastHipsPosition;
    private float cycleTime = 0f;

    private bool isInitialized = false;
    private float initializationDelay = 0.5f;
    private float initTimer = 0f;

    // Raycast caches
    private RaycastHit[] raycastHitCache = new RaycastHit[5];
    private RaycastHit[] obstacleHitCache = new RaycastHit[3];

    // Terrain info cache
    private RaycastHit lastLeftFootHit;
    private RaycastHit lastRightFootHit;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (character == null)
        {
            character = GetComponentInParent<ActiveRagdollCharacter>();
        }

        if (balancer == null)
        {
            balancer = GetComponent<ActiveRagdollBalancer>();
        }

        if (character == null)
        {
            Debug.LogError("[ProceduralLegAnimator] ActiveRagdollCharacter not found!");
            return;
        }

        // Initialize dynamic parameters
        stepDistance = baseStepDistance;
        stepHeight = baseStepHeight;
        stepDuration = baseStepDuration;

        if (character.hips != null)
        {
            lastHipsPosition = character.hips.transform.position;
        }

        // Initialize animation curves if not set
        if (stepDistanceCurve == null || stepDistanceCurve.length == 0)
        {
            stepDistanceCurve = AnimationCurve.Linear(0, 0.4f, 1, 0.8f);
        }
        if (stepHeightCurve == null || stepHeightCurve.length == 0)
        {
            stepHeightCurve = AnimationCurve.EaseInOut(0, 0.15f, 1, 0.35f);
        }
        if (stepDurationCurve == null || stepDurationCurve.length == 0)
        {
            stepDurationCurve = AnimationCurve.Linear(0, 0.35f, 1, 0.2f);
        }

        Debug.Log("[ProceduralLegAnimator] ✓ Initialized with enhanced features - waiting for stabilization");
    }

    private void Update()
    {
        if (character == null) return;

        // Wait for initialization delay
        if (!isInitialized)
        {
            initTimer += Time.deltaTime;
            if (initTimer >= initializationDelay)
            {
                if (character.leftFoot != null)
                {
                    leftFootPlantedPosition = character.leftFoot.transform.position;
                }

                if (character.rightFoot != null)
                {
                    rightFootPlantedPosition = character.rightFoot.transform.position;
                }

                isInitialized = true;
                Debug.Log("[ProceduralLegAnimator] ✓ Animation system ready with all enhancements");
            }
            return;
        }

        UpdateWalkCycle();
    }

    private void UpdateWalkCycle()
    {
        if (character.hips == null) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        Vector3 velocity = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        float speed = velocity.magnitude;

        // ENHANCEMENT 2: Update gait parameters based on speed
        UpdateGaitParameters(speed);

        if (speed < minVelocityToAnimate)
        {
            if (leftLegState == LegState.Planted && rightLegState == LegState.Planted)
            {
                UpdateIdleStance();
            }
            return;
        }

        cycleTime += Time.deltaTime * speed;

        CheckForStep(character.leftFoot, ref leftLegState, ref leftFootPlantedPosition,
                     ref leftFootStepTarget, ref leftStepProgress, velocity, true);

        CheckForStep(character.rightFoot, ref rightLegState, ref rightFootPlantedPosition,
                     ref rightFootStepTarget, ref rightStepProgress, velocity, false);

        if (leftLegState == LegState.Stepping)
        {
            ExecuteStep(character.leftFoot, leftFootPlantedPosition, leftFootStepTarget,
                       ref leftStepProgress, ref leftLegState, ref leftFootPlantedPosition, true);
        }

        if (rightLegState == LegState.Stepping)
        {
            ExecuteStep(character.rightFoot, rightFootPlantedPosition, rightFootStepTarget,
                       ref rightStepProgress, ref rightLegState, ref rightFootPlantedPosition, false);
        }

        if (enableHipSway)
        {
            ApplyHipAnimation(speed);
        }

        lastHipsPosition = character.hips.transform.position;
    }

    // ========== ENHANCEMENT 2: DYNAMIC GAIT SYSTEM ==========
    private void UpdateGaitParameters(float speed)
    {
        GaitType previousGait = currentGait;

        // Determine current gait
        if (speed < minVelocityToAnimate)
            currentGait = GaitType.Idle;
        else if (speed < walkSpeed)
            currentGait = GaitType.Walk;
        else if (speed < runSpeed)
            currentGait = GaitType.Run;
        else
            currentGait = GaitType.Sprint;

        // Trigger gait change event
        if (enableEvents && currentGait != previousGait)
        {
            OnGaitChanged?.Invoke(currentGait);
            if (logGaitChanges)
            {
                Debug.Log($"[Gait] Changed from {previousGait} to {currentGait} at speed {speed:F2}m/s");
            }
        }

        // Dynamically adjust parameters based on speed
        if (enableDynamicGait)
        {
            float normalizedSpeed = Mathf.Clamp01(speed / sprintSpeed);
            stepDistance = stepDistanceCurve.Evaluate(normalizedSpeed);
            stepHeight = stepHeightCurve.Evaluate(normalizedSpeed);
            stepDuration = stepDurationCurve.Evaluate(normalizedSpeed);
        }
        else
        {
            // Use base values
            stepDistance = baseStepDistance;
            stepHeight = baseStepHeight;
            stepDuration = baseStepDuration;
        }
    }

    private void CheckForStep(RagdollBone foot, ref LegState legState, ref Vector3 plantedPos,
                              ref Vector3 stepTarget, ref float stepProgress, Vector3 velocity, bool isLeftFoot)
    {
        if (foot == null) return;
        if (legState == LegState.Stepping) return;

        Vector3 footPos = foot.transform.position;
        float distanceFromPlant = Vector3.Distance(
            new Vector3(footPos.x, 0, footPos.z),
            new Vector3(plantedPos.x, 0, plantedPos.z)
        );

        Vector3 idealFootPos = CalculateIdealFootPosition(velocity, isLeftFoot);

        bool shouldStep = distanceFromPlant > stepDistance;

        // ENHANCEMENT 4: Predictive step planning
        if (enablePrediction && !shouldStep)
        {
            Vector3 predictedHipsPos = character.hips.transform.position + velocity * predictionTime;
            float predictedDistance = Vector3.Distance(
                new Vector3(footPos.x, 0, footPos.z),
                new Vector3(predictedHipsPos.x, 0, predictedHipsPos.z)
            );

            if (predictedDistance > stepDistance * predictionStepMultiplier)
            {
                shouldStep = true;
                if (logStepEvents)
                {
                    Debug.Log($"[Prediction] {(isLeftFoot ? "LEFT" : "RIGHT")} foot step predicted (distance will be {predictedDistance:F2}m)");
                }
            }
        }

        // Don't allow both feet to step simultaneously
        LegState otherLegState = isLeftFoot ? rightLegState : leftLegState;
        if (otherLegState == LegState.Stepping)
        {
            shouldStep = false;
        }

        if (shouldStep)
        {
            legState = LegState.Stepping;
            stepProgress = 0f;
            stepTarget = idealFootPos;

            // ENHANCEMENT 9: Trigger foot lift event
            if (enableEvents)
            {
                OnFootLift?.Invoke(isLeftFoot);
            }

            if (logStepEvents)
            {
                Debug.Log($"[ProceduralLegAnimator] {(isLeftFoot ? "LEFT" : "RIGHT")} stepping to {stepTarget}");
            }
        }
    }

    // ========== ENHANCEMENT 6: DIRECTIONAL MOVEMENT ==========
    private Vector3 CalculateIdealFootPosition(Vector3 velocity, bool isLeftFoot)
    {
        if (character.hips == null) return Vector3.zero;

        Vector3 hipsPos = character.hips.transform.position;
        Vector3 hipsForward = character.hips.transform.forward;
        Vector3 hipsRight = character.hips.transform.right;
        Vector3 moveDir = velocity.normalized;

        // Determine movement direction relative to character
        float forwardDot = Vector3.Dot(moveDir, hipsForward);
        float rightDot = Vector3.Dot(moveDir, hipsRight);

        float lateralOffset = isLeftFoot ? -footLateralSpacing : footLateralSpacing;
        float forwardOffsetMultiplier = footForwardOffset;

        // Adjust for backward movement
        if (enableBackwardWalk && forwardDot < -0.5f)
        {
            forwardOffsetMultiplier *= -backwardStepReduction;
            if (logStepEvents)
            {
                Debug.Log($"[Directional] Backward walking detected (dot: {forwardDot:F2})");
            }
        }
        // Adjust for strafing
        else if (enableStrafing && Mathf.Abs(rightDot) > Mathf.Cos(strafeAngleThreshold * Mathf.Deg2Rad))
        {
            forwardOffsetMultiplier *= 0.5f;
            lateralOffset *= strafeStanceMultiplier;
            if (logStepEvents)
            {
                Debug.Log($"[Directional] Strafing detected (right dot: {rightDot:F2})");
            }
        }

        Vector3 lateralPos = hipsPos + hipsRight * lateralOffset;
        Vector3 forwardPos = lateralPos + moveDir * forwardOffsetMultiplier;

        // ENHANCEMENT 1: Terrain-adaptive foot placement
        Vector3 groundPos = FindGroundPosition(forwardPos, out RaycastHit hit);

        if (adaptToTerrainSlope && hit.collider != null)
        {
            // Adjust foot position based on terrain normal
            Vector3 slopeDirection = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            if (slopeAngle > 1f) // Only adjust for slopes > 1 degree
            {
                groundPos += slopeDirection.normalized * (slopeAngle / 90f) * slopeCompensation;

                // Validate step height isn't too extreme
                float stepHeightDiff = Mathf.Abs(groundPos.y - hipsPos.y);
                if (stepHeightDiff > maxStepHeightDifference)
                {
                    float clampedY = Mathf.Clamp(groundPos.y,
                        hipsPos.y - maxStepHeightDifference,
                        hipsPos.y + maxStepHeightDifference);
                    groundPos.y = clampedY;

                    if (logStepEvents)
                    {
                        Debug.LogWarning($"[Terrain] Step height clamped from {stepHeightDiff:F2}m to {maxStepHeightDifference:F2}m");
                    }
                }
            }
        }

        // ENHANCEMENT 3: IK constraints validation
        if (enforceIKLimits)
        {
            bool isValid = ValidateFootTarget(groundPos, isLeftFoot, out Vector3 clampedPos);
            if (!isValid)
            {
                if (logStepEvents)
                {
                    Debug.LogWarning($"[IK] {(isLeftFoot ? "LEFT" : "RIGHT")} foot target clamped to reachable distance");
                }
                groundPos = clampedPos;
            }
        }

        // Store hit info for foot rotation
        if (isLeftFoot)
            lastLeftFootHit = hit;
        else
            lastRightFootHit = hit;

        return groundPos;
    }

    // ========== ENHANCEMENT 3: IK CONSTRAINTS ==========
    private bool ValidateFootTarget(Vector3 targetPos, bool isLeftFoot, out Vector3 clampedPos)
    {
        Vector3 hipPos = character.hips.transform.position;
        Vector3 hipsRight = character.hips.transform.right;

        // Get approximate hip joint position
        Vector3 hipJointPos = hipPos + hipsRight * (isLeftFoot ? -hipJointOffsetX : hipJointOffsetX);

        // Check leg extension distance
        float distance = Vector3.Distance(hipJointPos, targetPos);

        if (distance > maxLegExtension)
        {
            // Clamp to maximum reach
            Vector3 direction = (targetPos - hipJointPos).normalized;
            clampedPos = hipJointPos + direction * maxLegExtension;
            return false;
        }
        else if (distance < minLegExtension)
        {
            // Too close - maintain minimum distance
            Vector3 direction = (targetPos - hipJointPos).normalized;
            clampedPos = hipJointPos + direction * minLegExtension;
            return false;
        }

        clampedPos = targetPos;
        return true;
    }

    private Vector3 FindGroundPosition(Vector3 position, out RaycastHit hitInfo)
    {
        Vector3 rayStart = position + Vector3.up * 1f;

        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, raycastHitCache, groundRayDistance, groundLayer);

        if (hitCount > 0)
        {
            hitInfo = raycastHitCache[0];
            return hitInfo.point + Vector3.up * 0.05f;
        }

        // Fallback: use current ground level
        hitInfo = new RaycastHit();
        if (character.leftFoot != null && character.rightFoot != null)
        {
            float avgFootY = (character.leftFoot.transform.position.y + character.rightFoot.transform.position.y) / 2f;
            return new Vector3(position.x, avgFootY, position.z);
        }

        return new Vector3(position.x, character.hips.transform.position.y - 2f, position.z);
    }

    // Backward compatibility method
    private Vector3 FindGroundPosition(Vector3 position)
    {
        return FindGroundPosition(position, out _);
    }

    // ========== ENHANCEMENT 5: OBSTACLE DETECTION ==========
    private float DetectObstacleHeight(Vector3 startPos, Vector3 endPos)
    {
        if (!enableObstacleDetection) return 0f;

        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);

        // Raycast forward at foot level
        int hitCount = Physics.RaycastNonAlloc(startPos, direction, obstacleHitCache,
            Mathf.Min(distance, obstacleDetectionDistance), obstacleMask);

        if (hitCount > 0)
        {
            RaycastHit hit = obstacleHitCache[0];

            // Measure obstacle height
            Vector3 topCheckStart = hit.point + Vector3.up * 1f;

            if (Physics.Raycast(topCheckStart, Vector3.down, out RaycastHit topHit, 1f, obstacleMask))
            {
                float obstacleHeight = topHit.point.y - startPos.y;

                if (logObstacles)
                {
                    Debug.Log($"[Obstacle] Detected at {hit.point}, height: {obstacleHeight:F2}m");
                }

                return Mathf.Clamp(obstacleHeight, 0, obstacleStepOverHeight);
            }
        }

        return 0f;
    }

    private void ExecuteStep(RagdollBone foot, Vector3 startPos, Vector3 endPos,
                            ref float stepProgress, ref LegState legState, ref Vector3 plantedPos, bool isLeftFoot)
    {
        if (foot == null) return;

        stepProgress += Time.deltaTime / stepDuration;

        if (stepProgress >= 1f)
        {
            legState = LegState.Planted;
            plantedPos = endPos;
            stepProgress = 0f;

            // ✅ FIX: Only update balancer when foot is PLANTED (on ground)
            if (balancer != null)
            {
                balancer.UpdateFootTarget(isLeftFoot, endPos);

                if (logBalancerUpdates)
                {
                    Debug.Log($"[Balancer Update] {(isLeftFoot ? "LEFT" : "RIGHT")} foot PLANTED at {endPos.y:F2}m");
                }
            }

            // ENHANCEMENT 8: Play footstep audio
            PlayFootstepSound(endPos);

            // ENHANCEMENT 9: Trigger foot planted event
            if (enableEvents)
            {
                OnFootPlanted?.Invoke(isLeftFoot, endPos);
            }

            if (logStepEvents)
            {
                Debug.Log($"[ProceduralLegAnimator] {(isLeftFoot ? "LEFT" : "RIGHT")} planted at {endPos}");
            }
            return;
        }

        float t = Mathf.SmoothStep(0f, 1f, stepProgress);

        // ENHANCEMENT 5: Check for obstacles
        float obstacleHeight = DetectObstacleHeight(startPos, endPos);

        Vector3 horizontalPos = Vector3.Lerp(startPos, endPos, t);

        // Enhanced arc calculation with obstacle clearance
        float baseHeight = Mathf.Sin(t * Mathf.PI) * stepHeight;
        float clearanceHeight = obstacleHeight > 0 ? obstacleHeight + obstacleClearance : 0f;
        float totalHeight = Mathf.Max(baseHeight, clearanceHeight);

        Vector3 targetPos = horizontalPos + Vector3.up * totalHeight;

        Rigidbody footRb = foot.GetRigidbody();
        if (footRb != null)
        {
            Vector3 toTarget = targetPos - footRb.position;
            float forceMagnitude = toTarget.magnitude * stepSpeed * 100f;

            Vector3 force = toTarget.normalized * forceMagnitude;
            footRb.AddForce(force, ForceMode.Force);

            footRb.linearVelocity *= 0.9f;

            // ENHANCEMENT 7: Foot rotation alignment
            if (alignFootToGround)
            {
                ApplyFootRotation(footRb, isLeftFoot, stepProgress);
            }
        }

        // ❌ REMOVED: Don't update balancer with mid-air positions!
        // This was causing the character to float upward
        /*
        if (balancer != null)
        {
            balancer.UpdateFootTarget(isLeftFoot, targetPos); // THIS WAS THE PROBLEM!
        }
        */

        if (logBalancerUpdates)
        {
            Debug.Log($"[Balancer Update] {(isLeftFoot ? "LEFT" : "RIGHT")} foot STEPPING (height: {targetPos.y:F2}m) - NOT updating balancer");
        }
    }

    // ========== ENHANCEMENT 7: FOOT ROTATION ALIGNMENT ==========
    private void ApplyFootRotation(Rigidbody footRb, bool isLeftFoot, float stepProgress)
    {
        RaycastHit hit = isLeftFoot ? lastLeftFootHit : lastRightFootHit;

        if (hit.collider != null)
        {
            // Calculate target rotation from ground normal
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            targetRotation *= character.hips.transform.rotation; // Maintain character rotation

            // Smoothly rotate foot, more aggressively as it approaches ground
            float rotationBlend = Mathf.Clamp01(stepProgress * 2f); // Rotate more in second half of step
            Quaternion newRotation = Quaternion.Slerp(
                footRb.rotation,
                targetRotation,
                Time.deltaTime * footRotationSpeed * rotationBlend
            );

            footRb.MoveRotation(newRotation);
        }
    }

    // ========== ENHANCEMENT 8: AUDIO SYSTEM ==========
    private void PlayFootstepSound(Vector3 position)
    {
        if (!enableFootstepAudio || footstepSounds == null || footstepSounds.Length == 0)
            return;

        // Create audio source if needed
        if (audioSource == null)
        {
            GameObject audioObj = new GameObject("FootstepAudio");
            audioObj.transform.SetParent(character.hips.transform);
            audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.maxDistance = 20f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        // Pick random clip
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];

        // Add pitch variation
        audioSource.pitch = 1f + Random.Range(-footstepPitchVariation, footstepPitchVariation);

        // Play sound
        audioSource.PlayOneShot(clip, footstepVolume);
    }

    private void ApplyHipAnimation(float speed)
    {
        if (character.hips == null) return;

        Rigidbody hipsRb = character.hips.GetRigidbody();
        if (hipsRb == null) return;

        float swayAngle = Mathf.Sin(cycleTime * hipSwaySpeed) * hipSwayAmount * speed;
        Vector3 swayTorque = character.hips.transform.forward * swayAngle * 10f;

        float bobForce = Mathf.Sin(cycleTime * hipBobSpeed) * hipBobAmount * speed * 50f;
        Vector3 bobForce3D = Vector3.up * bobForce;

        hipsRb.AddTorque(swayTorque * animationInfluence, ForceMode.Force);
        hipsRb.AddForce(bobForce3D * animationInfluence, ForceMode.Force);
    }

    private void UpdateIdleStance()
    {
        if (character.hips == null) return;

        Vector3 hipsPos = character.hips.transform.position;
        Vector3 hipsRight = character.hips.transform.right;

        Vector3 leftIdlePos = hipsPos + hipsRight * -footLateralSpacing + Vector3.down * 2f;
        Vector3 rightIdlePos = hipsPos + hipsRight * footLateralSpacing + Vector3.down * 2f;

        leftFootPlantedPosition = FindGroundPosition(leftIdlePos);
        rightFootPlantedPosition = FindGroundPosition(rightIdlePos);

        // ✅ This is OK - idle feet are on the ground
        if (balancer != null)
        {
            balancer.UpdateFootTarget(true, leftFootPlantedPosition);
            balancer.UpdateFootTarget(false, rightFootPlantedPosition);
        }
    }

    // ========== PUBLIC API ==========

    public bool IsFootStepping(bool isLeftFoot)
    {
        return isLeftFoot ? (leftLegState == LegState.Stepping) : (rightLegState == LegState.Stepping);
    }

    public void SetAnimationInfluence(float influence)
    {
        animationInfluence = Mathf.Clamp01(influence);
    }

    public float GetWalkCyclePhase()
    {
        return (cycleTime % 1f);
    }

    public GaitType GetCurrentGait()
    {
        return currentGait;
    }

    public Vector3 GetFootTarget(bool isLeftFoot)
    {
        return isLeftFoot ? leftFootStepTarget : rightFootStepTarget;
    }

    // ========== DEBUG VISUALIZATION ==========

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !isInitialized) return;

        // Planted positions
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftFootPlantedPosition, 0.1f);
        Gizmos.DrawWireSphere(rightFootPlantedPosition, 0.1f);

        // Stepping targets
        if (leftLegState == LegState.Stepping)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftFootStepTarget, 0.12f);
            Gizmos.DrawLine(character.leftFoot.transform.position, leftFootStepTarget);

            // Show step progress
            Vector3 currentPos = Vector3.Lerp(leftFootPlantedPosition, leftFootStepTarget, leftStepProgress);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentPos, 0.08f);
        }

        if (rightLegState == LegState.Stepping)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rightFootStepTarget, 0.12f);
            Gizmos.DrawLine(character.rightFoot.transform.position, rightFootStepTarget);

            Vector3 currentPos = Vector3.Lerp(rightFootPlantedPosition, rightFootStepTarget, rightStepProgress);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentPos, 0.08f);
        }

        if (character.hips != null)
        {
            Vector3 hipsPos = character.hips.transform.position;
            Vector3 hipsRight = character.hips.transform.right;

            // Ideal foot positions
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Vector3 leftIdeal = hipsPos + hipsRight * -footLateralSpacing;
            Vector3 rightIdeal = hipsPos + hipsRight * footLateralSpacing;

            Gizmos.DrawLine(leftIdeal + Vector3.up * 0.5f, leftIdeal + Vector3.down * 1f);
            Gizmos.DrawLine(rightIdeal + Vector3.up * 0.5f, rightIdeal + Vector3.down * 1f);

            // IK constraint visualization
            if (enforceIKLimits)
            {
                Vector3 leftHipJoint = hipsPos + hipsRight * -hipJointOffsetX;
                Vector3 rightHipJoint = hipsPos + hipsRight * hipJointOffsetX;

                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawWireSphere(leftHipJoint, maxLegExtension);
                Gizmos.DrawWireSphere(rightHipJoint, maxLegExtension);

                Gizmos.color = new Color(1, 0, 0, 0.2f);
                Gizmos.DrawWireSphere(leftHipJoint, minLegExtension);
                Gizmos.DrawWireSphere(rightHipJoint, minLegExtension);
            }

            // Gait indicator
            Gizmos.color = GetGaitColor(currentGait);
            Gizmos.DrawWireCube(hipsPos + Vector3.up * 1f, Vector3.one * 0.2f);
        }
    }

    private Color GetGaitColor(GaitType gait)
    {
        switch (gait)
        {
            case GaitType.Idle: return Color.gray;
            case GaitType.Walk: return Color.blue;
            case GaitType.Run: return Color.yellow;
            case GaitType.Sprint: return Color.red;
            default: return Color.white;
        }
    }
}