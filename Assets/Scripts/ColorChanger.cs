using UnityEngine;

public class ColorChanger : MonoBehaviour
{
    [Header("Color Settings")]
    public Color color = Color.white;

    [Header("Emission Settings")]
    public bool enableEmission = false;
    [ColorUsage(true, true)] // Enables HDR color picker for emission
    public Color emissionColor = Color.white;
    [Range(0f, 10f)]
    public float emissionIntensity = 1f;

    [Header("Options")]
    public bool applyOnStart = true;

    private Renderer meshRenderer;
    private SpriteRenderer spriteRenderer;
    private Material materialInstance;

    private void Start()
    {
        // Try to get renderer components
        meshRenderer = GetComponent<Renderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (applyOnStart)
        {
            ApplyColor();
        }
    }

    public void ApplyColor()
    {
        // Handle SpriteRenderer (2D)
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;

            // Create material instance for emission
            if (enableEmission && spriteRenderer.material != null)
            {
                if (materialInstance == null)
                {
                    materialInstance = new Material(spriteRenderer.material);
                    spriteRenderer.material = materialInstance;
                }

                materialInstance.EnableKeyword("_EMISSION");
                materialInstance.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            }
        }
        // Handle MeshRenderer/SkinnedMeshRenderer (3D)
        else if (meshRenderer != null)
        {
            if (materialInstance == null)
            {
                materialInstance = new Material(meshRenderer.material);
                meshRenderer.material = materialInstance;
            }

            materialInstance.SetColor("_BaseColor", color); // URP
            materialInstance.SetColor("_Color", color);     // Built-in

            if (enableEmission)
            {
                materialInstance.EnableKeyword("_EMISSION");
                materialInstance.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            }
            else
            {
                materialInstance.DisableKeyword("_EMISSION");
            }
        }
        else
        {
            Debug.LogWarning("No Renderer or SpriteRenderer found on " + gameObject.name);
        }
    }

    private void OnValidate()
    {
        // Update in editor when values change
        if (Application.isPlaying)
        {
            ApplyColor();
        }
    }

    private void OnDestroy()
    {
        // Clean up material instance
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}