using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Physics-based voxel character with procedural leg animation and upper body ragdoll
/// Lower body: Procedural animation with IK foot placement
/// Upper body: Physics-based ragdoll controlled by target poses
/// </summary>
public class PhysicsVoxelCharacter : MonoBehaviour
{
    [Header("=== SKELETON REFERENCES ===")]
    public Transform root;

    [Header("Lower Body (Procedural)")]
    public Transform hips;
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;

    [Header("Upper Body (Physics)")]
    public Transform spine;
    public Transform chest;
    public Transform neck;
    public Transform head;
    public Transform leftShoulder;
    public Transform leftUpperArm;
    public Transform leftForearm;
    public Transform leftHand;
    public Transform rightShoulder;
    public Transform rightUpperArm;
    public Transform rightForearm;
    public Transform rightHand;

    [Header("Build Settings")]
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private float voxelSize = 0.1f;
    [SerializeField] private Material voxelMaterial;
    [SerializeField] private bool useCombinedMesh = true;

    private bool isBuilt = false;

    public void BuildCharacter()
    {
        if (isBuilt)
        {
            Debug.LogWarning("Character already built!");
            return;
        }

        // Create visual root as CHILD of this transform (not parented to world)
        GameObject visualRoot = new GameObject("PhysicsSkeletonVisuals");
        visualRoot.transform.SetParent(transform);
        visualRoot.transform.localPosition = Vector3.zero; // LOCAL position (relative to parent)
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        // Root bone at local origin
        GameObject rootGO = new GameObject("Root");
        rootGO.transform.SetParent(visualRoot.transform);
        rootGO.transform.localPosition = Vector3.zero; // LOCAL
        rootGO.transform.localRotation = Quaternion.identity;
        root = rootGO.transform;

        BuildLowerBody(cubeUnit, voxelMaterial);
        BuildUpperBody(cubeUnit, voxelMaterial);

        isBuilt = true;
        Debug.Log($"Physics voxel character built at {transform.position}");
    }

    private void BuildLowerBody(float unit, Material mat)
    {
        // All positions are LOCAL to their parent
        GameObject hipsGO = CubePlayerSpawner.CreateVoxelBone(
            "Hips",
            new Vector3(unit * 1.5f, unit * 0.8f, unit),
            new Vector3(0, unit * 0.4f, 0), // LOCAL position
            root,
            voxelSize,
            mat,
            useCombinedMesh
        );
        hips = hipsGO.transform;

        // Left leg
        GameObject leftUpperLegGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftUpperLeg",
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f),
            new Vector3(-unit * 0.5f, -unit * 1.0f, 0),
            hips,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftUpperLeg = leftUpperLegGO.transform;

