using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NewspaperHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private NewspaperManager manager;

    [Header("UI Elements")]
    [Tooltip("Displays 'Day N'.")]
    [SerializeField] private TMP_Text dayLabel;

    [Tooltip("Context-sensitive instruction hints.")]
    [SerializeField] private TMP_Text instructionLabel;

    [Tooltip("Button to advance to next day.")]
    [SerializeField] private GameObject advanceDayButton;

    private void Start()
    {
        // Wire advance day button
        if (advanceDayButton != null)
        {
            var btn = advanceDayButton.GetComponent<Button>();
            if (btn != null && dayManager != null)
                btn.onClick.AddListener(OnAdvanceDayClicked);
        }

        if (dayManager != null)
        {
            dayManager.OnDayChanged.AddListener(OnDayChanged);
        }

        UpdateDayLabel();
    }

    private void Update()
    {
        if (manager == null) return;

        UpdateInstructionLabel();
        UpdateAdvanceDayButton();
    }

    private void OnDayChanged(int newDay)
    {
        UpdateDayLabel();
    }

    private void OnAdvanceDayClicked()
    {
        if (dayManager != null)
            dayManager.AdvanceDay();
    }

    private void UpdateDayLabel()
    {
        if (dayLabel == null || dayManager == null) return;
        dayLabel.SetText("Day {0}", dayManager.CurrentDay);
    }

    private void UpdateInstructionLabel()
    {
        if (instructionLabel == null) return;

        switch (manager.CurrentState)
        {
            case NewspaperManager.State.ReadingPaper:
                instructionLabel.SetText("Draw around a phone number to cut it out");
                break;
            case NewspaperManager.State.Cutting:
                instructionLabel.SetText("Cutting...");
                break;
            case NewspaperManager.State.Calling:
                instructionLabel.SetText("Dialing...");
                break;
            case NewspaperManager.State.Done:
                instructionLabel.SetText("");
                break;
        }
    }

    private void UpdateAdvanceDayButton()
    {
        if (advanceDayButton == null) return;

        // Show button only in Done state (after date interaction is complete)
        advanceDayButton.SetActive(manager.CurrentState == NewspaperManager.State.Done);
    }
}
