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

    private void Update()
    {
        bool showDate = DateSessionManager.Instance != null &&
                        DateSessionManager.Instance.CurrentState != DateSessionManager.SessionState.Idle;

        if (hudRoot != null)
            hudRoot.SetActive(showDate);

        if (!showDate) return;

        // Date info
        if (dateNameText != null && DateSessionManager.Instance.CurrentDate != null)
            dateNameText.text = DateSessionManager.Instance.CurrentDate.characterName;

        // Affection
        float aff = DateSessionManager.Instance.Affection;
        if (affectionText != null)
            affectionText.text = $"{aff:F0}%";

        if (affectionBar != null)
        {
            affectionBar.minValue = 0f;
            affectionBar.maxValue = 100f;
            affectionBar.value = aff;
        }

        // Clock
        if (clockText != null && GameClock.Instance != null)
        {
            float hour = Mathf.Repeat(GameClock.Instance.CurrentHour, 24f);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            clockText.text = $"{h:D2}:{m:D2}";
        }

        // Day
        if (dayText != null && GameClock.Instance != null)
            dayText.text = $"Day {GameClock.Instance.CurrentDay}";
    }
}
