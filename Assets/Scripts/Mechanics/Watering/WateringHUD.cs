using TMPro;
using UnityEngine;

/// <summary>
/// Simple overlay HUD for the ambient watering system.
/// Shows plant name, water/foam levels, fill target, overflow warning, and score.
/// Hidden when idle.
/// </summary>
[DisallowMultipleComponent]
public class WateringHUD : MonoBehaviour
{
    [Header("References")]
    public WateringManager manager;

    [Header("Labels")]
    public TMP_Text plantNameLabel;
    public TMP_Text waterLevelLabel;
    public TMP_Text foamLevelLabel;
    public TMP_Text targetLabel;
    public TMP_Text scoreLabel;

    [Header("Panels")]
    [Tooltip("Overflow warning (red, shown when foam > 85%).")]
    public GameObject overflowWarning;

    [Tooltip("Root panel — hidden when Idle.")]
    public GameObject hudPanel;

    void Update()
    {
        if (manager == null) return;

        bool showHUD = manager.CurrentState != WateringManager.State.Idle;
        if (hudPanel != null)
            hudPanel.SetActive(showHUD);

        if (!showHUD) return;

        switch (manager.CurrentState)
        {
            case WateringManager.State.Pouring:
                UpdatePouring();
                break;
            case WateringManager.State.Scoring:
                UpdateScoring();
                break;
        }
    }

    private void UpdatePouring()
    {
        var plant = manager.CurrentPlant;
        var pot = manager.Pot;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "";

        if (pot != null)
        {
            if (waterLevelLabel != null)
                waterLevelLabel.SetText("Water: {0}%", (int)(pot.WaterLevel * 100f));

            if (foamLevelLabel != null)
                foamLevelLabel.SetText("Foam: {0}%", (int)(pot.FoamLevel * 100f));

            if (targetLabel != null && plant != null)
                targetLabel.SetText("Target: {0}%", (int)(plant.idealWaterLevel * 100f));

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

        if (waterLevelLabel != null) waterLevelLabel.text = "";
        if (foamLevelLabel != null) foamLevelLabel.text = "";
        if (targetLabel != null) targetLabel.text = "";

        if (overflowWarning != null)
            overflowWarning.SetActive(false);

        if (scoreLabel != null)
        {
            scoreLabel.text = $"Score: {manager.lastScore}\nFill: {(int)manager.lastFillScore}  Bonus: {(int)manager.lastBonusScore}  Overflow: {(int)manager.lastOverflowScore}";
        }
    }
}
