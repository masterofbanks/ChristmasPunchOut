using UnityEngine;

/// <summary>
/// Active ragdoll V2 - Simplified with proper balance controller
/// </summary>
public class ActiveRagdollV2 : MonoBehaviour
{
    [Header("=== BODY PARTS ===")]
    public RagdollJoint hips;
    public RagdollJoint spine;
    public RagdollJoint chest;
    public RagdollJoint neck;
    public RagdollJoint head;
    public RagdollJoint leftUpperLeg;
    public RagdollJoint leftLowerLeg;
    public RagdollJoint leftFoot;
    public RagdollJoint rightUpperLeg;
    public RagdollJoint rightLowerLeg;
    public RagdollJoint rightFoot;
    public RagdollJoint leftShoulder;
    public RagdollJoint rightShoulder;

    [Header("Build Settings")]
    [SerializeField] private float cubeUnit = 1f;
    [SerializeField] private Material bodyMaterial;

    [Header("Physics Settings")]
    [SerializeField] private float boneMass = 1f;
    [SerializeField] private float jointStiffness = 50f;  // Lower for more flexibility
    [SerializeField] private float jointDamping = 10f;

    [Header("Layers")]
    [SerializeField] private int characterLayer = 6;
    [SerializeField] private int groundLayer = 0;

    private BipedalBalanceController balanceController;

    private void Start()
    {
        if (hips == null)
        {
            BuildCharacter();
        }

        SetupBalanceController();
    }

    public void BuildCharacter()
    {
        float unit = cubeUnit;
        Vector3 spawnOffset = new Vector3(0, unit * 5f, 0);

        // Build skeleton
        hips = CreateJoint("Hips", null, spawnOffset,
            new Vector3(unit * 1.5f, unit * 0.8f, unit), boneMass * 3f);

        spine = CreateJoint("Spine", hips, new Vector3(0, unit * 1.2f, 0),
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 0.8f), boneMass * 2f);

        chest = CreateJoint("Chest", spine, new Vector3(0, unit * 1.3f, 0),
            new Vector3(unit * 1.8f, unit * 1.0f, unit), boneMass * 2.5f);

        neck = CreateJoint("Neck", chest, new Vector3(0, unit * 1.0f, 0),
            new Vector3(unit * 0.6f, unit * 0.5f, unit * 0.6f), boneMass * 0.5f);

        head = CreateJoint("Head", neck, new Vector3(0, unit * 0.8f, 0),
            new Vector3(unit * 1.2f, unit * 1.2f, unit * 1.2f), boneMass * 1f);

