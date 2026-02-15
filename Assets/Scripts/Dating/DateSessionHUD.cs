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
        var dsm = DateSessionManager.Instance;
        bool showDate = dsm != null &&
                        dsm.CurrentState != DateSessionManager.SessionState.Idle;

        if (hudRoot != null)
            hudRoot.SetActive(showDate);

        if (!showDate) return;

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
