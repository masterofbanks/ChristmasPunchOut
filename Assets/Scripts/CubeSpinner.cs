using UnityEngine;

public class CubeSpinner : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f); // Degrees per second

    void Update()
    {
        // Rotate the object continuously
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}