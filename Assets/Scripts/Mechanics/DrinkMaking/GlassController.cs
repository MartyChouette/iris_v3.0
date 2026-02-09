using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core liquid/foam simulation for the drink-making prototype.
/// Tracks fill level, foam level, blended colour, and overflow state.
/// Called by <see cref="DrinkMakingManager"/> during the pouring phase.
/// </summary>
[DisallowMultipleComponent]
public class GlassController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Glass shape definition (capacity, fill line, foam headroom).")]
    public GlassDefinition definition;

    [Tooltip("Renderer for the liquid visual inside the glass.")]
    public Renderer liquidRenderer;

    [Tooltip("Renderer for the foam layer on top of the liquid.")]
    public Renderer foamRenderer;

    [Tooltip("Small marker showing the target fill height.")]
    public Transform fillLineMarker;

    [Tooltip("Transform scaled to show liquid height.")]
    public Transform liquidTransform;

    [Tooltip("Transform scaled/positioned to show foam height.")]
    public Transform foamTransform;

    [Header("State (Read-Only)")]
    [SerializeField] private float _liquidLevel;
    [SerializeField] private float _foamLevel;
    [SerializeField] private Color _currentColor = Color.clear;
    [SerializeField] private bool _overflowed;

    // Internal
    private float _rushPenalty;
    private float _totalPoured;
    private readonly List<(DrinkIngredientDefinition ingredient, float amount)> _ingredients
        = new List<(DrinkIngredientDefinition, float)>();

    // ── Public read-only API ────────────────────────────────────────────

    public float LiquidLevel => _liquidLevel;
    public float FoamLevel => _foamLevel;
    public bool Overflowed => _overflowed;
    public Color CurrentColor => _currentColor;

    /// <summary>
    /// How close the liquid level is to the fill line (1 = perfect, 0 = way off).
    /// </summary>
    public float FillAccuracy
    {
        get
        {
            if (definition == null) return 0f;
            float dist = Mathf.Abs(_liquidLevel - definition.fillLineNormalized);
            return Mathf.Clamp01(1f - dist / Mathf.Max(definition.fillLineTolerance, 0.001f));
        }
    }

    // ── Pour API (called by DrinkMakingManager) ────────────────────────

    /// <summary>
    /// Pour a given ingredient for one frame. Advances liquid and foam levels.
    /// </summary>
    public void Pour(DrinkIngredientDefinition ingredient, float dt)
    {
        if (definition == null || ingredient == null) return;

        float liquidDelta = ingredient.pourRate * dt;
        _liquidLevel += liquidDelta;
        _totalPoured += liquidDelta;

        // Foam rises faster for fizzy ingredients
        float foamDelta = liquidDelta * ingredient.foamRateMultiplier * (1f + ingredient.fizziness);
        foamDelta += _rushPenalty * dt;
        _foamLevel += foamDelta;

        // Rush penalty increases while continuously pouring fizzy drinks
        _rushPenalty += ingredient.fizziness * 0.1f * dt;

        // Track ingredient contribution
        AddIngredient(ingredient, liquidDelta);

        // Clamp liquid to 0-1
        _liquidLevel = Mathf.Clamp01(_liquidLevel);

        // Foam can exceed 1.0 → overflow
        if (_foamLevel > 1f)
        {
            _foamLevel = 1f;
            _overflowed = true;
        }

        // Foam never below liquid
        _foamLevel = Mathf.Max(_foamLevel, _liquidLevel);
    }

    /// <summary>
    /// Signal the end of a pour. Resets rush accumulator.
    /// </summary>
    public void StopPouring()
    {
        _rushPenalty = 0f;
    }

    /// <summary>
    /// Reset the glass to empty.
    /// </summary>
    public void Clear()
    {
        _liquidLevel = 0f;
        _foamLevel = 0f;
        _currentColor = Color.clear;
        _overflowed = false;
        _rushPenalty = 0f;
        _totalPoured = 0f;
        _ingredients.Clear();
    }

    // ── MonoBehaviour ──────────────────────────────────────────────────

    void Update()
    {
        SettleFoam(Time.deltaTime);
        UpdateVisuals();
    }

    // ── Internals ──────────────────────────────────────────────────────

    private void SettleFoam(float dt)
    {
        if (_ingredients.Count == 0) return;

        // Weighted average of settle rates
        float weightedSettle = 0f;
        float totalWeight = 0f;
        for (int i = 0; i < _ingredients.Count; i++)
        {
            float w = _ingredients[i].amount;
            weightedSettle += _ingredients[i].ingredient.foamSettleRate * w;
            totalWeight += w;
        }
        if (totalWeight > 0f)
            weightedSettle /= totalWeight;

        _foamLevel = Mathf.MoveTowards(_foamLevel, _liquidLevel, weightedSettle * dt);
    }

    private void AddIngredient(DrinkIngredientDefinition ingredient, float amount)
    {
        // Find existing entry
        for (int i = 0; i < _ingredients.Count; i++)
        {
            if (_ingredients[i].ingredient == ingredient)
            {
                _ingredients[i] = (ingredient, _ingredients[i].amount + amount);
                BlendColor();
                return;
            }
        }
        _ingredients.Add((ingredient, amount));
        BlendColor();
    }

    private void BlendColor()
    {
        if (_ingredients.Count == 0)
        {
            _currentColor = Color.clear;
            return;
        }

        float r = 0f, g = 0f, b = 0f, a = 0f;
        float totalWeight = 0f;
        for (int i = 0; i < _ingredients.Count; i++)
        {
            float w = _ingredients[i].amount;
            Color c = _ingredients[i].ingredient.liquidColor;
            r += c.r * w;
            g += c.g * w;
            b += c.b * w;
            a += c.a * w;
            totalWeight += w;
        }

        if (totalWeight > 0f)
        {
            _currentColor = new Color(r / totalWeight, g / totalWeight,
                                      b / totalWeight, a / totalWeight);
        }
    }

    private void UpdateVisuals()
    {
        if (definition == null) return;

        float glassHeight = definition.worldHeight;

        // Liquid transform — scale Y from bottom
        if (liquidTransform != null)
        {
            float liquidHeight = _liquidLevel * glassHeight;
            liquidTransform.localScale = new Vector3(
                liquidTransform.localScale.x,
                Mathf.Max(liquidHeight, 0.001f),
                liquidTransform.localScale.z);
            liquidTransform.localPosition = new Vector3(0f, liquidHeight * 0.5f, 0f);
        }

        // Liquid renderer colour
        if (liquidRenderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            liquidRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", _currentColor);
            liquidRenderer.SetPropertyBlock(mpb);
        }

        // Foam transform — sits on top of liquid, height = foam - liquid
        if (foamTransform != null)
        {
            float liquidHeight = _liquidLevel * glassHeight;
            float foamHeight = (_foamLevel - _liquidLevel) * glassHeight;
            foamHeight = Mathf.Max(foamHeight, 0f);

            foamTransform.localScale = new Vector3(
                foamTransform.localScale.x,
                Mathf.Max(foamHeight, 0.001f),
                foamTransform.localScale.z);
            foamTransform.localPosition = new Vector3(0f, liquidHeight + foamHeight * 0.5f, 0f);
        }

        // Fill line marker
        if (fillLineMarker != null)
        {
            float fillY = definition.fillLineNormalized * glassHeight;
            fillLineMarker.localPosition = new Vector3(0f, fillY, 0f);
        }
    }
}
