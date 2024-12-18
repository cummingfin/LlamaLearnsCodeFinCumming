using UnityEngine;

public class ComponentHighlighter : MonoBehaviour
{
    public Material highlightMaterial; // Assign highlight material in the Inspector
    private Material originalMaterial;
    private Renderer componentRenderer;

    void Start()
    {
        componentRenderer = GetComponent<Renderer>();
        if (componentRenderer != null)
        {
            originalMaterial = componentRenderer.material;
        }
    }

    public void Highlight()
    {
        if (componentRenderer != null)
        {
            componentRenderer.material = highlightMaterial;
        }
    }

    public void RemoveHighlight()
    {
        if (componentRenderer != null)
        {
            componentRenderer.material = originalMaterial;
        }
    }
}