        GameObject leftLowerLegGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftLowerLeg",
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f),
            new Vector3(0, -unit * 1.6f, 0),
            leftUpperLeg,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftLowerLeg = leftLowerLegGO.transform;

        GameObject leftFootGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftFoot",
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f),
            new Vector3(0, -unit * 1.0f, unit * 0.3f),
            leftLowerLeg,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftFoot = leftFootGO.transform;

        // Right leg
        GameObject rightUpperLegGO = CubePlayerSpawner.CreateVoxelBone(
            "RightUpperLeg",
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f),
            new Vector3(unit * 0.5f, -unit * 1.0f, 0),
            hips,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightUpperLeg = rightUpperLegGO.transform;

        GameObject rightLowerLegGO = CubePlayerSpawner.CreateVoxelBone(
            "RightLowerLeg",
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f),
            new Vector3(0, -unit * 1.6f, 0),
            rightUpperLeg,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightLowerLeg = rightLowerLegGO.transform;

        GameObject rightFootGO = CubePlayerSpawner.CreateVoxelBone(
            "RightFoot",
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f),
            new Vector3(0, -unit * 1.0f, unit * 0.3f),
            rightLowerLeg,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightFoot = rightFootGO.transform;
    }

    private void BuildUpperBody(float unit, Material mat)
    {
        // Spine
        GameObject spineGO = CubePlayerSpawner.CreateVoxelBone(
            "Spine",
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 0.8f),
            new Vector3(0, unit * 1.2f, 0),
            hips,
            voxelSize,
            mat,
            useCombinedMesh
        );
        spine = spineGO.transform;

        // Chest
        GameObject chestGO = CubePlayerSpawner.CreateVoxelBone(
            "Chest",
            new Vector3(unit * 1.8f, unit * 1.0f, unit),
            new Vector3(0, unit * 1.3f, 0),
            spine,
            voxelSize,
            mat,
            useCombinedMesh
        );
        chest = chestGO.transform;

        // Neck
        GameObject neckGO = CubePlayerSpawner.CreateVoxelBone(
            "Neck",
            new Vector3(unit * 0.6f, unit * 0.5f, unit * 0.6f),
            new Vector3(0, unit * 1.0f, 0),
            chest,
            voxelSize,
            mat,
            useCombinedMesh
        );
        neck = neckGO.transform;

        // Head
        GameObject headGO = CubePlayerSpawner.CreateVoxelBone(
            "Head",
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 1.2f),
            new Vector3(0, unit * 0.8f, 0),
            neck,
            voxelSize,
            mat,
            useCombinedMesh
        );
        head = headGO.transform;

        // Left arm
        GameObject leftShoulderGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftShoulder",
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f),
            new Vector3(-unit * 1.15f, unit * 0.4f, 0),
            chest,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftShoulder = leftShoulderGO.transform;

        GameObject leftUpperArmGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftUpperArm",
            new Vector3(unit * 0.6f, unit * 1.2f, unit * 0.6f),
            new Vector3(0, -unit * 0.85f, 0),
            leftShoulder,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftUpperArm = leftUpperArmGO.transform;

        GameObject leftForearmGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftForearm",
            new Vector3(unit * 0.5f, unit * 1.1f, unit * 0.5f),
            new Vector3(0, -unit * 1.15f, 0),
            leftUpperArm,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftForearm = leftForearmGO.transform;

        GameObject leftHandGO = CubePlayerSpawner.CreateVoxelBone(
            "LeftHand",
            new Vector3(unit * 0.6f, unit * 0.6f, unit * 0.6f),
            new Vector3(0, -unit * 0.85f, 0),
            leftForearm,
            voxelSize,
            mat,
            useCombinedMesh
        );
        leftHand = leftHandGO.transform;

        // Right arm
        GameObject rightShoulderGO = CubePlayerSpawner.CreateVoxelBone(
            "RightShoulder",
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f),
            new Vector3(unit * 1.15f, unit * 0.4f, 0),
            chest,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightShoulder = rightShoulderGO.transform;

        GameObject rightUpperArmGO = CubePlayerSpawner.CreateVoxelBone(
            "RightUpperArm",
            new Vector3(unit * 0.6f, unit * 1.2f, unit * 0.6f),
            new Vector3(0, -unit * 0.85f, 0),
            rightShoulder,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightUpperArm = rightUpperArmGO.transform;

        GameObject rightForearmGO = CubePlayerSpawner.CreateVoxelBone(
            "RightForearm",
            new Vector3(unit * 0.5f, unit * 1.1f, unit * 0.5f),
            new Vector3(0, -unit * 1.15f, 0),
            rightUpperArm,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightForearm = rightForearmGO.transform;

        GameObject rightHandGO = CubePlayerSpawner.CreateVoxelBone(
            "RightHand",
            new Vector3(unit * 0.6f, unit * 0.6f, unit * 0.6f),
            new Vector3(0, -unit * 0.85f, 0),
            rightForearm,
            voxelSize,
            mat,
            useCombinedMesh
        );
        rightHand = rightHandGO.transform;
    }

    public Transform[] GetLowerBodyBones()
    {
        return new Transform[] { hips, leftUpperLeg, leftLowerLeg, leftFoot, rightUpperLeg, rightLowerLeg, rightFoot };
    }

    public Transform[] GetUpperBodyBones()
    {
        return new Transform[] { spine, chest, neck, head, leftShoulder, leftUpperArm, leftForearm, leftHand, rightShoulder, rightUpperArm, rightForearm, rightHand };
    }
}