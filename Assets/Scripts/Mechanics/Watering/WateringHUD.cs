using TMPro;
using UnityEngine;

/// <summary>
/// Simple overlay HUD for the ambient watering system.
/// Shows plant name and a visual pour bar. Hidden when idle.
/// </summary>
[DisallowMultipleComponent]
public class WateringHUD : MonoBehaviour
{
    [Header("References")]
    public WateringManager manager;

    [Header("UI")]
    public PourBarUI pourBar;
    public TMP_Text plantNameLabel;

    [Header("Panels")]
    [Tooltip("Root panel — hidden when Idle.")]
    public GameObject hudPanel;

    [Tooltip("HUD canvas — hidden until first interaction.")]
    public Canvas hudCanvas;

    void Update()
    {
        if (manager == null) return;

        bool showHUD = manager.CurrentState != WateringManager.State.Idle;

        // Show canvas on first interaction
        if (showHUD && hudCanvas != null && !hudCanvas.gameObject.activeSelf)
            hudCanvas.gameObject.SetActive(true);

        if (hudPanel != null)
            hudPanel.SetActive(showHUD);

        if (pourBar != null)
            pourBar.SetVisible(showHUD);

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

        if (pourBar != null && pot != null && plant != null)
        {
            pourBar.SetLevels(pot.WaterLevel, pot.FoamLevel, plant.idealWaterLevel, plant.waterTolerance);
            pourBar.SetOverflowing(pot.FoamLevel >= 1f);
        }
    }

    private void UpdateScoring()
    {
        var plant = manager.CurrentPlant;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "";

        if (pourBar != null)
        {
            pourBar.SetOverflowing(false);
            pourBar.ShowScore($"Score: {manager.lastScore}");
        }
    }
}
