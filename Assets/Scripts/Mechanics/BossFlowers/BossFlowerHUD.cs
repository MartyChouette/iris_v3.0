using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for <see cref="BossFlowerController"/>.
/// Shows boss name, snap-trap warning, and regrowth indicator.
/// </summary>
[DisallowMultipleComponent]
public class BossFlowerHUD : MonoBehaviour
{
    [Header("References")]
    public BossFlowerController boss;

    [Header("UI Elements")]
    public TMP_Text nameLabel;
    public TMP_Text phaseLabel;
    public TMP_Text warningLabel;
    public CanvasGroup warningFlash;

    private static readonly Color WarningColor = new Color(0.95f, 0.2f, 0.15f);

    void Update()
    {
        if (boss == null) return;

        // Boss name
        if (nameLabel != null)
            nameLabel.text = boss.bossName;

        // Phase / state
        if (phaseLabel != null)
        {
            if (boss.IsRegrowing)
                phaseLabel.text = "REGROWING";
            else if (boss.enableSnapTrap)
                phaseLabel.text = "ACTIVE";
            else
                phaseLabel.text = "";
        }

        // Snap warning
        if (boss.enableSnapTrap)
        {
            float remaining = boss.SnapTimeRemaining;
            bool danger = remaining < 1.5f && remaining > 0f;

            if (warningLabel != null)
            {
                warningLabel.gameObject.SetActive(danger);
                if (danger)
                {
                    warningLabel.text = $"SNAP IN {remaining:F1}s!";
                    warningLabel.color = WarningColor;
                }
            }

            if (warningFlash != null)
            {
                warningFlash.alpha = danger ? Mathf.PingPong(Time.time * 4f, 1f) : 0f;
            }
        }
        else
        {
            if (warningLabel != null) warningLabel.gameObject.SetActive(false);
            if (warningFlash != null) warningFlash.alpha = 0f;
        }
    }
}
