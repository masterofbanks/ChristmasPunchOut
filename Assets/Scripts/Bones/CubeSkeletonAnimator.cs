using UnityEngine;

/// <summary>
/// Provides simple procedural animations for the cube skeleton
/// </summary>
public class CubeSkeletonAnimator : MonoBehaviour
{
    private CubeSkeletonCharacter skeleton;

    [Header("Animation Settings")]
    [SerializeField] private bool enableIdleAnimation = true;
    [SerializeField] private bool enableWalkAnimation = true;

    [Header("Walk Animation")]
    [SerializeField] private float baseWalkSpeed = 5f;
    [SerializeField] private float legSwingAmount = 30f;
    [SerializeField] private float armSwingAmount = 20f;
    [SerializeField] private bool syncAnimationToVelocity = true;
    [SerializeField] private float minSwingIntensity = 0.3f;

    [Header("Idle Animation")]
    [SerializeField] private float idleBreathingSpeed = 1f;
    [SerializeField] private float idleBreathingAmount = 2f;

    [Header("Crouch Animation")]
    [SerializeField] private float crouchSpineAngle = 22.5f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private float crouchArmAngle = -15f;

    [Header("Jump Animation")]
    [SerializeField] private float jumpPoseSpeed = 15f;
    [SerializeField] private float jumpWindupSpineAngle = 13.5f;
    [SerializeField] private float jumpLegForwardAngle = 45f;
    [SerializeField] private float jumpLegBackAngle = -30f;
    [SerializeField] private float jumpArmRaise = -90f;
    [SerializeField] private float jumpRecoverySpeed = 8f;
    [SerializeField] private float preJumpDuration = 0.08f;
    [SerializeField] private float landingDuration = 0.25f;
    [SerializeField] private bool allowAttackDuringJump = true;
    [SerializeField] private float earlyJumpTransitionTime = 0.05f;

    [Header("Attack/Punch Animation")]
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float attackSpeed = 20f;
    [SerializeField] private float punchForwardAngle = -115f;
    [SerializeField] private float punchSpineLeanAngle = 5f;
    [SerializeField] private float punchNeckLeanAngle = 3f;
    [SerializeField] private float punchRecoverySpeed = 15f;
    [SerializeField] private bool alternateHands = true;
    [SerializeField] private bool allowMovementDuringAttack = true;
    [SerializeField] private bool allowAttackDuringCrouch = true;

    [Header("Combo System")]
    [SerializeField] private float comboWindow = 0.5f;
    [SerializeField] private float comboSpeedMultiplier = 1.3f;
    [SerializeField] private int maxComboCount = 3;
    private float lastAttackTime = -999f;
    private int currentComboCount = 0;

    [Header("Momentum Response")]
    [SerializeField] private bool enableMomentumLean = true;
    [SerializeField] private float momentumLeanAmount = 5f;
    [SerializeField] private float momentumSmoothTime = 0.2f;
    [SerializeField] private float maxMomentumLean = 10f;
    private Vector3 previousVelocity;
    private float currentMomentumLean = 0f;
    private float momentumLeanVelocity = 0f;

    [Header("Blend Prediction")]
    [SerializeField] private bool enableEarlyBlending = true;
    [SerializeField] private float earlyBlendTime = 0.05f;

