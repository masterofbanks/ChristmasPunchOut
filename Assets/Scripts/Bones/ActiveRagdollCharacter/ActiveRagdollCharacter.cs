using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Active ragdoll character built from voxel cubes
/// Uses physics-based balance and procedural IK for feet
/// </summary>
public class ActiveRagdollCharacter : MonoBehaviour
{
    [Header("=== SKELETON BONES ===")]
    public RagdollBone hips;
    public RagdollBone spine;
    public RagdollBone chest;
    public RagdollBone neck;
    public RagdollBone head;

    public RagdollBone leftUpperLeg;
    public RagdollBone leftLowerLeg;
    public RagdollBone leftFoot;

    public RagdollBone rightUpperLeg;
    public RagdollBone rightLowerLeg;
    public RagdollBone rightFoot;

    public RagdollBone leftShoulder;
    public RagdollBone leftUpperArm;
    public RagdollBone leftForearm;
    public RagdollBone leftHand;

    public RagdollBone rightShoulder;
    public RagdollBone rightUpperArm;
    public RagdollBone rightForearm;
    public RagdollBone rightHand;

    [Header("Build Settings")]
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private float voxelSize = 0.1f;
    [SerializeField] private Material voxelMaterial;
    [SerializeField] private bool useCombinedMesh = true;

    [Header("Physics Settings")]
    [SerializeField] private float boneMass = 1f;
    [SerializeField] private float jointSpring = 5000f;
    [SerializeField] private float jointDamper = 500f;

    private Dictionary<string, RagdollBone> bones = new Dictionary<string, RagdollBone>();
    private bool isBuilt = false;

    public void BuildCharacter()
    {
        if (isBuilt)
        {
            Debug.LogWarning("Character already built!");
            return;
        }

        // Create skeleton hierarchy
        CreateBoneHierarchy();

        // Build voxel visuals for each bone
        BuildVoxelVisuals();

        // Setup physics joints
        SetupPhysicsJoints();

        isBuilt = true;
        Debug.Log($"✓ Active ragdoll character built at {transform.position}");
    }

    private void CreateBoneHierarchy()
    {
        float unit = cubeUnit;

        // Hips (root - no parent)
        hips = CreateBone("Hips", Vector3.zero, null,
            new Vector3(unit * 1.5f, unit * 0.8f, unit), boneMass * 3f);

        // Spine
        spine = CreateBone("Spine", new Vector3(0, unit * 1.2f, 0), hips,
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 0.8f), boneMass * 2f);

        // Chest
        chest = CreateBone("Chest", new Vector3(0, unit * 1.3f, 0), spine,
            new Vector3(unit * 1.8f, unit * 1.0f, unit), boneMass * 2.5f);

        // Neck
        neck = CreateBone("Neck", new Vector3(0, unit * 1.0f, 0), chest,
            new Vector3(unit * 0.6f, unit * 0.5f, unit * 0.6f), boneMass * 0.5f);

