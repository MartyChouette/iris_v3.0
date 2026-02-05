using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Periodically rolls to wilt flower parts on a timer. Each tick, every
/// non-crown attached part with Normal condition has a chance to wither.
/// Withered parts are tinted darker as visual feedback.
/// </summary>
[DisallowMultipleComponent]
public class WiltingClock : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The flower brain whose parts will be wilted.")]
    public FlowerGameBrain brain;

    [Header("Timing")]
    [Tooltip("Seconds between wilt ticks.")]
    public float wiltInterval = 5f;

    [Header("Chance")]
    [Tooltip("Per-part probability of wilting each tick (0-1).")]
    [Range(0f, 1f)]
    public float wiltChancePerTick = 0.3f;

    [Header("Options")]
    [Tooltip("If true, the crown part can also wilt.")]
    public bool crownCanWilt;

    [Tooltip("If true, parts wilt in random order. Otherwise iterates in list order.")]
    public bool randomOrder = true;

    // Runtime
    private float _timer;
    private Dictionary<FlowerPartRuntime, Color> _originalColors = new Dictionary<FlowerPartRuntime, Color>();
    private static readonly Color WiltTint = new Color(0.45f, 0.35f, 0.25f);

    /// <summary>Time remaining until the next wilt tick.</summary>
    public float TimeUntilNextTick => Mathf.Max(0f, wiltInterval - _timer);

    /// <summary>Number of currently wilted parts.</summary>
    public int WiltedCount
    {
        get
        {
            if (brain == null) return 0;
            int count = 0;
            for (int i = 0; i < brain.parts.Count; i++)
            {
                if (brain.parts[i] != null && brain.parts[i].condition == FlowerPartCondition.Withered)
                    count++;
            }
            return count;
        }
    }

    void Awake()
    {
        CacheOriginalColors();
    }

    void Update()
    {
        if (brain == null) return;

        _timer += Time.deltaTime;
        if (_timer >= wiltInterval)
        {
            _timer = 0f;
            DoWiltTick();
        }
    }

    private void CacheOriginalColors()
    {
        if (brain == null) return;
        _originalColors.Clear();
        for (int i = 0; i < brain.parts.Count; i++)
        {
            var part = brain.parts[i];
            if (part == null) continue;
            var rend = part.GetComponentInChildren<Renderer>();
            if (rend != null && rend.material != null)
                _originalColors[part] = rend.material.color;
        }
    }

    private void DoWiltTick()
    {
        var candidates = new List<FlowerPartRuntime>();
        for (int i = 0; i < brain.parts.Count; i++)
        {
            var part = brain.parts[i];
            if (part == null || !part.isAttached) continue;
            if (part.condition != FlowerPartCondition.Normal) continue;
            if (!crownCanWilt && part.kind == FlowerPartKind.Crown) continue;
            candidates.Add(part);
        }

        if (randomOrder)
        {
            // Fisher-Yates shuffle
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (Random.value < wiltChancePerTick)
            {
                WiltPart(candidates[i]);
            }
        }
    }

    private void WiltPart(FlowerPartRuntime part)
    {
        part.condition = FlowerPartCondition.Withered;

        // Tint visual
        var rend = part.GetComponentInChildren<Renderer>();
        if (rend != null && rend.material != null)
        {
            rend.material.color = Color.Lerp(
                _originalColors.ContainsKey(part) ? _originalColors[part] : rend.material.color,
                WiltTint,
                0.7f);
        }

        Debug.Log($"[WiltingClock] {part.name} withered.");
    }
}
