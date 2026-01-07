using UnityEngine;

public class CubePlayerSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Visual Settings")]
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private Color[] boneColors;

    [Header("Character Proportions")]
    [Tooltip("Base unit size for cubes - affects overall character size")]
    [SerializeField] private float cubeUnit = 1f;

    [Header("Voxel Resolution")]
    [Tooltip("Size of individual voxel cubes (smaller = more detail)")]
    [SerializeField] private float voxelSize = 0.1f;
    [Tooltip("Use single mesh per bone instead of individual cubes (better performance)")]
    [SerializeField] private bool useCombinedMesh = true;

    [Header("Physics Settings")]
    [SerializeField] private float characterMass = 1f;
    [SerializeField] private bool autoCalculateCollider = true;
    [SerializeField] private float colliderHeightPadding = 0.0f;
    [SerializeField] private float colliderRadiusMultiplier = 0.55f;
    [SerializeField] private float manualColliderHeight = 2f;
    [SerializeField] private float manualColliderRadius = 0.3f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Camera Settings")]
    [SerializeField] private bool setupCameraFollow = true;
    [SerializeField] private bool createCameraIfMissing = false;

    [Header("Input System - REQUIRED")]
    [SerializeField] private UnityEngine.InputSystem.InputActionAsset inputActions;
    [Tooltip("Leave empty to use default scheme")]
    [SerializeField] private string controlScheme = "";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("References")]
    private CubeSkeletonCharacter spawnedCharacter;

    void Start()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ Cannot spawn character: No Input Actions asset assigned!");
            return;
        }

        if (spawnOnStart)
        {
            SpawnCharacter();
        }
    }

    public CubeSkeletonCharacter SpawnCharacter()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ Cannot spawn character: No Input Actions asset assigned!");
            return null;
        }

        if (showDebugLogs) Debug.Log("=== SPAWNING VOXEL CHARACTER ===");

        // PHASE 1: Create root GameObject at spawn position
        GameObject characterRoot = new GameObject("VoxelSkeletonPlayer");
        characterRoot.transform.position = spawnPosition;
        characterRoot.transform.rotation = Quaternion.identity;
        characterRoot.tag = "Player"; // Tag for camera to find

        // PHASE 2: Build voxel skeleton FIRST (so we can measure it)
        CubeSkeletonCharacter skeleton = characterRoot.AddComponent<CubeSkeletonCharacter>();
        skeleton.BuildSkeleton(cubeUnit, voxelSize, cubeMaterial, useCombinedMesh);

        // PHASE 3: MEASURE the actual skeleton bounds
        Transform skeletonVisuals = skeleton.transform.Find("SkeletonVisuals");
        Bounds totalBounds = new Bounds(skeletonVisuals.position, Vector3.zero);

        // Get all renderers to calculate actual bounds
        Renderer[] allRenderers = skeletonVisuals.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in allRenderers)
        {
            totalBounds.Encapsulate(renderer.bounds);
        }

        // The BOTTOM of the skeleton in world space
        float skeletonBottom = totalBounds.min.y;
        float skeletonTop = totalBounds.max.y;
        float actualHeight = skeletonTop - skeletonBottom;

        if (showDebugLogs)
        {
            Debug.Log($"=== MEASURED SKELETON BOUNDS ===");
            Debug.Log($"  Skeleton bottom (world): {skeletonBottom:F3}");
            Debug.Log($"  Skeleton top (world): {skeletonTop:F3}");
            Debug.Log($"  Actual height: {actualHeight:F3}");
            Debug.Log($"  Desired ground level: {spawnPosition.y:F3}");
        }

        // PHASE 4: Position skeleton so bottom is at ground level (spawnPosition.y)
        float offsetNeeded = spawnPosition.y - skeletonBottom;
        skeletonVisuals.position = new Vector3(
            skeletonVisuals.position.x,
            skeletonVisuals.position.y + offsetNeeded,
            skeletonVisuals.position.z
        );

        if (showDebugLogs)
        {
            Debug.Log($"  Offset applied: {offsetNeeded:F3}");
            Debug.Log($"  New skeleton bottom: {spawnPosition.y:F3}");
            Debug.Log($"  New skeleton top: {spawnPosition.y + actualHeight:F3}");
        }

        // PHASE 5: Setup collider based on ACTUAL measurements
        float colliderHeight, colliderRadius;

        if (autoCalculateCollider)
        {
            colliderHeight = actualHeight + colliderHeightPadding;

            // Calculate radius from skeleton width
            float skeletonWidth = Mathf.Max(
                totalBounds.size.x,
                totalBounds.size.z
            );
            colliderRadius = (skeletonWidth / 2f) * colliderRadiusMultiplier;

            if (showDebugLogs)
            {
                Debug.Log($"=== COLLIDER SETUP ===");
                Debug.Log($"  Collider height: {colliderHeight:F3}");
                Debug.Log($"  Collider radius: {colliderRadius:F3}");
                Debug.Log($"  Skeleton width: {skeletonWidth:F3}");
            }
        }
        else
        {
            colliderHeight = manualColliderHeight;
            colliderRadius = manualColliderRadius;
        }

        SetupPhysics(characterRoot, colliderHeight, colliderRadius);

        // PHASE 6: Add Movement Controller
        SimpleCharacterMovement movement = characterRoot.AddComponent<SimpleCharacterMovement>();
        movement.moveSpeed = moveSpeed;
        movement.rotationSpeed = rotationSpeed;
        movement.jumpForce = jumpForce;

        // PHASE 7: Add Input Component
        UnityEngine.InputSystem.PlayerInput playerInput = characterRoot.AddComponent<UnityEngine.InputSystem.PlayerInput>();
        playerInput.actions = inputActions;
        playerInput.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeCSharpEvents;
        playerInput.defaultActionMap = "Player";

        if (!string.IsNullOrEmpty(controlScheme))
        {
            playerInput.defaultControlScheme = controlScheme;
        }

        var playerActionMap = playerInput.actions.FindActionMap("Player");
        if (playerActionMap != null)
        {
            playerActionMap.Enable();
        }

        playerInput.ActivateInput();

        // PHASE 8: Add animator
        CubeSkeletonAnimator animator = characterRoot.AddComponent<CubeSkeletonAnimator>();
        animator.Initialize(skeleton);

        // PHASE 9: Add attack handler
        PlayerAttackHandler attackHandler = characterRoot.AddComponent<PlayerAttackHandler>();

        // Configure enemy layer
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex == -1)
        {
            Debug.LogWarning("⚠ 'Enemy' layer not found! Create it in Project Settings > Tags & Layers.");
            // Try to use default layer as fallback
            attackHandler.SetEnemyLayer(LayerMask.GetMask("Default"));
        }
        else
        {
            attackHandler.SetEnemyLayer(LayerMask.GetMask("Enemy"));
        }

        if (showDebugLogs)
        {
            Debug.Log("✓ Player attack handler configured");
        }

        // PHASE 10: Setup camera follow
        if (setupCameraFollow)
        {
            SetupCamera(characterRoot.transform);
        }

        spawnedCharacter = skeleton;

        if (showDebugLogs)
        {
            Debug.Log("=== ✓ VOXEL CHARACTER SPAWNED SUCCESSFULLY ===");
        }

        return skeleton;
    }

    private void SetupCamera(Transform playerTransform)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null && createCameraIfMissing)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";

            // Add audio listener
            cameraObj.AddComponent<AudioListener>();

            if (showDebugLogs)
            {
                Debug.Log("✓ Created new camera");
            }
        }

        if (mainCamera != null)
        {
            CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();

            if (cameraFollow == null)
            {
                cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
                if (showDebugLogs)
                {
                    Debug.Log("✓ Added CameraFollow component to camera");
                }
            }

            cameraFollow.SetTarget(playerTransform);

            // Give camera reference to movement controller
            var movement = playerTransform.GetComponent<SimpleCharacterMovement>();
            if (movement != null)
            {
                movement.SetCamera(mainCamera);
            }

            if (showDebugLogs)
            {
                Debug.Log("✓ Camera setup complete - following player with deadzone");
            }
        }
        else
        {
            Debug.LogWarning("⚠ No main camera found! Create one or enable 'Create Camera If Missing'");
        }
    }

    private void SetupPhysics(GameObject characterRoot, float height, float radius)
    {
        Rigidbody rb = characterRoot.AddComponent<Rigidbody>();
        rb.mass = characterMass;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;

        CapsuleCollider collider = characterRoot.AddComponent<CapsuleCollider>();
        collider.height = height;
        collider.radius = radius;
        collider.center = new Vector3(0, height / 2f, 0);
        collider.direction = 1; // Y-axis
    }

    public static GameObject CreateVoxelBone(string boneName, Vector3 boneSize, Vector3 localPosition,
        Transform parent, float voxelSize, Material mat, bool useCombinedMesh, Color? color = null)
    {
        GameObject bone = new GameObject(boneName);
        bone.transform.SetParent(parent);
        bone.transform.localPosition = localPosition;
        bone.transform.localRotation = Quaternion.identity;

        int voxelsX = Mathf.Max(1, Mathf.RoundToInt(boneSize.x / voxelSize));
        int voxelsY = Mathf.Max(1, Mathf.RoundToInt(boneSize.y / voxelSize));
        int voxelsZ = Mathf.Max(1, Mathf.RoundToInt(boneSize.z / voxelSize));

        if (useCombinedMesh)
        {
            CreateCombinedVoxelMesh(bone, voxelsX, voxelsY, voxelsZ, voxelSize, mat, color);
        }
        else
        {
            CreateIndividualVoxels(bone, voxelsX, voxelsY, voxelsZ, voxelSize, boneSize, mat, color);
        }

        return bone;
    }

    private static void CreateCombinedVoxelMesh(GameObject bone, int voxelsX, int voxelsY, int voxelsZ,
        float voxelSize, Material mat, Color? color)
    {
        CombineInstance[] combine = new CombineInstance[voxelsX * voxelsY * voxelsZ];
        int index = 0;

        Vector3 offset = new Vector3(
            -((voxelsX - 1) * voxelSize) / 2f,
            -((voxelsY - 1) * voxelSize) / 2f,
            -((voxelsZ - 1) * voxelSize) / 2f
        );

        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;

        for (int x = 0; x < voxelsX; x++)
        {
            for (int y = 0; y < voxelsY; y++)
            {
                for (int z = 0; z < voxelsZ; z++)
                {
                    Vector3 voxelPos = offset + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);

                    Matrix4x4 matrix = Matrix4x4.TRS(
                        voxelPos,
                        Quaternion.identity,
                        Vector3.one * voxelSize
                    );

                    combine[index].mesh = cubeMesh;
                    combine[index].transform = matrix;
                    index++;
                }
            }
        }

        MeshFilter meshFilter = bone.AddComponent<MeshFilter>();
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = bone.name + "_VoxelMesh";
        combinedMesh.CombineMeshes(combine, true, true);
        combinedMesh.RecalculateNormals();
        combinedMesh.RecalculateBounds();
        meshFilter.mesh = combinedMesh;

        MeshRenderer renderer = bone.AddComponent<MeshRenderer>();
        if (mat != null)
        {
            renderer.material = mat;
        }

        if (color.HasValue)
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", color.Value);
            renderer.SetPropertyBlock(propBlock);
        }

        Object.Destroy(tempCube);
    }

    private static void CreateIndividualVoxels(GameObject bone, int voxelsX, int voxelsY, int voxelsZ,
        float voxelSize, Vector3 boneSize, Material mat, Color? color)
    {
        Vector3 offset = new Vector3(
            -((voxelsX - 1) * voxelSize) / 2f,
            -((voxelsY - 1) * voxelSize) / 2f,
            -((voxelsZ - 1) * voxelSize) / 2f
        );

        for (int x = 0; x < voxelsX; x++)
        {
            for (int y = 0; y < voxelsY; y++)
            {
                for (int z = 0; z < voxelsZ; z++)
                {
                    GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxel.name = $"Voxel_{x}_{y}_{z}";
                    voxel.transform.SetParent(bone.transform);

                    Vector3 voxelPos = offset + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
                    voxel.transform.localPosition = voxelPos;
                    voxel.transform.localScale = Vector3.one * voxelSize;

                    Collider voxelCollider = voxel.GetComponent<Collider>();
                    if (voxelCollider != null)
                    {
                        Object.Destroy(voxelCollider);
                    }

                    Renderer renderer = voxel.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        if (mat != null)
                        {
                            renderer.material = mat;
                        }

                        if (color.HasValue)
                        {
                            renderer.material.color = color.Value;
                        }
                    }
                }
            }
        }
    }

    public void DespawnCharacter()
    {
        if (spawnedCharacter != null)
        {
            Destroy(spawnedCharacter.gameObject);
            spawnedCharacter = null;
        }
    }

    public CubeSkeletonCharacter GetSpawnedCharacter()
    {
        return spawnedCharacter;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, 0.2f);

        if (Application.isPlaying && spawnedCharacter != null)
        {
            CapsuleCollider collider = spawnedCharacter.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                float height = collider.height;
                float radius = collider.radius;
                Vector3 center = spawnedCharacter.transform.position + collider.center;

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(center, new Vector3(radius * 2f, height, radius * 2f));

                Gizmos.color = Color.cyan;
                float halfHeight = (height / 2f) - radius;
                Gizmos.DrawWireSphere(center + Vector3.up * halfHeight, radius);
                Gizmos.DrawWireSphere(center + Vector3.down * halfHeight, radius);

                // Draw ground line
                Gizmos.color = Color.red;
                Vector3 groundPos = spawnedCharacter.transform.position;
                Gizmos.DrawLine(groundPos + Vector3.left * 0.5f, groundPos + Vector3.right * 0.5f);
                Gizmos.DrawLine(groundPos + Vector3.back * 0.5f, groundPos + Vector3.forward * 0.5f);
            }
        }
    }
#endif
}