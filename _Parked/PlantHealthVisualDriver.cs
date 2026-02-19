using UnityEngine;

/// <summary>
/// Per-plant component that drives the Iris/Plant shader _Health property
/// via MaterialPropertyBlock. Registered with PlantHealthTracker on Awake.
/// </summary>
public class PlantHealthVisualDriver : MonoBehaviour
{
    [Tooltip("Unique identifier for this plant (set by scene builder).")]
    [SerializeField] private string _plantId;

    [Tooltip("Renderers for stem and leaves (NOT the pot). Shader must be Iris/Plant.")]
    [SerializeField] private Renderer[] _plantRenderers;

    private MaterialPropertyBlock _mpb;
    private static readonly int HealthProp = Shader.PropertyToID("_Health");

    public string PlantId => _plantId;
    public Renderer[] PlantRenderers => _plantRenderers;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        if (PlantHealthTracker.Instance != null)
            PlantHealthTracker.Instance.RegisterPlant(_plantId);
    }

    /// <summary>Apply a health value (0-1) to all plant renderers.</summary>
    public void ApplyHealth(float health)
    {
        if (_plantRenderers == null || _mpb == null) return;

        _mpb.SetFloat(HealthProp, health);
        for (int i = 0; i < _plantRenderers.Length; i++)
        {
            if (_plantRenderers[i] != null)
                _plantRenderers[i].SetPropertyBlock(_mpb);
        }
    }
}
