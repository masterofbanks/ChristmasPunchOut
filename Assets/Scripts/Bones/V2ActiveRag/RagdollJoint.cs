using UnityEngine;

/// <summary>
/// PASSIVE joint marker - does NOT apply forces
/// Balance controller has full control
/// </summary>
[RequireComponent(typeof(ConfigurableJoint))]
public class RagdollJoint : MonoBehaviour
{
    [Header("Target Pose (Read by Balance Controller)")]
    public Quaternion targetLocalRotation = Quaternion.identity;

    private ConfigurableJoint joint;
    private Rigidbody rb;

    private void Awake()
    {
        joint = GetComponent<ConfigurableJoint>();
        rb = GetComponent<Rigidbody>();
        targetLocalRotation = transform.localRotation;
    }

    private void Start()
    {
        // CRITICAL: Disable all drives - balance controller will handle everything
        if (joint != null && joint.connectedBody != null)
        {
            JointDrive disabledDrive = new JointDrive
            {
                positionSpring = 0,
                positionDamper = 0,
                maximumForce = 0
            };

            joint.slerpDrive = disabledDrive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.targetRotation = Quaternion.identity;

            Debug.Log($"[RagdollJoint] {name} - drives DISABLED (passive mode)");
        }
    }

    // NO FixedUpdate - component is purely a data holder

    public void SetTargetRotation(Quaternion target)
    {
        targetLocalRotation = target;
    }

    public Rigidbody GetRigidbody() => rb;
    public ConfigurableJoint GetJoint() => joint;
}