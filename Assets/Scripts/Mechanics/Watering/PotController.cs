using UnityEngine;

/// <summary>
/// Soil saturation simulation for the watering mechanic.
/// Water pools on top of soil, soil absorbs it over time.
/// Soil color is the primary feedback — dry → perfect → waterlogged.
/// Player judges moisture by color alone.
/// </summary>
[DisallowMultipleComponent]
public class PotController : MonoBehaviour
{
    [Header("Definition")]
    [Tooltip("Active plant definition (set by WateringManager).")]
    public PlantDefinition definition;

    [Header("Soil Visuals")]
    [Tooltip("Renderer for the soil surface (color driven by moisture).")]
    public Renderer soilRenderer;

    [Tooltip("Transform for the soil box.")]
    public Transform soilTransform;

    [Header("Water Pool Visuals")]
    [Tooltip("Renderer for the pooled water on top of soil.")]
    public Renderer waterRenderer;

    [Tooltip("Transform for the pooled water layer (scales Y with pool amount).")]
    public Transform waterTransform;

    [Header("Target Swatch")]
    [Tooltip("Small renderer showing the 'perfect' soil color as a hint.")]
    public Renderer targetSwatchRenderer;

    [Header("Target Line")]
    [Tooltip("Thin marker at the oscillating target moisture level.")]
    public Transform fillLineMarker;

    [Header("Overflow Visuals")]
    [Tooltip("Small drip boxes on the pot exterior (visible when overflowing).")]
    public Transform[] overflowDrips;

    [Header("Pot Dimensions")]
    [Tooltip("Internal height of the pot in world units.")]
    public float potWorldHeight = 0.10f;

    // ── State ────────────────────────────────────────────────────

    /// <summary>Oscillating target level set by WateringManager each frame.</summary>
    public float TargetLevel { get; set; }

    /// <summary>Actual wetness of the soil (0 = bone dry, 1 = waterlogged). Increases as pooled water absorbs.</summary>
    public float SoilMoisture => _soilMoisture;

    /// <summary>Water sitting on top of soil that hasn't absorbed yet.</summary>
    public float PooledWater => _pooledWater;

    /// <summary>True if pooled water hit the max and spilled over.</summary>
    public bool Overflowed => _overflowed;

    /// <summary>True while actively pouring.</summary>
    public bool IsPouring => _isPouring;

    /// <summary>
    /// How close soil moisture is to the perfect level (1 = perfect, 0 = way off).
    /// </summary>
    public float MoistureAccuracy
    {
        get
        {
            if (definition == null) return 0f;
            float dist = Mathf.Abs(_soilMoisture - definition.perfectMoisture);
            return Mathf.Clamp01(1f - dist / Mathf.Max(definition.moistureTolerance, 0.001f));
        }
    }

    /// <summary>
    /// Returns the soil color for a given moisture value.
    /// 0 → soilDry, perfectMoisture → soilPerfect, 1 → soilWaterlogged.
    /// </summary>
    public Color GetSoilColor(float moisture)
    {
        if (definition == null) return Color.gray;
        float perfect = definition.perfectMoisture;

        if (moisture <= perfect)
        {
            float t = perfect > 0f ? moisture / perfect : 0f;
            return Color.Lerp(definition.soilDry, definition.soilPerfect, t);
        }
        else
        {
            float t = (1f - perfect) > 0f ? (moisture - perfect) / (1f - perfect) : 1f;
            return Color.Lerp(definition.soilPerfect, definition.soilWaterlogged, t);
        }
    }

    [SerializeField] private float _soilMoisture;
    [SerializeField] private float _pooledWater;
    [SerializeField] private bool _overflowed;
    private bool _isPouring;

    private MaterialPropertyBlock _soilMPB;
    private MaterialPropertyBlock _waterMPB;
    private MaterialPropertyBlock _swatchMPB;

    // ── Public API ──────────────────────────────────────────────

    /// <summary>Add water while pouring (call each frame while held).</summary>
    public void Pour(float dt)
    {
        if (definition == null) return;
        _isPouring = true;

        _pooledWater += definition.pourRate * dt;

        // Overflow
        if (_pooledWater > definition.maxPool)
        {
            _pooledWater = definition.maxPool;
            _overflowed = true;
        }
    }

    /// <summary>Signal the end of a pour.</summary>
    public void StopPouring()
    {
        _isPouring = false;
    }

    /// <summary>Reset pot to empty for a new plant.</summary>
    public void Clear()
    {
        _soilMoisture = 0f;
        _pooledWater = 0f;
        _overflowed = false;
        _isPouring = false;

        if (waterTransform != null)
            waterTransform.gameObject.SetActive(false);
        if (overflowDrips != null)
            for (int i = 0; i < overflowDrips.Length; i++)
                if (overflowDrips[i] != null) overflowDrips[i].gameObject.SetActive(false);
    }

    // ── MonoBehaviour ───────────────────────────────────────────