    [Header("Impact Feedback")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private float hitStopDuration = 0.05f;
    [SerializeField] private float hitStopTimeScale = 0.1f;
    private bool isInHitStop = false;
    private float hitStopTimer = 0f;
    private float previousTimeScale = 1f;

    [Header("Transition Settings")]
    [SerializeField] private float limbResetSpeed = 10f;
    [SerializeField] private bool smoothTransitions = true;

    private float animationTime;
    private Vector3 lastPosition;
    private bool isMoving;
    private bool wasMovingLastFrame;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;
    private bool isCrouching = false;

    private Rigidbody rb;
    private float currentVelocityMagnitude;
    private SimpleCharacterMovement movementController;

    private enum JumpState
    {
        None,
        PreJump,
        InAir,
        Landing
    }
    private JumpState jumpState = JumpState.None;
    private float jumpStateTimer = 0f;
    private bool leftLegForwardOnJump = true;
    private bool hasSyncedAnimationPhase = false; // NEW: Track if we've synced during this landing

    private bool isAttacking = false;
    private float attackTimer = 0f;
    private bool useLeftHand = true;

    private Vector3 originalChestScale;

    // Current rotations for smooth blending
    private Quaternion currentLeftLegRotation = Quaternion.identity;
    private Quaternion currentRightLegRotation = Quaternion.identity;
    private Quaternion currentLeftArmRotation = Quaternion.identity;
    private Quaternion currentRightArmRotation = Quaternion.identity;
    private Quaternion currentChestRotation = Quaternion.identity;
    private Quaternion currentSpineRotation = Quaternion.identity;
    private Quaternion currentNeckRotation = Quaternion.identity;

    // Target rotations for smooth transitions
    private Quaternion targetLeftLegRotation = Quaternion.identity;
    private Quaternion targetRightLegRotation = Quaternion.identity;
    private Quaternion targetLeftArmRotation = Quaternion.identity;
    private Quaternion targetRightArmRotation = Quaternion.identity;
    private Quaternion targetChestRotation = Quaternion.identity;
    private Quaternion targetSpineRotation = Quaternion.identity;
    private Quaternion targetNeckRotation = Quaternion.identity;

    public void Initialize(CubeSkeletonCharacter skeletonRef)
    {
        skeleton = skeletonRef;
        lastPosition = transform.position;

        rb = GetComponent<Rigidbody>();
        movementController = GetComponent<SimpleCharacterMovement>();

        if (skeleton.chest != null)
        {
            originalChestScale = skeleton.chest.localScale;
        }

        currentLeftLegRotation = skeleton.leftUpperLeg.localRotation;
        currentRightLegRotation = skeleton.rightUpperLeg.localRotation;
        currentLeftArmRotation = skeleton.leftUpperArm.localRotation;
        currentRightArmRotation = skeleton.rightUpperArm.localRotation;
        currentChestRotation = skeleton.chest.localRotation;
        currentSpineRotation = skeleton.spine.localRotation;
        currentNeckRotation = skeleton.neck != null ? skeleton.neck.localRotation : Quaternion.identity;

        wasMovingLastFrame = false;
        wasGroundedLastFrame = true;
        hasSyncedAnimationPhase = false;

        // Initialize momentum tracking
        previousVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
    }

    void Update()
    {
        // #10: Handle hit stop
        if (isInHitStop)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0f)
            {
                ExitHitStop();
            }
            return;
        }

        if (skeleton == null || skeleton.IsDead()) return;

        animationTime += Time.deltaTime;
        jumpStateTimer += Time.deltaTime;

