using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD for <see cref="ToolCondition"/> — displays a sharpness bar with color
/// lerp and text hints.
/// </summary>
[DisallowMultipleComponent]
public class ToolConditionHUD : MonoBehaviour
{
    [Header("References")]
    public ToolCondition tool;

    [Header("UI Elements")]
    public TMP_Text sharpnessLabel;
    public Image sharpnessBar;
    public TMP_Text hintLabel;

    [Header("Colors")]
    [Tooltip("Color when fully sharp.")]
    public Color sharpColor = new Color(0.4f, 0.6f, 0.85f);

    [Tooltip("Color when fully dull.")]
    public Color dullColor = new Color(0.85f, 0.5f, 0.2f);

    void Update()
    {
        if (tool == null) return;

        float s = tool.Sharpness;

        // Bar fill
        if (sharpnessBar != null)
        {
            sharpnessBar.fillAmount = s;
            sharpnessBar.color = Color.Lerp(dullColor, sharpColor, s);
        }

        // Label
        if (sharpnessLabel != null)
        {
            if (s > 0.7f)
                sharpnessLabel.text = "Sharp";
            else if (s > 0.4f)
                sharpnessLabel.text = "Worn";
            else
                sharpnessLabel.text = "Dull";
        }

        // Hint
        if (hintLabel != null)
        {
            bool showHint = s < 0.5f;
            hintLabel.gameObject.SetActive(showHint);
            if (showHint)
                hintLabel.text = "Press Q to sharpen";
        }
    }
}