        // Head
        head = CreateBone("Head", new Vector3(0, unit * 0.8f, 0), neck,
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 1.2f), boneMass * 1f);

        // Left Leg
        leftUpperLeg = CreateBone("LeftUpperLeg", new Vector3(-unit * 0.5f, -unit * 1.0f, 0), hips,
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f), boneMass * 1.5f);
        leftLowerLeg = CreateBone("LeftLowerLeg", new Vector3(0, -unit * 1.6f, 0), leftUpperLeg,
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f), boneMass * 1f);
        leftFoot = CreateBone("LeftFoot", new Vector3(0, -unit * 1.0f, unit * 0.3f), leftLowerLeg,
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f), boneMass * 0.5f);

        // Right Leg
        rightUpperLeg = CreateBone("RightUpperLeg", new Vector3(unit * 0.5f, -unit * 1.0f, 0), hips,
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f), boneMass * 1.5f);
        rightLowerLeg = CreateBone("RightLowerLeg", new Vector3(0, -unit * 1.6f, 0), rightUpperLeg,
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f), boneMass * 1f);
        rightFoot = CreateBone("RightFoot", new Vector3(0, -unit * 1.0f, unit * 0.3f), rightLowerLeg,
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f), boneMass * 0.5f);

        // Left Arm
        leftShoulder = CreateBone("LeftShoulder", new Vector3(-unit * 1.15f, unit * 0.4f, 0), chest,
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f), boneMass * 0.3f);
        leftUpperArm = CreateBone("LeftUpperArm", new Vector3(0, -unit * 0.85f, 0), leftShoulder,
            new Vector3(unit * 0.6f, unit * 1.2f, unit * 0.6f), boneMass * 0.8f);
        leftForearm = CreateBone("LeftForearm", new Vector3(0, -unit * 1.15f, 0), leftUpperArm,
            new Vector3(unit * 0.5f, unit * 1.1f, unit * 0.5f), boneMass * 0.6f);
        leftHand = CreateBone("LeftHand", new Vector3(0, -unit * 0.85f, 0), leftForearm,
            new Vector3(unit * 0.6f, unit * 0.6f, unit * 0.6f), boneMass * 0.3f);

        // Right Arm
        rightShoulder = CreateBone("RightShoulder", new Vector3(unit * 1.15f, unit * 0.4f, 0), chest,
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f), boneMass * 0.3f);
        rightUpperArm = CreateBone("RightUpperArm", new Vector3(0, -unit * 0.85f, 0), rightShoulder,
            new Vector3(unit * 0.6f, unit * 1.2f, unit * 0.6f), boneMass * 0.8f);
        rightForearm = CreateBone("RightForearm", new Vector3(0, -unit * 1.15f, 0), rightUpperArm,
            new Vector3(unit * 0.5f, unit * 1.1f, unit * 0.5f), boneMass * 0.6f);
        rightHand = CreateBone("RightHand", new Vector3(0, -unit * 0.85f, 0), rightForearm,
            new Vector3(unit * 0.6f, unit * 0.6f, unit * 0.6f), boneMass * 0.3f);
    }

    private RagdollBone CreateBone(string name, Vector3 localPos, RagdollBone parent, Vector3 size, float mass)
    {
        GameObject boneObj = new GameObject(name);

        if (parent != null)
        {
            boneObj.transform.SetParent(parent.transform);
            boneObj.transform.localPosition = localPos;
        }
        else
        {
            boneObj.transform.SetParent(transform);
            boneObj.transform.localPosition = localPos;
        }

        boneObj.transform.localRotation = Quaternion.identity;

        RagdollBone bone = boneObj.AddComponent<RagdollBone>();
        bone.Initialize(size, mass, voxelSize, voxelMaterial, useCombinedMesh);

        bones[name] = bone;
        return bone;
    }

    private void BuildVoxelVisuals()
    {
        foreach (var bone in bones.Values)
        {
            bone.BuildVoxels();
        }
    }

    private void SetupPhysicsJoints()
    {
        // Spine joints
        CreateJoint(spine, hips, new Vector3(-45, -30, -30), new Vector3(45, 30, 30));
        CreateJoint(chest, spine, new Vector3(-20, -20, -20), new Vector3(20, 20, 20));
        CreateJoint(neck, chest, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));
        CreateJoint(head, neck, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));

        // Leg joints (hips)
        CreateJoint(leftUpperLeg, hips, new Vector3(-90, -45, -30), new Vector3(30, 45, 30));
        CreateJoint(rightUpperLeg, hips, new Vector3(-90, -45, -30), new Vector3(30, 45, 30));

        // Knee joints
        CreateJoint(leftLowerLeg, leftUpperLeg, new Vector3(0, -10, -10), new Vector3(120, 10, 10));
        CreateJoint(rightLowerLeg, rightUpperLeg, new Vector3(0, -10, -10), new Vector3(120, 10, 10));

        // Ankle joints
        CreateJoint(leftFoot, leftLowerLeg, new Vector3(-30, -20, -20), new Vector3(30, 20, 20));
        CreateJoint(rightFoot, rightLowerLeg, new Vector3(-30, -20, -20), new Vector3(30, 20, 20));

        // Arm joints
        CreateJoint(leftShoulder, chest, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));
        CreateJoint(rightShoulder, chest, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));

        CreateJoint(leftUpperArm, leftShoulder, new Vector3(-90, -45, -80), new Vector3(90, 45, 80));
        CreateJoint(rightUpperArm, rightShoulder, new Vector3(-90, -45, -80), new Vector3(90, 45, 80));

        // Elbow joints
        CreateJoint(leftForearm, leftUpperArm, new Vector3(-120, -10, -10), new Vector3(0, 10, 10));
        CreateJoint(rightForearm, rightUpperArm, new Vector3(-120, -10, -10), new Vector3(0, 10, 10));

        // Wrist joints
        CreateJoint(leftHand, leftForearm, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));
        CreateJoint(rightHand, rightForearm, new Vector3(-30, -30, -30), new Vector3(30, 30, 30));
    }

    private void CreateJoint(RagdollBone child, RagdollBone parent, Vector3 lowLimits, Vector3 highLimits)
    {
        if (child == null || parent == null) return;

        ConfigurableJoint joint = child.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = parent.GetComponent<Rigidbody>();

        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = child.transform.localPosition;
        joint.axis = Vector3.right;
        joint.secondaryAxis = Vector3.up;

        // Lock position
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;

        // Set angular limits
        joint.angularXMotion = ConfigurableJointMotion.Limited;
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularZMotion = ConfigurableJointMotion.Limited;

        SoftJointLimit limitX = new SoftJointLimit();
        limitX.limit = highLimits.x;
        joint.highAngularXLimit = limitX;

        limitX.limit = -lowLimits.x;
        joint.lowAngularXLimit = limitX;

        SoftJointLimit limitYZ = new SoftJointLimit();
        limitYZ.limit = highLimits.y;
        joint.angularYLimit = limitYZ;

        limitYZ.limit = highLimits.z;
        joint.angularZLimit = limitYZ;

        // Spring and damping
        JointDrive drive = new JointDrive();
        drive.positionSpring = jointSpring;
        drive.positionDamper = jointDamper;
        drive.maximumForce = Mathf.Infinity;

        joint.slerpDrive = drive;
        joint.rotationDriveMode = RotationDriveMode.Slerp;
    }

    public RagdollBone GetBone(string boneName)
    {
        return bones.ContainsKey(boneName) ? bones[boneName] : null;
    }
}