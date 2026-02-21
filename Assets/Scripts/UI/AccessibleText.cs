using UnityEngine;
using TMPro;

/// <summary>
/// Lightweight component on TMP_Text objects that scales font size
/// based on AccessibilitySettings.TextScale. Subscribes to OnSettingsChanged
/// for live updates.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class AccessibleText : MonoBehaviour
{
    private TMP_Text _text;
    private float _baseFontSize;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _baseFontSize = _text.fontSize;
    }

    private void OnEnable()
    {
        AccessibilitySettings.OnSettingsChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        AccessibilitySettings.OnSettingsChanged -= Apply;
    }

    private void Apply()
    {
        if (_text == null) return;
        _text.fontSize = _baseFontSize * AccessibilitySettings.TextScale;
    }
}
