using UnityEngine;

/// <summary>
/// Soil/water/foam simulation for the watering prototype.
/// Adapted from <see cref="GlassController"/> — uses Transform scaling for visual levels.
/// Dirt foams up when watered (homage to drink-making foam mechanic).
/// </summary>
[DisallowMultipleComponent]
public class PotController : MonoBehaviour
{
    [Header("Definition")]
    [Tooltip("Active plant definition (set by WateringManager when entering watering state).")]
    public PlantDefinition definition;

    [Header("Visuals")]
    [Tooltip("Renderer for the soil box (colour lerps dry to wet).")]
    public Renderer soilRenderer;

    [Tooltip("Transform for the soil box (scales Y with water level).")]
    public Transform soilTransform;

    [Tooltip("Renderer for the foam box (bubbly dirt on top).")]
    public Renderer foamRenderer;

    [Tooltip("Transform for the foam box (scales Y with foam minus water).")]
    public Transform foamTransform;

    [Tooltip("Thin marker at the ideal water level.")]
    public Transform fillLineMarker;

    [Tooltip("Thin marker tracking the current water level.")]
    public Transform waterLineMarker;

    [Header("Pot Dimensions")]
    [Tooltip("Internal height of the pot in world units.")]
    public float potWorldHeight = 0.10f;

    [Tooltip("Visual radius of the pot interior.")]
    public float potWorldRadius = 0.04f;

    // ── State (read-only from outside) ───────────────────────────────

    [Header("State (Read-Only)")]
    [SerializeField] private float _waterLevel;
    [SerializeField] private float _foamLevel;
    [SerializeField] private bool _overflowed;

    private bool _isPouring;
    private MaterialPropertyBlock _soilMPB;
    private MaterialPropertyBlock _foamMPB;

    // ── Public API ───────────────────────────────────────────────────

    public float WaterLevel => _waterLevel;
    public float FoamLevel => _foamLevel;
    public bool Overflowed => _overflowed;

    /// <summary>
    /// How close the water level is to the ideal fill line (1 = perfect, 0 = way off).
    /// </summary>
    public float FillAccuracy
    {
        get
        {
            if (definition == null) return 0f;
            float dist = Mathf.Abs(_waterLevel - definition.idealWaterLevel);
            return Mathf.Clamp01(1f - dist / Mathf.Max(definition.waterTolerance, 0.001f));
        }
    }

    /// <summary>
    /// Add water while pouring (call each frame while mouse held).
    /// </summary>
    public void Pour(float dt)
    {
        if (definition == null) return;

        float liquidDelta = definition.pourRate * dt;
        _waterLevel += liquidDelta;

        // Foam rises FASTER than water (dirt bubbles up)
        float foamDelta = liquidDelta * definition.foamRateMultiplier;
        _foamLevel += foamDelta;

        // Clamp water to 0-1
        _waterLevel = Mathf.Clamp01(_waterLevel);

        // Foam can exceed 1.0 → overflow
        if (_foamLevel > 1f)
        {
            _foamLevel = 1f;
            _overflowed = true;
        }

        // Foam never below water
        _foamLevel = Mathf.Max(_foamLevel, _waterLevel);

        _isPouring = true;
    }

    /// <summary>
    /// Signal the end of a pour.
    /// </summary>
    public void StopPouring()
    {
        _isPouring = false;
    }

    /// <summary>
    /// Reset pot to empty for a new plant.
    /// </summary>
    public void Clear()
    {
        _waterLevel = 0f;
        _foamLevel = 0f;
        _overflowed = false;
        _isPouring = false;
    }

    // ── MonoBehaviour ────────────────────────────────────────────────

    void Awake()
    {
        _soilMPB = new MaterialPropertyBlock();
        _foamMPB = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (definition == null) return;

        SettleFoam(Time.deltaTime);
        UpdateVisuals();
    }

    // ── Internals ────────────────────────────────────────────────────

    private void SettleFoam(float dt)
    {
        if (!_isPouring)
        {
            // Foam settles toward water level
            _foamLevel = Mathf.MoveTowards(_foamLevel, _waterLevel, definition.foamSettleRate * dt);
        }
    }

    private void UpdateVisuals()
    {
        float h = potWorldHeight;

        // Soil colour lerp (dry → wet based on water level)
        if (soilRenderer != null)
        {
            Color soilColor = Color.Lerp(definition.dryColor, definition.wetColor, _waterLevel);
            soilRenderer.GetPropertyBlock(_soilMPB);
            _soilMPB.SetColor("_BaseColor", soilColor);
            soilRenderer.SetPropertyBlock(_soilMPB);
        }

        // Soil transform — scales Y with water level
        if (soilTransform != null)
        {
            float soilHeight = Mathf.Max(_waterLevel * h, 0.001f);
            soilTransform.localScale = new Vector3(
                soilTransform.localScale.x,
                soilHeight,
                soilTransform.localScale.z);
            soilTransform.localPosition = new Vector3(0f, soilHeight * 0.5f, 0f);
        }

        // Foam transform — sits on top of soil, height = foam - water
        if (foamTransform != null)
        {
            float soilHeight = _waterLevel * h;
            float foamHeight = Mathf.Max((_foamLevel - _waterLevel) * h, 0f);

            foamTransform.localScale = new Vector3(
                foamTransform.localScale.x,
                Mathf.Max(foamHeight, 0.001f),
                foamTransform.localScale.z);
            foamTransform.localPosition = new Vector3(0f, soilHeight + foamHeight * 0.5f, 0f);

            // Foam colour via MPB
            if (foamRenderer != null)
            {
                foamRenderer.GetPropertyBlock(_foamMPB);
                _foamMPB.SetColor("_BaseColor", definition.foamColor);
                foamRenderer.SetPropertyBlock(_foamMPB);
            }
        }

        // Fill line at ideal water level
        if (fillLineMarker != null)
        {
            float fillY = definition.idealWaterLevel * h;
            fillLineMarker.localPosition = new Vector3(0f, fillY, 0f);
        }

        // Current water line
        if (waterLineMarker != null)
        {
            float waterY = _waterLevel * h;
            waterLineMarker.localPosition = new Vector3(0f, waterY, 0f);
        }
    }
}
