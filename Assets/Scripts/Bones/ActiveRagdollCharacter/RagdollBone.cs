using UnityEngine;

/// <summary>
/// Individual bone in the active ragdoll system
/// Combines rigidbody physics with voxel visualization
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RagdollBone : MonoBehaviour
{
    public Vector3 size;
    public float mass;

    private float voxelSize;
    private Material voxelMaterial;
    private bool useCombinedMesh;
    private Rigidbody rb;
    private BoxCollider col;

    // Target rotation for active pose control
    public Quaternion targetLocalRotation = Quaternion.identity;

    public void Initialize(Vector3 boneSize, float boneMass, float voxSize, Material mat, bool combineMesh)
    {
        size = boneSize;
        mass = boneMass;
        voxelSize = voxSize;
        voxelMaterial = mat;
        useCombinedMesh = combineMesh;

        // Setup rigidbody
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Setup collider
        col = gameObject.AddComponent<BoxCollider>();
        col.size = size;
        col.center = Vector3.zero;
    }

    public void BuildVoxels()
    {
        GameObject visualsRoot = new GameObject("Visuals");
        visualsRoot.transform.SetParent(transform);
        visualsRoot.transform.localPosition = Vector3.zero;
        visualsRoot.transform.localRotation = Quaternion.identity;

        CubePlayerSpawner.CreateVoxelBone(
            "VoxelMesh",
            size,
            Vector3.zero,
            visualsRoot.transform,
            voxelSize,
            voxelMaterial,
            useCombinedMesh
        );
    }

    public Rigidbody GetRigidbody() => rb;
}