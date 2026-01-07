using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages a humanoid skeleton made of voxel cubes with proper bone hierarchy
/// Each bone is composed of multiple small cubes for a blocky, voxel aesthetic
/// </summary>
public class CubeSkeletonCharacter : MonoBehaviour
{
    [Header("=== CORE SKELETON ===")]
    public Transform root;

    [Header("Torso")]
    public Transform hips;
    public Transform spine;
    public Transform chest;
    public Transform neck;
    public Transform head;

    [Header("=== LEFT ARM ===")]
    public Transform leftShoulder;
    public Transform leftUpperArm;
    public Transform leftForearm;
    public Transform leftHand;

    [Header("=== RIGHT ARM ===")]
    public Transform rightShoulder;
    public Transform rightUpperArm;
    public Transform rightForearm;
    public Transform rightHand;

    [Header("=== LEFT LEG ===")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;

    [Header("=== RIGHT LEG ===")]
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;

    [Header("=== DEATH/SHATTER SETTINGS ===")]
    [SerializeField] private float explosionForce = 5f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float upwardsModifier = 1f;
    [SerializeField] private float voxelMass = 0.1f;
    [SerializeField] private float voxelLifetime = 5f;
    [SerializeField] private bool useRandomTorque = true;
    [SerializeField] private float torqueAmount = 50f;

    private float voxelSize;
    private bool useCombinedMesh;
    private bool isDead = false;

    public void BuildSkeleton(float unit, float voxSize, Material mat, bool combineMesh)
    {
        voxelSize = voxSize;
        useCombinedMesh = combineMesh;

        GameObject visualRoot = new GameObject("SkeletonVisuals");
        visualRoot.transform.SetParent(transform);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        GameObject rootGO = new GameObject("Root");
        rootGO.transform.SetParent(visualRoot.transform);
        rootGO.transform.localPosition = Vector3.zero;
        root = rootGO.transform;

        BuildTorso(unit, mat);
        BuildLeftArm(unit, mat);
        BuildRightArm(unit, mat);
        BuildLeftLeg(unit, mat);
        BuildRightLeg(unit, mat);

        int totalVoxels = CountTotalVoxels(unit);
        Debug.Log($"Voxel skeleton built: {GetBoneCount()} bones, ~{totalVoxels} voxels total");
    }

    /// <summary>
    /// Triggers the death/shatter effect - all voxels explode outward
    /// </summary>
    public void TriggerDeath(Vector3? explosionOrigin = null)
    {
        if (isDead) return;
        isDead = true;

        Vector3 explosionCenter = explosionOrigin ?? transform.position;

        Debug.Log("?? DEATH TRIGGERED - Shattering character!");

        // Disable character controller components
        DisableCharacterComponents();

        // Shatter all bones
        StartCoroutine(ShatterAllBones(explosionCenter));
    }

    /// <summary>
    /// Disables movement and animation components
    /// </summary>
    private void DisableCharacterComponents()
    {
        // Disable movement
        var movement = GetComponent<SimpleCharacterMovement>();
        if (movement != null) movement.enabled = false;

        // Disable animator
        var animator = GetComponent<CubeSkeletonAnimator>();
        if (animator != null) animator.enabled = false;

        // Disable rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Disable collider
        var collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
    }

    /// <summary>
    /// Shatters all bones into individual voxels with physics
    /// </summary>
    private IEnumerator ShatterAllBones(Vector3 explosionCenter)
    {
        List<GameObject> allVoxels = new List<GameObject>();

        // Get all bone transforms
        Transform[] allBones = GetAllBones();

        foreach (Transform bone in allBones)
        {
            if (bone == null || bone == root) continue;

            if (useCombinedMesh)
            {
                // For combined meshes, we need to break them apart
                allVoxels.AddRange(BreakCombinedMeshIntoCubes(bone));
            }
            else
            {
                // For individual voxels, just detach them
                allVoxels.AddRange(DetachIndividualVoxels(bone));
            }
        }

        Debug.Log($"?? Shattered into {allVoxels.Count} voxel pieces!");

        // Apply explosion force to all voxels
        foreach (GameObject voxel in allVoxels)
        {
            ApplyVoxelPhysics(voxel, explosionCenter);
        }

        // Clean up skeleton root after a delay
        yield return new WaitForSeconds(voxelLifetime);

        // Destroy all voxel pieces
        foreach (GameObject voxel in allVoxels)
        {
            if (voxel != null) Destroy(voxel);
        }

        // Destroy the character root
        Destroy(gameObject);
    }

    /// <summary>
    /// Breaks a combined mesh bone into individual voxel cubes
    /// </summary>
    private List<GameObject> BreakCombinedMeshIntoCubes(Transform bone)
    {
        List<GameObject> voxels = new List<GameObject>();

        MeshFilter meshFilter = bone.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = bone.GetComponent<MeshRenderer>();

        if (meshFilter == null || meshRenderer == null) return voxels;

        // Get the bone's world transform data
        Vector3 worldPos = bone.position;
        Quaternion worldRot = bone.rotation;
        Vector3 worldScale = bone.lossyScale;

        // Parse voxel count from original combined mesh bounds
        Mesh mesh = meshFilter.mesh;
        Bounds bounds = mesh.bounds;

        // Estimate voxel count
        int voxelsX = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / voxelSize));
        int voxelsY = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y / voxelSize));
        int voxelsZ = Mathf.Max(1, Mathf.RoundToInt(bounds.size.z / voxelSize));

        // Recreate individual voxels
        Material boneMaterial = meshRenderer.material;

        for (int x = 0; x < voxelsX; x++)
        {
            for (int y = 0; y < voxelsY; y++)
            {
                for (int z = 0; z < voxelsZ; z++)
                {
                    GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxel.name = $"{bone.name}_Voxel_{x}_{y}_{z}";

                    // Calculate local offset
                    Vector3 localOffset = new Vector3(
                        (x - (voxelsX - 1) / 2f) * voxelSize,
                        (y - (voxelsY - 1) / 2f) * voxelSize,
                        (z - (voxelsZ - 1) / 2f) * voxelSize
                    );

                    // Transform to world space
                    voxel.transform.position = worldPos + worldRot * localOffset;
                    voxel.transform.rotation = worldRot;
                    voxel.transform.localScale = Vector3.one * voxelSize;

                    // Apply material
                    voxel.GetComponent<Renderer>().material = boneMaterial;

                    voxels.Add(voxel);
                }
            }
        }

        // Hide original bone
        if (meshRenderer != null) meshRenderer.enabled = false;

        return voxels;
    }

    /// <summary>
    /// Detaches individual voxel GameObjects from bone
    /// </summary>
    private List<GameObject> DetachIndividualVoxels(Transform bone)
    {
        List<GameObject> voxels = new List<GameObject>();

        // Get all direct children with MeshRenderer (the voxels)
        for (int i = bone.childCount - 1; i >= 0; i--)
        {
            Transform child = bone.GetChild(i);

            if (child.GetComponent<MeshRenderer>() != null)
            {
                // Unparent from bone
                child.SetParent(null);
                voxels.Add(child.gameObject);
            }
        }

        return voxels;
    }

    /// <summary>
    /// Applies physics and explosion force to a voxel piece
    /// </summary>
    private void ApplyVoxelPhysics(GameObject voxel, Vector3 explosionCenter)
    {
        // Add Rigidbody
        Rigidbody rb = voxel.AddComponent<Rigidbody>();
        rb.mass = voxelMass;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Apply explosion force
        rb.AddExplosionForce(
            explosionForce,
            explosionCenter,
            explosionRadius,
            upwardsModifier,
            ForceMode.Impulse
        );

        // Add random torque for spinning effect
        if (useRandomTorque)
        {
            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueAmount, torqueAmount),
                Random.Range(-torqueAmount, torqueAmount),
                Random.Range(-torqueAmount, torqueAmount)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }

        // Tag for cleanup
        voxel.tag = "DeadVoxel";
    }

    private int CountTotalVoxels(float unit)
    {
        int count = 0;

        // Torso
        count += EstimateVoxels(new Vector3(unit * 1.5f, unit * 0.8f, unit));
        count += EstimateVoxels(new Vector3(unit * 1.2f, unit * 1.2f, unit * 0.8f));
        count += EstimateVoxels(new Vector3(unit * 1.8f, unit * 1.0f, unit));
        count += EstimateVoxels(new Vector3(unit * 0.6f, unit * 0.5f, unit * 0.6f));
        count += EstimateVoxels(new Vector3(unit * 1.2f, unit * 1.2f, unit * 1.2f));

        // Arms (x2)
        count += 2 * EstimateVoxels(new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f));
        count += 2 * EstimateVoxels(new Vector3(unit * 0.6f, unit * 1.2f, unit * 0.6f));
        count += 2 * EstimateVoxels(new Vector3(unit * 0.5f, unit * 1.1f, unit * 0.5f));
        count += 2 * EstimateVoxels(new Vector3(unit * 0.6f, unit * 0.6f, unit * 0.6f));

        // Legs (x2)
        count += 2 * EstimateVoxels(new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f));
        count += 2 * EstimateVoxels(new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f));
        count += 2 * EstimateVoxels(new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f));

        return count;
    }

    private int EstimateVoxels(Vector3 boneSize)
    {
        int vx = Mathf.Max(1, Mathf.RoundToInt(boneSize.x / voxelSize));
        int vy = Mathf.Max(1, Mathf.RoundToInt(boneSize.y / voxelSize));
        int vz = Mathf.Max(1, Mathf.RoundToInt(boneSize.z / voxelSize));
        return vx * vy * vz;
    }

    private void BuildTorso(float unit, Material mat)
    {
        GameObject hipsGO = CubePlayerSpawner.CreateVoxelBone(
            "Hips",
            new Vector3(unit * 1.5f, unit * 0.8f, unit),
            new Vector3(0, unit * 0.4f, 0),
            root,
            voxelSize,
            mat,
            useCombinedMesh
        );
        hips = hipsGO.transform;

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
    }

    private void BuildLeftArm(float unit, Material mat)
    {
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
    }

    private void BuildRightArm(float unit, Material mat)
    {
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

    private void BuildLeftLeg(float unit, Material mat)
    {
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
    }

    private void BuildRightLeg(float unit, Material mat)
    {
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

    public int GetBoneCount()
    {
        return 19;
    }

    public void ResetToTPose()
    {
        Transform[] allBones = GetAllBones();
        foreach (Transform bone in allBones)
        {
            if (bone != null)
            {
                bone.localRotation = Quaternion.identity;
            }
        }
    }

    public Transform[] GetAllBones()
    {
        return new Transform[]
        {
            root, hips, spine, chest, neck, head,
            leftShoulder, leftUpperArm, leftForearm, leftHand,
            rightShoulder, rightUpperArm, rightForearm, rightHand,
            leftUpperLeg, leftLowerLeg, leftFoot,
            rightUpperLeg, rightLowerLeg, rightFoot
        };
    }

    public void RotateBone(Transform bone, Vector3 eulerAngles)
    {
        if (bone != null)
        {
            bone.localRotation = Quaternion.Euler(eulerAngles);
        }
    }

    public bool IsDead() => isDead;
}