    void Awake()
    {
        _soilMPB = new MaterialPropertyBlock();
        _waterMPB = new MaterialPropertyBlock();
        _swatchMPB = new MaterialPropertyBlock();

        // Auto-create soil disc if not assigned
        if (soilTransform == null)
        {
            var soilGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soilGO.name = "SoilDisc_Auto";
            soilGO.transform.SetParent(transform, false);
            soilGO.transform.localPosition = new Vector3(0f, potWorldHeight * 0.4f, 0f);
            soilGO.transform.localScale = new Vector3(potWorldRadius * 2f, 0.005f, potWorldRadius * 2f);
            var col = soilGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            soilRenderer = soilGO.GetComponent<Renderer>();
            soilTransform = soilGO.transform;
        }

        // Auto-create water disc if not assigned
        if (waterTransform == null)
        {
            var waterGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterGO.name = "WaterDisc_Auto";
            waterGO.transform.SetParent(transform, false);
            waterGO.transform.localPosition = new Vector3(0f, potWorldHeight * 0.4f, 0f);
            waterGO.transform.localScale = new Vector3(potWorldRadius * 1.8f, 0.003f, potWorldRadius * 1.8f);
            var col = waterGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
            waterRenderer = waterGO.GetComponent<Renderer>();
            waterTransform = waterGO.transform;
            waterGO.SetActive(false);
        }
    }

    void Update()
    {
        if (definition == null) return;
        SimulateAbsorption(Time.deltaTime);
        UpdateVisuals();
    }

    // ── Simulation ──────────────────────────────────────────────

    private void SimulateAbsorption(float dt)
    {
        if (_pooledWater > 0f)
        {
            // Soil soaks up pooled water at its absorption rate
            float absorb = Mathf.Min(_pooledWater, definition.absorptionRate * dt);

            // Soil can't exceed 1.0 moisture
            float headroom = 1f - _soilMoisture;
            absorb = Mathf.Min(absorb, headroom);

            _soilMoisture += absorb;
            _pooledWater -= absorb;

            // If soil is fully saturated, excess pool drains away
            if (_soilMoisture >= 1f && _pooledWater > 0f)
            {
                _pooledWater = Mathf.MoveTowards(_pooledWater, 0f, definition.overflowDrainRate * dt);
                _overflowed = true;
            }
        }

        _soilMoisture = Mathf.Clamp01(_soilMoisture);
        _pooledWater = Mathf.Max(_pooledWater, 0f);
    }

    // ── Visuals ─────────────────────────────────────────────────

    private void UpdateVisuals()
    {
        float h = potWorldHeight;

        // Soil color driven by moisture
        if (soilRenderer != null)
        {
            Color col = GetSoilColor(_soilMoisture);
            soilRenderer.GetPropertyBlock(_soilMPB);
            _soilMPB.SetColor("_BaseColor", col);
            soilRenderer.SetPropertyBlock(_soilMPB);
        }

        // Target swatch — shows the perfect color as a hint
        if (targetSwatchRenderer != null)
        {
            Color perfectCol = definition.soilPerfect;
            targetSwatchRenderer.GetPropertyBlock(_swatchMPB);
            _swatchMPB.SetColor("_BaseColor", perfectCol);
            targetSwatchRenderer.SetPropertyBlock(_swatchMPB);
        }

        // Fill line tracks oscillating target
        if (fillLineMarker != null)
        {
            float fillY = TargetLevel * h;
            fillLineMarker.localPosition = new Vector3(0f, fillY, 0f);
        }

        // Pooled water disc on top of soil — deep blue, rises as pool grows
        if (waterTransform != null)
        {
            bool showPool = _pooledWater > 0.01f;
            waterTransform.gameObject.SetActive(showPool);

            if (showPool)
            {
                float soilTop = h * 0.8f;
                float poolRise = _pooledWater * h * 0.3f;
                // Disc sits just above soil, rises slightly as pool grows
                waterTransform.localPosition = new Vector3(0f, soilTop + poolRise * 0.5f + 0.002f, 0f);
                // Thin disc that gets slightly thicker with more water
                waterTransform.localScale = new Vector3(
                    waterTransform.localScale.x,
                    Mathf.Max(poolRise, 0.002f),
                    waterTransform.localScale.z);

                if (waterRenderer != null)
                {
                    // Deep blue that darkens as pool grows
                    float depth = Mathf.Clamp01(_pooledWater * 2f);
                    Color waterCol = Color.Lerp(
                        new Color(0.2f, 0.4f, 0.75f), // light blue (shallow)
                        new Color(0.08f, 0.15f, 0.45f), // deep blue (full)
                        depth);
                    waterRenderer.GetPropertyBlock(_waterMPB);
                    _waterMPB.SetColor("_BaseColor", waterCol);
                    waterRenderer.SetPropertyBlock(_waterMPB);
                }
            }
        }

        // Overflow drips
        if (overflowDrips != null)
        {
            for (int i = 0; i < overflowDrips.Length; i++)
            {
                if (overflowDrips[i] == null) continue;
                overflowDrips[i].gameObject.SetActive(_overflowed && _pooledWater > 0.05f);
            }
        }
    }
}
