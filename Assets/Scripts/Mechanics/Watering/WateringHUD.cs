using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD overlay for the watering prototype. Shows plant info,
/// water/foam meters, overflow warnings, and score breakdown per state.
/// </summary>
[DisallowMultipleComponent]
public class WateringHUD : MonoBehaviour
{
    [Header("References")]
    public WateringManager manager;

    [Header("Labels")]
    public TMP_Text plantNameLabel;
    public TMP_Text descriptionLabel;
    public TMP_Text instructionLabel;
    public TMP_Text waterLevelLabel;
    public TMP_Text foamLevelLabel;
    public TMP_Text scoreLabel;

    [Header("Panels")]
    [Tooltip("Shown during Browsing state.")]
    public GameObject browsePanel;

    [Tooltip("Shown during Watering state (Done Watering button).")]
    public GameObject wateringPanel;

    [Tooltip("Shown during Scoring state (Retry + Next Plant buttons).")]
    public GameObject scoringPanel;

    [Tooltip("Overflow warning flash (red, shown when foam is high).")]
    public GameObject overflowWarning;

    [Header("Buttons")]
    public Button doneButton;
    public Button retryButton;
    public Button nextPlantButton;

    void Update()
    {
        if (manager == null) return;

        UpdatePanelVisibility();

        switch (manager.CurrentState)
        {
            case WateringManager.State.Browsing:
                UpdateBrowsing();
                break;
            case WateringManager.State.Watering:
                UpdateWatering();
                break;
            case WateringManager.State.Scoring:
                UpdateScoring();
                break;
        }
    }

    private void UpdatePanelVisibility()
    {
        var state = manager.CurrentState;
        if (browsePanel != null) browsePanel.SetActive(state == WateringManager.State.Browsing);
        if (wateringPanel != null) wateringPanel.SetActive(state == WateringManager.State.Watering);
        if (scoringPanel != null) scoringPanel.SetActive(state == WateringManager.State.Scoring);
    }

    private void UpdateBrowsing()
    {
        var plant = manager.CurrentPlant;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "Select a plant";

        if (descriptionLabel != null)
            descriptionLabel.text = plant != null ? plant.description : "";

        if (instructionLabel != null)
            instructionLabel.text = "A/D: Browse plants   Enter: Select";

        ClearMeters();

        if (overflowWarning != null)
            overflowWarning.SetActive(false);
    }

    private void UpdateWatering()
    {
        var plant = manager.CurrentPlant;
        var pot = manager.Pot;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "";

        if (descriptionLabel != null)
            descriptionLabel.text = "";

        if (pot != null)
        {
            if (waterLevelLabel != null)
                waterLevelLabel.SetText("Water: {0}%", (int)(pot.WaterLevel * 100f));

            if (foamLevelLabel != null)
                foamLevelLabel.SetText("Foam: {0}%", (int)(pot.FoamLevel * 100f));

            // Instruction changes near overflow
            if (instructionLabel != null)
            {
                if (pot.FoamLevel > 0.85f)
                    instructionLabel.text = "Careful! The dirt is foaming up!";
                else
                    instructionLabel.text = "Click+hold to pour water \u2014 watch the fill line!";
            }

            // Overflow warning
            if (overflowWarning != null)
                overflowWarning.SetActive(pot.FoamLevel > 0.85f);
        }

        if (scoreLabel != null)
            scoreLabel.text = "";
    }

    private void UpdateScoring()
    {
        var plant = manager.CurrentPlant;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "";

        if (descriptionLabel != null)
            descriptionLabel.text = "";

        if (instructionLabel != null)
            instructionLabel.text = "";

        if (overflowWarning != null)
            overflowWarning.SetActive(false);

        if (scoreLabel != null)
        {
            scoreLabel.SetText(
                "Score: {0}\nFill: {1}  Bonus: {2}  Overflow: {3}",
                manager.lastScore,
                (int)manager.lastFillScore,
                (int)manager.lastBonusScore,
                (int)manager.lastOverflowScore);
        }
    }

    private void ClearMeters()
    {
        if (waterLevelLabel != null) waterLevelLabel.text = "";
        if (foamLevelLabel != null) foamLevelLabel.text = "";
        if (scoreLabel != null) scoreLabel.text = "";
    }
}
