using UnityEngine;

/// <summary>
/// Scene-scoped singleton that tracks scissor sharpness. Each cut dulls the tool,
/// adding angle noise and slowing cut speed. The player must visit a sharpening
/// stone to restore sharpness.
/// </summary>
[DisallowMultipleComponent]
public class ToolCondition : MonoBehaviour
{
    public static ToolCondition Instance { get; private set; }

    [Header("Sharpness")]
    [Tooltip("Current sharpness (1 = sharp, 0 = completely dull).")]
    [Range(0f, 1f)]
    public float sharpness = 1f;

    [Tooltip("Sharpness lost per cut.")]
    public float dullPerCut = 0.15f;

    [Tooltip("Minimum sharpness floor.")]
    [Range(0f, 1f)]
    public float minSharpness = 0.1f;

    [Header("Effects")]
    [Tooltip("Max degrees of random angle offset when fully dull.")]
    public float angleNoiseAtMinSharpness = 25f;

    [Tooltip("Cut speed multiplier when fully dull (1.0 at sharp).")]
    [Range(0.1f, 1f)]
    public float cutSpeedMultiplier = 0.3f;

    [Header("References")]
    [Tooltip("The flower brain — angleOffsetDeg is written to simulate imprecise cuts.")]
    public FlowerGameBrain brain;

    [Tooltip("The cutting plane controller — axisMoveSpeed is modulated by sharpness.")]
    public CuttingPlaneController planeController;

    /// <summary>Public read-only accessor.</summary>
    public float Sharpness => sharpness;

    // Cached originals
    private float _originalAxisMoveSpeed;
    private float _lastStemLength;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (planeController != null)
            _originalAxisMoveSpeed = planeController.axisMoveSpeed;

        if (brain != null && brain.stem != null)
            _lastStemLength = brain.stem.CurrentLength;
    }

    void Update()
    {
        if (brain == null) return;

        // Detect cut by stem length change
        if (brain.stem != null)
        {
            float currentLen = brain.stem.CurrentLength;
            if (_lastStemLength > 0f && currentLen < _lastStemLength - 0.01f)
            {
                OnCutPerformed();
            }
            _lastStemLength = currentLen;
        }

        // Apply angle noise
        float dullness = 1f - sharpness;
        float noise = Mathf.Lerp(0f, angleNoiseAtMinSharpness, dullness);
        brain.angleOffsetDeg = Random.Range(-noise, noise);

        // Apply speed multiplier
        if (planeController != null)
        {
            planeController.axisMoveSpeed = Mathf.Lerp(
                _originalAxisMoveSpeed * cutSpeedMultiplier,
                _originalAxisMoveSpeed,
                sharpness);
        }
    }

    private void OnCutPerformed()
    {
        sharpness = Mathf.Max(minSharpness, sharpness - dullPerCut);
        Debug.Log($"[ToolCondition] Cut performed. Sharpness: {sharpness:F2}");
    }

    /// <summary>Restore sharpness by the given amount (called by SharpeningMinigame).</summary>
    public void Sharpen(float amount)
    {
        sharpness = Mathf.Clamp01(sharpness + amount);
    }
}
