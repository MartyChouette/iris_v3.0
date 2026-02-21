using UnityEngine;
using TMPro;

/// <summary>
/// Centralized text theme ScriptableObject. Drop one in Resources/ so it auto-loads.
/// All AccessibleText components read font, color, and size overrides from here.
/// Changing the asset in the Inspector updates every themed text at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Text Theme", fileName = "IrisTextTheme")]
public class IrisTextTheme : ScriptableObject
{
    // ── Singleton access (loaded from Resources) ────────────────────

    private static IrisTextTheme s_instance;

    /// <summary>
    /// The active theme. Loaded once from Resources/IrisTextTheme.
    /// Returns null if no theme asset exists (texts use their defaults).
    /// </summary>
    public static IrisTextTheme Active
    {
        get
        {
            if (s_instance == null)
                s_instance = Resources.Load<IrisTextTheme>("IrisTextTheme");
            return s_instance;
        }
    }

    /// <summary>Fires when any theme property is changed at runtime.</summary>
    public static event System.Action OnThemeChanged;

    // ── Theme Properties ────────────────────────────────────────────

    [Header("Font")]
    [Tooltip("Primary font used for all themed text. Leave null to keep each text's original font.")]
    public TMP_FontAsset primaryFont;

    [Tooltip("Secondary font for accents, headers, etc. Used by AccessibleText when role = Header.")]
    public TMP_FontAsset headerFont;

    [Header("Colors")]
    [Tooltip("Default text color for body text.")]
    public Color bodyColor = Color.white;

    [Tooltip("Header / title text color.")]
    public Color headerColor = new Color(1f, 0.95f, 0.85f);

    [Tooltip("Subtitle / secondary text color.")]
    public Color subtitleColor = new Color(0.75f, 0.75f, 0.75f);

    [Tooltip("Accent color for highlights and interactive text.")]
    public Color accentColor = new Color(0.4f, 0.9f, 0.6f);

    [Header("Size Multipliers")]
    [Tooltip("Global size multiplier applied on top of AccessibilitySettings.TextScale.")]
    [Range(0.5f, 2f)]
    public float globalSizeMultiplier = 1f;

    [Tooltip("Size multiplier specifically for headers.")]
    [Range(0.5f, 3f)]
    public float headerSizeMultiplier = 1.2f;

    [Header("Spacing")]
    [Tooltip("Character spacing adjustment (em units). 0 = default.")]
    public float characterSpacing;

    [Tooltip("Line spacing adjustment (em units). 0 = default.")]
    public float lineSpacing;

    // ── Runtime Notification ────────────────────────────────────────

    /// <summary>Call this after changing theme properties at runtime to update all text.</summary>
    public void NotifyChanged()
    {
        OnThemeChanged?.Invoke();
    }

    private void OnValidate()
    {
        // Fire in editor so scene text updates live when tweaking the SO
        OnThemeChanged?.Invoke();
    }
}
