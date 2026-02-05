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

    private bool _interactionDone;

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
            dayManager.OnNewNewspaper.AddListener(OnNewNewspaper);
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
        _interactionDone = false;
        UpdateDayLabel();
    }

    private void OnNewNewspaper()
    {
        _interactionDone = false;
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
            case NewspaperManager.State.TableView:
                instructionLabel.SetText("Click the newspaper to pick it up");
                break;
            case NewspaperManager.State.PickingUp:
                instructionLabel.SetText("");
                break;
            case NewspaperManager.State.ReadingPaper:
                instructionLabel.SetText("Draw around an ad to cut it out | Escape to put down");
                break;
            case NewspaperManager.State.Cutting:
                instructionLabel.SetText("Cutting...");
                break;
            case NewspaperManager.State.Calling:
                instructionLabel.SetText("Dialing...");
                break;
            case NewspaperManager.State.Waiting:
                instructionLabel.SetText("Waiting for your date...");
                break;
            case NewspaperManager.State.DateArrived:
                instructionLabel.SetText("Your date is here!");
                _interactionDone = true;
                break;
        }
    }

    private void UpdateAdvanceDayButton()
    {
        if (advanceDayButton == null) return;

        // Show button only when in TableView and interaction is done (or DateArrived)
        bool show = manager.CurrentState == NewspaperManager.State.TableView && _interactionDone;

        // Also show if DateArrived (player can advance from arrived state)
        if (manager.CurrentState == NewspaperManager.State.DateArrived)
            show = true;

        advanceDayButton.SetActive(show);
    }
}