        if (isAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackDuration)
            {
                isAttacking = false;
                attackTimer = 0f;
            }
        }

        // Calculate velocity
        if (rb != null && syncAnimationToVelocity)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            currentVelocityMagnitude = horizontalVelocity.magnitude;
        }
        else
        {
            currentVelocityMagnitude = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        }

        isMoving = currentVelocityMagnitude > 0.01f;
        lastPosition = transform.position;

        if (isGrounded && !wasGroundedLastFrame)
        {
            OnLanded();
        }

        if (!isMoving && wasMovingLastFrame)
        {
            ResetLimbsToNeutral();
        }

        // #7: Calculate momentum-based lean (FIXED: Only use local forward direction)
        if (enableMomentumLean && rb != null && isGrounded) // Only apply when grounded
        {
            Vector3 currentVel = rb.linearVelocity;
            Vector3 acceleration = (currentVel - previousVelocity) / Time.deltaTime;

            // Project acceleration onto character's forward direction (local Z-axis)
            float forwardAccel = Vector3.Dot(acceleration, transform.forward);

            // Calculate target lean angle - negative because we lean forward when accelerating forward
            float targetMomentumLean = -forwardAccel * momentumLeanAmount;

            // CLAMP to prevent extreme values
            targetMomentumLean = Mathf.Clamp(targetMomentumLean, -maxMomentumLean, maxMomentumLean);

            // Smooth the lean value
            currentMomentumLean = Mathf.SmoothDamp(
                currentMomentumLean,
                targetMomentumLean,
                ref momentumLeanVelocity,
                momentumSmoothTime
            );

            previousVelocity = currentVel;
        }
        else if (!isGrounded || !enableMomentumLean)
        {
            // Decay momentum lean when not grounded or disabled
            currentMomentumLean = Mathf.SmoothDamp(currentMomentumLean, 0f, ref momentumLeanVelocity, momentumSmoothTime);
        }

        // Base animation (lowest priority)
        if (jumpState != JumpState.None)
        {
            UpdateJumpTargets();
        }
        else if (isCrouching)
        {
            UpdateCrouchTargets();
        }
        else if (isMoving && enableWalkAnimation)
        {
            UpdateWalkTargets();
        }
        else if (enableIdleAnimation)
        {
            UpdateIdleTargets();
        }
        else
        {
            SetNeutralTargets();
        }

        // #7: Apply momentum lean to spine (additive) - ONLY if significant
        if (enableMomentumLean && Mathf.Abs(currentMomentumLean) > 0.5f) // Threshold increased from 0.1f
        {
            Vector3 spineEuler = targetSpineRotation.eulerAngles;
            // Normalize to -180 to 180
            if (spineEuler.x > 180f) spineEuler.x -= 360f;
            if (spineEuler.y > 180f) spineEuler.y -= 360f;
            if (spineEuler.z > 180f) spineEuler.z -= 360f;

            // Add momentum lean ONLY to X-axis (pitch)
            spineEuler.x += currentMomentumLean;

            // Clamp the final spine angle to reasonable values
            spineEuler.x = Mathf.Clamp(spineEuler.x, -45f, 45f);

            targetSpineRotation = Quaternion.Euler(spineEuler.x, spineEuler.y, spineEuler.z);
        }

        // OVERLAY: Attack animation on top of base
        if (isAttacking)
        {
            ApplyAttackOverlay();
        }

        ApplySmoothRotations();

        wasMovingLastFrame = isMoving;
        wasGroundedLastFrame = isGrounded;
    }

    private void ApplyAttackOverlay()
    {
        float progress = attackTimer / attackDuration;

        float effectiveProgress = progress;
        if (currentComboCount > 0)
        {
            effectiveProgress = Mathf.Min(1f, progress * comboSpeedMultiplier);
        }

        float punchCurve;
        if (effectiveProgress < 0.3f)
        {
            punchCurve = effectiveProgress / 0.3f;
        }
        else
        {
            punchCurve = 1f - ((effectiveProgress - 0.3f) / 0.7f);
        }

        // OVERRIDE: Arms only
        if (useLeftHand)
        {
            targetLeftArmRotation = Quaternion.Euler(punchForwardAngle * punchCurve, 0, 0);
        }
        else
        {
            targetRightArmRotation = Quaternion.Euler(punchForwardAngle * punchCurve, 0, 0);
        }

        // ADDITIVE: Spine lean
        Vector3 baseSpineEuler = targetSpineRotation.eulerAngles;
        if (baseSpineEuler.x > 180f) baseSpineEuler.x -= 360f;

        float finalSpineAngle = baseSpineEuler.x + (punchSpineLeanAngle * punchCurve);
        targetSpineRotation = Quaternion.Euler(finalSpineAngle, baseSpineEuler.y, baseSpineEuler.z);

        // ADDITIVE: Neck follows spine lean
        Vector3 baseNeckEuler = targetNeckRotation.eulerAngles;
        if (baseNeckEuler.x > 180f) baseNeckEuler.x -= 360f;

        float finalNeckAngle = baseNeckEuler.x + (punchNeckLeanAngle * punchCurve);
        targetNeckRotation = Quaternion.Euler(finalNeckAngle, baseNeckEuler.y, baseNeckEuler.z);
    }

    private void UpdateWalkTargets()
    {
        float velocityRatio = GetVelocityRatio();
        float currentSwingIntensity = GetSwingIntensity(velocityRatio);
        float currentAnimSpeed = baseWalkSpeed * velocityRatio;

        float wave = Mathf.Sin(animationTime * currentAnimSpeed);

        targetLeftLegRotation = Quaternion.Euler(wave * legSwingAmount * currentSwingIntensity, 0, 0);
        targetRightLegRotation = Quaternion.Euler(-wave * legSwingAmount * currentSwingIntensity, 0, 0);
        targetLeftArmRotation = Quaternion.Euler(-wave * armSwingAmount * currentSwingIntensity, 0, 0);
        targetRightArmRotation = Quaternion.Euler(wave * armSwingAmount * currentSwingIntensity, 0, 0);
        targetChestRotation = Quaternion.Euler(0, wave * 5f * currentSwingIntensity, 0);
        targetSpineRotation = Quaternion.identity;
        targetNeckRotation = Quaternion.identity;

        skeleton.chest.localScale = originalChestScale;
    }

    private void UpdateIdleTargets()
    {
        float breath = Mathf.Sin(animationTime * idleBreathingSpeed) * 0.5f + 0.5f;

        float breathScale = 1f + (breath * 0.05f * idleBreathingAmount);
        skeleton.chest.localScale = originalChestScale * breathScale;

        skeleton.head.localRotation = Quaternion.Euler((breath - 0.5f) * 4f, 0, 0);

        targetLeftLegRotation = Quaternion.identity;
        targetRightLegRotation = Quaternion.identity;
        targetLeftArmRotation = Quaternion.identity;
        targetRightArmRotation = Quaternion.identity;
        targetChestRotation = Quaternion.identity;
        targetSpineRotation = Quaternion.identity;
        targetNeckRotation = Quaternion.identity;
    }

    private void UpdateCrouchTargets()
    {
        targetSpineRotation = Quaternion.Euler(crouchSpineAngle, 0, 0);
        targetNeckRotation = Quaternion.identity;
        targetLeftLegRotation = Quaternion.identity;
        targetRightLegRotation = Quaternion.identity;

        if (isMoving)
        {
            float velocityRatio = GetVelocityRatio();
            float currentSwingIntensity = GetSwingIntensity(velocityRatio) * 0.6f;
            float currentAnimSpeed = baseWalkSpeed * velocityRatio * 0.7f;

            float wave = Mathf.Sin(animationTime * currentAnimSpeed);
            targetLeftLegRotation = Quaternion.Euler(wave * legSwingAmount * currentSwingIntensity, 0, 0);
            targetRightLegRotation = Quaternion.Euler(-wave * legSwingAmount * currentSwingIntensity, 0, 0);

            targetLeftArmRotation = Quaternion.Euler(-wave * armSwingAmount * currentSwingIntensity * 0.5f + crouchArmAngle, 0, 0);
            targetRightArmRotation = Quaternion.Euler(wave * armSwingAmount * currentSwingIntensity * 0.5f + crouchArmAngle, 0, 0);
        }
        else
        {
            targetLeftArmRotation = Quaternion.Euler(crouchArmAngle, 0, 0);
            targetRightArmRotation = Quaternion.Euler(crouchArmAngle, 0, 0);
        }

        targetChestRotation = Quaternion.identity;
        skeleton.chest.localScale = originalChestScale;
    }

    private void UpdateJumpTargets()
    {
        if (!isCrouching)
        {
            targetSpineRotation = Quaternion.identity;
        }

        switch (jumpState)
        {
            case JumpState.PreJump:
                UpdatePreJumpTargets();

                if (jumpStateTimer > preJumpDuration - earlyJumpTransitionTime)
                {
                    jumpState = JumpState.InAir;
                    jumpStateTimer = 0f;
                }
                break;

            case JumpState.InAir:
                UpdateInAirTargets();
                break;

            case JumpState.Landing:
                UpdateLandingTargets();

                // FIXED: Start blending IMMEDIATELY when landing, not just at the end
                if (isMoving)
                {
                    // Calculate blend factor across the ENTIRE landing duration
                    float blendFactor = Mathf.Clamp01(jumpStateTimer / landingDuration);
                    BlendTowardWalkPose(blendFactor);
                }
                else
                {
                    // Blend to idle across entire landing duration
                    float blendFactor = Mathf.Clamp01(jumpStateTimer / landingDuration);
                    BlendTowardIdlePose(blendFactor);
                }

                if (jumpStateTimer > landingDuration)
                {
                    jumpState = JumpState.None;
                    jumpStateTimer = 0f;
                }
                break;
        }
    }

    /// <summary>
    /// Synchronizes animationTime to match the current leg positions
    /// Prevents "shuffle step" by making walk cycle continue from where legs are now
    /// </summary>
    private void SyncAnimationPhaseToLegs()
    {
        // Get current leg angles
        float leftLegAngle = currentLeftLegRotation.eulerAngles.x;
        float rightLegAngle = currentRightLegRotation.eulerAngles.x;

        // Normalize to -180 to 180
        if (leftLegAngle > 180f) leftLegAngle -= 360f;
        if (rightLegAngle > 180f) rightLegAngle -= 360f;

        // Calculate what the average leg swing is
        float averageLegSwing = (leftLegAngle - rightLegAngle) / 2f;

        // Get velocity parameters for current walk cycle
        float velocityRatio = GetVelocityRatio();
        float currentSwingIntensity = GetSwingIntensity(velocityRatio);
        float currentAnimSpeed = baseWalkSpeed * velocityRatio;

        // Calculate what the target swing should be based on full intensity
        float targetMaxSwing = legSwingAmount * currentSwingIntensity;

        if (targetMaxSwing > 0.1f) // Avoid division by zero
        {
            // Calculate what sine wave value would produce current leg positions
            float normalizedSwing = Mathf.Clamp(averageLegSwing / targetMaxSwing, -1f, 1f);

            // Calculate the phase (angle in the sine wave) that produces this value
            float phase = Mathf.Asin(normalizedSwing);

            // If right leg is forward (negative swing), we're in the second half of the cycle
            if (rightLegAngle > leftLegAngle)
            {
                phase = Mathf.PI - phase;
            }

            // Convert phase back to animation time
            if (currentAnimSpeed > 0.01f)
            {
                animationTime = phase / currentAnimSpeed;
            }
        }
    }

    private void BlendTowardWalkPose(float blendFactor)
    {
        float velocityRatio = GetVelocityRatio();
        float currentSwingIntensity = GetSwingIntensity(velocityRatio);
        float currentAnimSpeed = baseWalkSpeed * velocityRatio;
        float wave = Mathf.Sin(animationTime * currentAnimSpeed);

        Quaternion walkLeftLeg = Quaternion.Euler(wave * legSwingAmount * currentSwingIntensity, 0, 0);
        Quaternion walkRightLeg = Quaternion.Euler(-wave * legSwingAmount * currentSwingIntensity, 0, 0);

        targetLeftLegRotation = Quaternion.Slerp(targetLeftLegRotation, walkLeftLeg, blendFactor);
        targetRightLegRotation = Quaternion.Slerp(targetRightLegRotation, walkRightLeg, blendFactor);
    }

    private void BlendTowardIdlePose(float blendFactor)
    {
        targetLeftLegRotation = Quaternion.Slerp(targetLeftLegRotation, Quaternion.identity, blendFactor);
        targetRightLegRotation = Quaternion.Slerp(targetRightLegRotation, Quaternion.identity, blendFactor);
    }

    private void UpdatePreJumpTargets()
    {
        if (!isCrouching)
        {
            targetSpineRotation = Quaternion.Euler(jumpWindupSpineAngle, 0, 0);
        }
        targetNeckRotation = Quaternion.Euler(jumpWindupSpineAngle * 0.3f, 0, 0);

        if (!isAttacking)
        {
            targetLeftArmRotation = Quaternion.Euler(30f, 0, 0);
            targetRightArmRotation = Quaternion.Euler(30f, 0, 0);
        }

        targetChestRotation = Quaternion.Euler(10f, 0, 0);
    }

    private void UpdateInAirTargets()
    {
        if (leftLegForwardOnJump)
        {
            targetLeftLegRotation = Quaternion.Euler(jumpLegForwardAngle, 0, 0);
            targetRightLegRotation = Quaternion.Euler(jumpLegBackAngle, 0, 0);
        }
        else
        {
            targetLeftLegRotation = Quaternion.Euler(jumpLegBackAngle, 0, 0);
            targetRightLegRotation = Quaternion.Euler(jumpLegForwardAngle, 0, 0);
        }

        if (!isCrouching)
        {
            targetSpineRotation = Quaternion.identity;
        }
        targetNeckRotation = Quaternion.identity;

        if (!isAttacking)
        {
            targetLeftArmRotation = Quaternion.Euler(jumpArmRaise * 0.7f, 0, 0);
            targetRightArmRotation = Quaternion.Euler(jumpArmRaise, 0, 0);
        }

        targetChestRotation = Quaternion.Euler(0, 5f, 0);
    }

    private void UpdateLandingTargets()
    {
        // Only reset legs if NOT moving
        if (!isMoving)
        {
            targetLeftLegRotation = Quaternion.identity;
            targetRightLegRotation = Quaternion.identity;
        }

        targetNeckRotation = Quaternion.identity;

        if (!isAttacking)
        {
            targetLeftArmRotation = Quaternion.identity;
            targetRightArmRotation = Quaternion.identity;
        }

        targetChestRotation = Quaternion.identity;
    }

    private float GetVelocityRatio()
    {
        if (!syncAnimationToVelocity || movementController == null)
        {
            return 1f;
        }

        float expectedSpeed = movementController.moveSpeed;

        if (isCrouching)
        {
            expectedSpeed *= 0.5f;
        }

        if (expectedSpeed < 0.01f) return 0f;

        float ratio = Mathf.Clamp01(currentVelocityMagnitude / expectedSpeed);
        return ratio;
    }

    private float GetSwingIntensity(float velocityRatio)
    {
        return Mathf.Lerp(minSwingIntensity, 1f, velocityRatio);
    }

    private void ApplySmoothRotations()
    {
        float interpSpeed;

        if (isAttacking)
        {
            interpSpeed = currentComboCount > 0 ? attackSpeed * comboSpeedMultiplier : attackSpeed;
        }
        else if (jumpState == JumpState.PreJump)
        {
            interpSpeed = jumpPoseSpeed;
        }
        else if (jumpState == JumpState.InAir)
        {
            interpSpeed = jumpPoseSpeed * 0.8f;
        }
        else if (jumpState == JumpState.Landing)
        {
            interpSpeed = jumpRecoverySpeed;
        }
        else if (isCrouching)
        {
            interpSpeed = crouchTransitionSpeed;
        }
        else if (isMoving)
        {
            interpSpeed = 25f;
        }
        else
        {
            interpSpeed = limbResetSpeed;
        }

        float smoothFactor = Mathf.Clamp01(interpSpeed * Time.deltaTime);

        currentLeftLegRotation = Quaternion.Slerp(currentLeftLegRotation, targetLeftLegRotation, smoothFactor);
        currentRightLegRotation = Quaternion.Slerp(currentRightLegRotation, targetRightLegRotation, smoothFactor);
        currentLeftArmRotation = Quaternion.Slerp(currentLeftArmRotation, targetLeftArmRotation, smoothFactor);
        currentRightArmRotation = Quaternion.Slerp(currentRightArmRotation, targetRightArmRotation, smoothFactor);
        currentChestRotation = Quaternion.Slerp(currentChestRotation, targetChestRotation, smoothFactor);
        currentSpineRotation = Quaternion.Slerp(currentSpineRotation, targetSpineRotation, smoothFactor);
        currentNeckRotation = Quaternion.Slerp(currentNeckRotation, targetNeckRotation, smoothFactor);

        skeleton.leftUpperLeg.localRotation = currentLeftLegRotation;
        skeleton.rightUpperLeg.localRotation = currentRightLegRotation;
        skeleton.leftUpperArm.localRotation = currentLeftArmRotation;
        skeleton.rightUpperArm.localRotation = currentRightArmRotation;
        skeleton.chest.localRotation = currentChestRotation;
        skeleton.spine.localRotation = currentSpineRotation;

        if (skeleton.neck != null)
        {
            skeleton.neck.localRotation = currentNeckRotation;
        }
    }

    private void ResetLimbsToNeutral()
    {
        SetNeutralTargets();
    }

    private void SetNeutralTargets()
    {
        targetLeftLegRotation = Quaternion.identity;
        targetRightLegRotation = Quaternion.identity;
        targetLeftArmRotation = Quaternion.identity;
        targetRightArmRotation = Quaternion.identity;
        targetChestRotation = Quaternion.identity;
        targetNeckRotation = Quaternion.identity;
        if (!isCrouching)
        {
            targetSpineRotation = Quaternion.identity;
        }
    }

    public void TriggerAttack()
    {
        if (skeleton == null || skeleton.IsDead()) return;

        if (isAttacking) return;

        bool isCombo = (Time.time - lastAttackTime) < comboWindow;

        if (isCombo && currentComboCount < maxComboCount)
        {
            currentComboCount++;
            Debug.Log($"[Animator] 💥 COMBO x{currentComboCount}!");
        }
        else
        {
            currentComboCount = 0;
        }

        isAttacking = true;
        attackTimer = 0f;
        lastAttackTime = Time.time;

        if (alternateHands)
        {
            useLeftHand = !useLeftHand;
        }

        Debug.Log($"[Animator] 👊 Attack triggered! Using {(useLeftHand ? "LEFT" : "RIGHT")} hand");
    }

    public void TriggerJump()
    {
        DetermineJumpLegConfiguration();

        jumpState = JumpState.PreJump;
        jumpStateTimer = 0f;
        hasSyncedAnimationPhase = false; // Reset sync flag when starting new jump
    }

    private void DetermineJumpLegConfiguration()
    {
        float leftLegAngle = currentLeftLegRotation.eulerAngles.x;
        float rightLegAngle = currentRightLegRotation.eulerAngles.x;

        if (leftLegAngle > 180f) leftLegAngle -= 360f;
        if (rightLegAngle > 180f) rightLegAngle -= 360f;

        float leftLegDistanceToForward = Mathf.Abs(jumpLegForwardAngle - leftLegAngle);
        float rightLegDistanceToForward = Mathf.Abs(jumpLegForwardAngle - rightLegAngle);

        leftLegForwardOnJump = leftLegDistanceToForward < rightLegDistanceToForward;
    }

    public void TriggerDeath(Vector3? explosionOrigin = null)
    {
        if (skeleton != null && !skeleton.IsDead())
        {
            skeleton.TriggerDeath(explosionOrigin);
        }
    }

    public void TriggerHitStop()
    {
        if (!enableHitStop || isInHitStop) return;

        isInHitStop = true;
        hitStopTimer = hitStopDuration;
        previousTimeScale = Time.timeScale;
        Time.timeScale = hitStopTimeScale;

        Debug.Log("[Animator] 💥 HIT STOP!");
    }

    public void ExitHitStop()
    {
        if (!isInHitStop) return;

        isInHitStop = false;
        Time.timeScale = previousTimeScale;
    }

    private void OnLanded()
    {
        if (jumpState == JumpState.InAir)
        {
            jumpState = JumpState.Landing;
            jumpStateTimer = 0f;

            if (enableHitStop)
            {
                TriggerHitStop();
            }
        }
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    public void SetCrouching(bool crouching)
    {
        isCrouching = crouching;
    }

    public void ResetPose()
    {
        if (skeleton != null)
        {
            skeleton.ResetToTPose();
            if (skeleton.chest != null)
            {
                skeleton.chest.localScale = originalChestScale;
            }

            SetNeutralTargets();
            targetSpineRotation = Quaternion.identity;
            targetNeckRotation = Quaternion.identity;

            currentLeftLegRotation = Quaternion.identity;
            currentRightLegRotation = Quaternion.identity;
            currentLeftArmRotation = Quaternion.identity;
            currentRightArmRotation = Quaternion.identity;
            currentChestRotation = Quaternion.identity;
            currentSpineRotation = Quaternion.identity;
            currentNeckRotation = Quaternion.identity;

            skeleton.spine.localRotation = Quaternion.identity;
            if (skeleton.neck != null)
            {
                skeleton.neck.localRotation = Quaternion.identity;
            }

            jumpState = JumpState.None;
            isCrouching = false;
            isAttacking = false;
            attackTimer = 0f;
            currentComboCount = 0;
            currentMomentumLean = 0f;
            momentumLeanVelocity = 0f;
            hasSyncedAnimationPhase = false;
        }
    }

    public bool IsMoving() => isMoving;
    public bool IsCrouching() => isCrouching;
    public bool IsJumping() => jumpState != JumpState.None;
    public bool IsInAir() => jumpState == JumpState.InAir;
    public bool IsDead() => skeleton != null && skeleton.IsDead();
    public bool IsAttacking() => isAttacking;
    public int GetComboCount() => currentComboCount;
    public bool IsInHitStop() => isInHitStop;
}