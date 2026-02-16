using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minimal HUD overlay showing date info, affection bar, and game clock.
/// </summary>
public class DateSessionHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private TMP_Text dateNameText;
    [SerializeField] private TMP_Text affectionText;
    [SerializeField] private Slider affectionBar;
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private TMP_Text dayText;

    [Header("Phase Display")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text revealItemText;

    private void OnEnable()
    {
        var dsm = DateSessionManager.Instance;
        if (dsm != null) dsm.OnRevealReaction += HandleRevealReaction;
    }

    private void OnDisable()
    {
        var dsm = DateSessionManager.Instance;
        if (dsm != null) dsm.OnRevealReaction -= HandleRevealReaction;
    }

    private void HandleRevealReaction(DateSessionManager.AccumulatedReaction reaction)
    {
        if (revealItemText != null)
            revealItemText.text = $"{reaction.itemName}: {reaction.type}";
    }

    private void Update()
    {
        var dsm = DateSessionManager.Instance;
        bool showDate = dsm != null &&
                        dsm.CurrentState != DateSessionManager.SessionState.Idle;

        if (hudRoot != null)
            hudRoot.SetActive(showDate);

        if (!showDate)
        {
            if (revealItemText != null) revealItemText.text = "";
            return;
        }

        // Date info
        if (dateNameText != null && dsm.CurrentDate != null)
            dateNameText.text = dsm.CurrentDate.characterName;

        // Affection
        float aff = dsm.Affection;
        if (affectionText != null)
            affectionText.text = $"{aff:F0}%";

        if (affectionBar != null)
        {
            affectionBar.minValue = 0f;
            affectionBar.maxValue = 100f;
            affectionBar.value = aff;
        }

        // Phase label
        if (phaseText != null)
        {
            phaseText.text = dsm.CurrentDatePhase switch
            {
                DateSessionManager.DatePhase.Arrival => "Arrival",
                DateSessionManager.DatePhase.BackgroundJudging => "Date in Progress",
                DateSessionManager.DatePhase.Reveal => "The Verdict",
                _ => ""
            };
        }

        // Clear reveal text when not in Reveal phase
        if (dsm.CurrentDatePhase != DateSessionManager.DatePhase.Reveal && revealItemText != null)
            revealItemText.text = "";

        // Clock
        var clock = GameClock.Instance;
        if (clockText != null && clock != null)
        {
            float hour = Mathf.Repeat(clock.CurrentHour, 24f);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            clockText.text = $"{h:D2}:{m:D2}";
        }

        // Day
        if (dayText != null && clock != null)
            dayText.text = $"Day {clock.CurrentDay}";
    }
}
