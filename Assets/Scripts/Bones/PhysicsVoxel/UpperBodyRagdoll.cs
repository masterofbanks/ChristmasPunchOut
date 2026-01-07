using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Physics-based ragdoll for upper body
/// DISABLED BY DEFAULT - Can be enabled for death/ragdoll effects
/// </summary>
public class UpperBodyRagdoll : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PhysicsVoxelCharacter character;

    [Header("Physics Settings")]
    [SerializeField] private float jointSpring = 500f;
    [SerializeField] private float jointDamper = 50f;
    [SerializeField] private float maxJointForce = 100f;
    [SerializeField] private float boneMass = 0.5f;

    [Header("Pose Following")]
    [SerializeField] private float poseFollowStrength = 100f;
    [SerializeField] private bool enablePoseFollow = true;

    [Header("Enable/Disable")]
    [SerializeField] private bool ragdollEnabled = false; // CHANGED: Default to false

    private Dictionary<Transform, Rigidbody> boneRigidbodies = new Dictionary<Transform, Rigidbody>();
    private Dictionary<Transform, ConfigurableJoint> boneJoints = new Dictionary<Transform, ConfigurableJoint>();
    private Dictionary<Transform, Quaternion> targetPoses = new Dictionary<Transform, Quaternion>();

    private bool isInitialized = false;

    private void Start()
    {
        // Don't auto-initialize - wait for manual call
        if (ragdollEnabled)
        {
            Initialize();
        }
    }

    public void Initialize()
    {
        if (isInitialized) return;

        if (character == null)
            character = GetComponent<PhysicsVoxelCharacter>();

        if (!ragdollEnabled)
        {
            Debug.Log("? UpperBodyRagdoll: Initialization skipped (ragdoll disabled)");
            return;
        }

        SetupRagdoll();
        isInitialized = true;

        Debug.Log("? Upper body ragdoll initialized");
    }

    private void SetupRagdoll()
    {
        Transform[] upperBodyBones = character.GetUpperBodyBones();

        foreach (Transform bone in upperBodyBones)
        {
            if (bone == null) continue;

            // Add Rigidbody
            Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = boneMass;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.None;

            boneRigidbodies[bone] = rb;

            // Add BoxCollider for bone collision
            BoxCollider collider = bone.gameObject.AddComponent<BoxCollider>();
            Renderer renderer = bone.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                collider.size = renderer.bounds.size / bone.lossyScale.x;
                collider.center = renderer.bounds.center - bone.position;
            }

            // Store initial pose as target
            targetPoses[bone] = bone.localRotation;
        }

        // Create joints to connect bones
        CreateJoint(character.spine, character.hips);
        CreateJoint(character.chest, character.spine);
        CreateJoint(character.neck, character.chest);
        CreateJoint(character.head, character.neck);

        CreateJoint(character.leftShoulder, character.chest);
        CreateJoint(character.leftUpperArm, character.leftShoulder);
        CreateJoint(character.leftForearm, character.leftUpperArm);
        CreateJoint(character.leftHand, character.leftForearm);

        CreateJoint(character.rightShoulder, character.chest);
        CreateJoint(character.rightUpperArm, character.rightShoulder);
        CreateJoint(character.rightForearm, character.rightUpperArm);
        CreateJoint(character.rightHand, character.rightForearm);
    }

    private void CreateJoint(Transform child, Transform parent)
    {
        if (child == null || parent == null) return;

        Rigidbody childRb = boneRigidbodies[child];
        Rigidbody parentRb = boneRigidbodies.ContainsKey(parent) ? boneRigidbodies[parent] : parent.GetComponent<Rigidbody>();

        if (parentRb == null)
        {
            // Parent needs rigidbody (hips) - make it kinematic
            parentRb = parent.gameObject.AddComponent<Rigidbody>();
            parentRb.isKinematic = true;
        }

        ConfigurableJoint joint = child.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = parentRb;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = child.localPosition;

        // Lock position
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;

        // Allow limited rotation
        joint.angularXMotion = ConfigurableJointMotion.Limited;
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularZMotion = ConfigurableJointMotion.Limited;

        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = 45f;
        joint.lowAngularXLimit = limit;
        joint.highAngularXLimit = limit;
        joint.angularYLimit = limit;
        joint.angularZLimit = limit;

        // Configure spring/damper
        JointDrive drive = new JointDrive();
        drive.positionSpring = jointSpring;
        drive.positionDamper = jointDamper;
        drive.maximumForce = maxJointForce;

        joint.slerpDrive = drive;

        boneJoints[child] = joint;
    }

    private void FixedUpdate()
    {
        if (!isInitialized || !enablePoseFollow || !ragdollEnabled) return;

        // Apply forces to follow target poses
        foreach (var kvp in targetPoses)
        {
            Transform bone = kvp.Key;
            Quaternion targetRot = kvp.Value;

            if (!boneRigidbodies.ContainsKey(bone)) continue;

            Rigidbody rb = boneRigidbodies[bone];

            // Calculate rotation difference
            Quaternion currentRot = bone.localRotation;
            Quaternion rotationDelta = targetRot * Quaternion.Inverse(currentRot);

            // Convert to angle-axis
            float angle;
            Vector3 axis;
            rotationDelta.ToAngleAxis(out angle, out axis);

            // Normalize angle to -180 to 180
            if (angle > 180f) angle -= 360f;

            // Apply torque to rotate towards target
            if (angle != 0)
            {
                Vector3 torque = axis * (angle * Mathf.Deg2Rad) * poseFollowStrength;
                rb.AddTorque(torque, ForceMode.Force);
            }
        }
    }

    /// <summary>
    /// Enable the ragdoll (e.g., for death)
    /// </summary>
    public void EnableRagdoll()
    {
        if (!isInitialized)
        {
            ragdollEnabled = true;
            Initialize();
        }
        else
        {
            SetRagdollEnabled(true);
        }
    }

    /// <summary>
    /// Disable the ragdoll
    /// </summary>
    public void DisableRagdoll()
    {
        SetRagdollEnabled(false);
    }

    /// <summary>
    /// Update target pose for a specific bone
    /// </summary>
    public void SetTargetPose(Transform bone, Quaternion localRotation)
    {
        if (targetPoses.ContainsKey(bone))
        {
            targetPoses[bone] = localRotation;
        }
    }

    /// <summary>
    /// Enable/disable ragdoll physics
    /// </summary>
    public void SetRagdollEnabled(bool enabled)
    {
        ragdollEnabled = enabled;
        
        foreach (var rb in boneRigidbodies.Values)
        {
            rb.isKinematic = !enabled;
        }
    }

    private void OnDrawGizmos()
    {
        if (!isInitialized || !ragdollEnabled) return;

        // Draw joint connections
        Gizmos.color = Color.red;
        foreach (var kvp in boneJoints)
        {
            Transform bone = kvp.Key;
            ConfigurableJoint joint = kvp.Value;

            if (joint.connectedBody != null)
            {
                Gizmos.DrawLine(bone.position, joint.connectedBody.position);
            }
        }
    }
}