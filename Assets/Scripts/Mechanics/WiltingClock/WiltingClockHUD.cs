using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for the <see cref="WiltingClock"/> mechanic.
/// Shows countdown to next wilt tick and healthy/wilted part counts.
/// </summary>
[DisallowMultipleComponent]
public class WiltingClockHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The WiltingClock driving the timer.")]
    public WiltingClock clock;

    [Tooltip("The FlowerGameBrain for part counts.")]
    public FlowerGameBrain brain;

    [Header("UI Elements")]
    [Tooltip("Shows countdown to the next wilt tick.")]
    public TMP_Text timerLabel;

    [Tooltip("Shows healthy vs total part status.")]
    public TMP_Text statusLabel;

    [Tooltip("Root canvas group for visibility control.")]
    public CanvasGroup root;

    // Color thresholds
    private static readonly Color HealthyColor = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color WarningColor = new Color(0.9f, 0.9f, 0.3f);
    private static readonly Color DangerColor = new Color(0.9f, 0.3f, 0.25f);

    void Update()
    {
        if (clock == null || brain == null) return;

        // Timer
        if (timerLabel != null)
        {
            float t = clock.TimeUntilNextTick;
            timerLabel.SetText("Next wilt: {0:1}s", t);
        }

        // Status
        if (statusLabel != null)
        {
            int total = 0;
            int healthy = 0;
            for (int i = 0; i < brain.parts.Count; i++)
            {
                var part = brain.parts[i];
                if (part == null || part.kind == FlowerPartKind.Crown) continue;
                if (!part.isAttached) continue;
                total++;
                if (part.condition == FlowerPartCondition.Normal)
                    healthy++;
            }

            statusLabel.SetText("Healthy: {0}/{1} parts", healthy, total);

            // Color shift
            float ratio = total > 0 ? (float)healthy / total : 1f;
            if (ratio > 0.6f)
                statusLabel.color = HealthyColor;
            else if (ratio > 0.3f)
                statusLabel.color = WarningColor;
            else
                statusLabel.color = DangerColor;
        }
    }
}
