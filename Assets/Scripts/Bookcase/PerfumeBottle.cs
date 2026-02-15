using UnityEngine;

/// <summary>
/// A perfume bottle on the bookcase shelf. Single click to spray —
/// big particle burst, mood set, bottle stays on shelf.
/// </summary>
public class PerfumeBottle : MonoBehaviour
{
    [Header("Definition")]
    [Tooltip("ScriptableObject defining this perfume.")]
    [SerializeField] private PerfumeDefinition definition;

    [Header("Spray")]
    [Tooltip("Child ParticleSystem for the spray mist.")]
    [SerializeField] private ParticleSystem sprayParticles;

    [Tooltip("Number of particles to emit in the one-shot burst.")]
    [SerializeField] private int burstCount = 30;

    public PerfumeDefinition Definition => definition;
    public bool SprayComplete { get; private set; }

    private Vector3 _shelfPosition;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private void Awake()
    {
        _shelfPosition = transform.position;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }

        if (sprayParticles != null)
            sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void SetDefinition(PerfumeDefinition def) => definition = def;

    public void SetSprayParticles(ParticleSystem ps) => sprayParticles = ps;

    public void OnHoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;

        transform.position = _shelfPosition - transform.forward * 0.02f;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        transform.position = _shelfPosition;
    }

    /// <summary>
    /// One-click spray: burst particles, set mood, mark complete. Bottle stays on shelf.
    /// </summary>
    public void SprayOnce()
    {
        if (SprayComplete) return;

        // Burst particles
        if (sprayParticles != null)
            sprayParticles.Emit(burstCount);

        // SFX
        if (definition != null && definition.spraySFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(definition.spraySFX);

        // Mood
        if (definition != null && MoodMachine.Instance != null)
            MoodMachine.Instance.SetSource("Perfume", definition.moodValue);

        // Reactable
        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = true;

        SprayComplete = true;
    }

    private void OnDestroy()
    {
        if (_instanceMaterial != null)
            Destroy(_instanceMaterial);
    }
}
