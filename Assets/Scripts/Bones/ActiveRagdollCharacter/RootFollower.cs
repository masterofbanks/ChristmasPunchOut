using UnityEngine;

/// <summary>
/// Makes the root GameObject follow a target transform (typically the hips)
/// This allows the camera to follow the root while physics drives the hips
/// </summary>
public class RootFollower : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindHips = true;

    [Header("Follow Settings")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = false;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Header("Smoothing")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSpeed = 10f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private bool isInitialized = false;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (target == null && autoFindHips)
        {
            // Try to find hips in children
            ActiveRagdollCharacter character = GetComponent<ActiveRagdollCharacter>();
            if (character != null && character.hips != null)
            {
                target = character.hips.transform;
                if (showDebugLogs)
                {
                    Debug.Log($"[RootFollower] Auto-found hips: {target.name}");
                }
            }
            else
            {
                Debug.LogWarning("[RootFollower] Could not auto-find hips!");
                return;
            }
        }

        if (target != null)
        {
            isInitialized = true;
            if (showDebugLogs)
            {
                Debug.Log($"[RootFollower] Initialized - following {target.name}");
            }
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized || target == null) return;

        if (followPosition)
        {
            Vector3 targetPosition = target.position + positionOffset;

            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    followSpeed * Time.deltaTime
                );
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        if (followRotation)
        {
            if (smoothFollow)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target.rotation,
                    followSpeed * Time.deltaTime
                );
            }
            else
            {
                transform.rotation = target.rotation;
            }
        }
    }

    /// <summary>
    /// Set the target to follow
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        isInitialized = target != null;

        if (showDebugLogs)
        {
            Debug.Log($"[RootFollower] Target set to: {(target != null ? target.name : "NULL")}");
        }
    }

    /// <summary>
    /// Snap to target position immediately (no smoothing)
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        if (followPosition)
        {
            transform.position = target.position + positionOffset;
        }

        if (followRotation)
        {
            transform.rotation = target.rotation;
        }
    }

    private void OnDrawGizmos()
    {
        if (target == null) return;

        // Draw line from root to target
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, target.position);

        // Draw target sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position, 0.2f);

        // Draw root position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}