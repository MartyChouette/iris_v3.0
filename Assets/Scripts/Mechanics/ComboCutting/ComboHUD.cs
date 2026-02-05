using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for <see cref="ComboManager"/>. Shows combo count, multiplier,
/// and countdown timer. Fades in when combo is active.
/// </summary>
[DisallowMultipleComponent]
public class ComboHUD : MonoBehaviour
{
    [Header("References")]
    public ComboManager combo;

    [Header("UI Elements")]
    public TMP_Text comboLabel;
    public TMP_Text multiplierLabel;
    public TMP_Text timerLabel;
    public CanvasGroup root;

    void OnEnable()
    {
        if (combo != null)
        {
            combo.OnComboChanged.AddListener(OnComboChanged);
            combo.OnComboBroken.AddListener(OnComboBroken);
        }
    }

    void OnDisable()
    {
        if (combo != null)
        {
            combo.OnComboChanged.RemoveListener(OnComboChanged);
            combo.OnComboBroken.RemoveListener(OnComboBroken);
        }
    }

    void Update()
    {
        if (combo == null) return;

        // Timer
        if (timerLabel != null && combo.comboCount > 0)
        {
            float remaining = Mathf.Max(0f, combo.comboWindow - combo.comboTimer);
            timerLabel.SetText("{0:1}s", remaining);
        }

        // Fade
        if (root != null)
        {
            float targetAlpha = combo.comboCount > 0 ? 1f : 0f;
            root.alpha = Mathf.MoveTowards(root.alpha, targetAlpha, Time.deltaTime * 4f);
        }
    }

    private void OnComboChanged(int count, float multiplier)
    {
        if (comboLabel != null)
            comboLabel.SetText("COMBO x{0}", count);

        if (multiplierLabel != null)
            multiplierLabel.SetText("{0:1}x", multiplier);
    }

    private void OnComboBroken()
    {
        if (comboLabel != null)
            comboLabel.text = "";
        if (multiplierLabel != null)
            multiplierLabel.text = "";
        if (timerLabel != null)
            timerLabel.text = "";
    }
}
