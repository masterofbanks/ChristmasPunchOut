using UnityEngine;

/// <summary>
/// Controls procedural walking animation for both legs
/// Alternates steps and syncs with movement speed
/// </summary>
public class ProceduralLegController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PhysicsVoxelCharacter character;
    [SerializeField] private Transform hips;

    [Header("IK Controllers")]
    [SerializeField] private FootIKController leftFootIK;
    [SerializeField] private FootIKController rightFootIK;

    [Header("Walking Parameters")]
    [SerializeField] private float strideLength = 1f;
    [SerializeField] private float stepTriggerDistance = 0.6f;
    [SerializeField] private float hipSway = 0.05f;
    [SerializeField] private float hipSwaySpeed = 5f;

    private Vector3 lastHipPosition;
    private bool leftFootStepping = false;
    private bool rightFootStepping = false;
    private float swayTime = 0f;

    public Vector3 Velocity { get; private set; }
    public float Speed => Velocity.magnitude;

    private void Start()
    {
        if (character == null)
            character = GetComponent<PhysicsVoxelCharacter>();

        if (hips == null)
            hips = character.hips;

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

        lastHipPosition = hips != null ? hips.position : transform.position;
        
        Debug.Log("✓ Procedural leg controller initialized");
    }

    private void Update()
    {
        if (hips == null) return;
        
        CalculateVelocity();
        UpdateLegs();
        AnimateHips();
    }

    private void CalculateVelocity()
    {
        Vector3 currentPos = hips.position;
        Velocity = (currentPos - lastHipPosition) / Time.deltaTime;
        lastHipPosition = currentPos;
    }

    private void UpdateLegs()
    {
        if (Speed < 0.1f) return; // Not moving

        Vector3 movementDir = Velocity.normalized;

        // Calculate desired foot positions (in front/back of hips based on movement)
        Vector3 leftDesiredPos = hips.position + movementDir * strideLength * 0.5f + hips.right * -0.3f;
        Vector3 rightDesiredPos = hips.position + movementDir * strideLength * 0.5f + hips.right * 0.3f;

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

    private void AnimateHips()
    {
        if (Speed < 0.1f || hips == null) return;

        // Sway hips side-to-side during walking
        swayTime += Time.deltaTime * hipSwaySpeed;
        float sway = Mathf.Sin(swayTime) * hipSway;

        Vector3 localOffset = new Vector3(sway, 0, 0);
        hips.localPosition += localOffset;
    }

    public void SetStrideLength(float length)
    {
        strideLength = length;
    }
}