        // Legs
        leftUpperLeg = CreateJoint("LeftUpperLeg", hips, new Vector3(-unit * 0.5f, -unit * 1.0f, 0),
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f), boneMass * 1.5f);
        leftLowerLeg = CreateJoint("LeftLowerLeg", leftUpperLeg, new Vector3(0, -unit * 1.6f, 0),
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f), boneMass * 1f);
        leftFoot = CreateJoint("LeftFoot", leftLowerLeg, new Vector3(0, -unit * 1.0f, unit * 0.3f),
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f), boneMass * 0.5f);

        rightUpperLeg = CreateJoint("RightUpperLeg", hips, new Vector3(unit * 0.5f, -unit * 1.0f, 0),
            new Vector3(unit * 0.8f, unit * 1.6f, unit * 0.8f), boneMass * 1.5f);
        rightLowerLeg = CreateJoint("RightLowerLeg", rightUpperLeg, new Vector3(0, -unit * 1.6f, 0),
            new Vector3(unit * 0.7f, unit * 1.6f, unit * 0.7f), boneMass * 1f);
        rightFoot = CreateJoint("RightFoot", rightLowerLeg, new Vector3(0, -unit * 1.0f, unit * 0.3f),
            new Vector3(unit * 0.8f, unit * 0.4f, unit * 1.2f), boneMass * 0.5f);

        // Arms
        leftShoulder = CreateJoint("LeftShoulder", chest, new Vector3(-unit * 1.15f, unit * 0.4f, 0),
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f), boneMass * 0.3f);
        rightShoulder = CreateJoint("RightShoulder", chest, new Vector3(unit * 1.15f, unit * 0.4f, 0),
            new Vector3(unit * 0.5f, unit * 0.5f, unit * 0.5f), boneMass * 0.3f);

        ConfigurePhysicsLayers();
        ConfigureJointLimits();

        Debug.Log($"✓ Character built successfully");
    }

    private RagdollJoint CreateJoint(string name, RagdollJoint parent, Vector3 localPos, Vector3 size, float mass)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.localScale = size;
        obj.layer = characterLayer;

        if (parent != null)
        {
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            obj.transform.SetParent(transform);
            obj.transform.position = localPos;
        }

        // Rigidbody with HIGH damping for stability
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 2f;  // INCREASED
        rb.angularDamping = 15f;  // INCREASED
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 10f;  // REDUCED
        rb.solverIterations = 15;  // INCREASED
        rb.solverVelocityIterations = 15;  // INCREASED

        // Collider with VERY HIGH friction
        BoxCollider col = obj.GetComponent<BoxCollider>();
        PhysicsMaterial physMat = new PhysicsMaterial("RagdollMat");
        physMat.dynamicFriction = 1.0f;  // MAX friction
        physMat.staticFriction = 1.0f;   // MAX friction
        physMat.bounciness = 0f;
        col.material = physMat;

        // Joint (if has parent)
        if (parent != null)
        {
            ConfigurableJoint joint = obj.AddComponent<ConfigurableJoint>();
            joint.connectedBody = parent.GetComponent<Rigidbody>();
            joint.autoConfigureConnectedAnchor = true;

            // Lock position
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Allow rotation
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            // CRITICAL: NO DRIVES - balance controller will apply torques directly
            JointDrive disabledDrive = new JointDrive
            {
                positionSpring = 0,
                positionDamper = 0,
                maximumForce = 0
            };
            joint.slerpDrive = disabledDrive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;

            // HIGH joint projection for stability
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.01f;
            joint.projectionAngle = 1f;
        }

        // Add RagdollJoint component (passive marker)
        RagdollJoint ragdollJoint = obj.AddComponent<RagdollJoint>();

        if (bodyMaterial != null)
        {
            obj.GetComponent<Renderer>().material = bodyMaterial;
        }

        return ragdollJoint;
    }

    private void SetupBalanceController()
    {
        balanceController = gameObject.AddComponent<BipedalBalanceController>();

        // Assign references
        balanceController.hips = hips;
        balanceController.leftUpperLeg = leftUpperLeg;
        balanceController.leftLowerLeg = leftLowerLeg;
        balanceController.leftFoot = leftFoot;
        balanceController.rightUpperLeg = rightUpperLeg;
        balanceController.rightLowerLeg = rightLowerLeg;
        balanceController.rightFoot = rightFoot;

        // Initialize
        balanceController.Initialize();

        Debug.Log("✓ Balance controller configured and initialized");
    }

    private void ConfigureJointLimits()
    {
        // RELAXED limits for more natural movement
        SetLimits(spine, -30, 30, 20, 20);
        SetLimits(chest, -20, 20, 15, 15);
        SetLimits(neck, -40, 40, 30, 30);

        // Leg limits - allow good range of motion
        SetLimits(leftUpperLeg, -70, 100, 40, 30);
        SetLimits(rightUpperLeg, -70, 100, 40, 30);

        SetLimits(leftLowerLeg, 0, 150, 10, 10);
        SetLimits(rightLowerLeg, 0, 150, 10, 10);

        SetLimits(leftFoot, -40, 40, 30, 30);
        SetLimits(rightFoot, -40, 40, 30, 30);

        // Arms - keep simple
        SetLimits(leftShoulder, -90, 90, 45, 45);
        SetLimits(rightShoulder, -90, 90, 45, 45);
    }

    private void SetLimits(RagdollJoint joint, float lowX, float highX, float y, float z)
    {
        if (joint == null) return;

        ConfigurableJoint cj = joint.GetComponent<ConfigurableJoint>();
        if (cj != null)
        {
            cj.lowAngularXLimit = new SoftJointLimit { limit = lowX };
            cj.highAngularXLimit = new SoftJointLimit { limit = highX };
            cj.angularYLimit = new SoftJointLimit { limit = y };
            cj.angularZLimit = new SoftJointLimit { limit = z };
        }
    }

    private void ConfigurePhysicsLayers()
    {
        Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
        Physics.IgnoreLayerCollision(characterLayer, groundLayer, false);
    }
}