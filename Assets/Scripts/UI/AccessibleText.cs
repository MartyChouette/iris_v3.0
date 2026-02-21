using UnityEngine;
using TMPro;

/// <summary>
/// Lightweight component on TMP_Text objects that applies the IrisTextTheme
/// (font, color, spacing) and scales font size based on AccessibilitySettings.TextScale.
/// Subscribes to both OnSettingsChanged and OnThemeChanged for live updates.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class AccessibleText : MonoBehaviour
{
    /// <summary>Determines which theme color and font to use.</summary>
    public enum TextRole { Body, Header, Subtitle, Accent }

    [Tooltip("Which theme slot this text uses for color and font.")]
    [SerializeField] private TextRole _role = TextRole.Body;

    [Tooltip("If true, the theme overrides this text's font asset.")]
    [SerializeField] private bool _applyFont = true;

    [Tooltip("If true, the theme overrides this text's color.")]
    [SerializeField] private bool _applyColor = true;

    [Tooltip("If true, the theme applies spacing adjustments.")]
    [SerializeField] private bool _applySpacing = true;

    private TMP_Text _text;
    private float _baseFontSize;
    private TMP_FontAsset _originalFont;
    private Color _originalColor;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _baseFontSize = _text.fontSize;
        _originalFont = _text.font;
        _originalColor = _text.color;
    }

    private void OnEnable()
    {
        AccessibilitySettings.OnSettingsChanged += Apply;
        IrisTextTheme.OnThemeChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        AccessibilitySettings.OnSettingsChanged -= Apply;
        IrisTextTheme.OnThemeChanged -= Apply;
    }

    private void Apply()
    {
        if (_text == null) return;

        var theme = IrisTextTheme.Active;

        // ── Font ────────────────────────────────────────────────
        if (_applyFont && theme != null)
        {
            TMP_FontAsset font = _role == TextRole.Header && theme.headerFont != null
                ? theme.headerFont
                : theme.primaryFont;

            if (font != null)
                _text.font = font;
            else
                _text.font = _originalFont;
        }

        // ── Color ───────────────────────────────────────────────
        if (_applyColor && theme != null)
        {
            _text.color = _role switch
            {
                TextRole.Header   => theme.headerColor,
                TextRole.Subtitle => theme.subtitleColor,
                TextRole.Accent   => theme.accentColor,
                _                 => theme.bodyColor
            };
        }

        // ── Size ────────────────────────────────────────────────
        float sizeMultiplier = AccessibilitySettings.TextScale;
        if (theme != null)
        {
            sizeMultiplier *= theme.globalSizeMultiplier;
            if (_role == TextRole.Header)
                sizeMultiplier *= theme.headerSizeMultiplier;
        }
        _text.fontSize = _baseFontSize * sizeMultiplier;

        // ── Spacing ─────────────────────────────────────────────
        if (_applySpacing && theme != null)
        {
            _text.characterSpacing = theme.characterSpacing;
            _text.lineSpacing = theme.lineSpacing;
        }
    }
}
