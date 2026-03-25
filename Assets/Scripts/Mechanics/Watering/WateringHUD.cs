using TMPro;
using UnityEngine;

/// <summary>
/// Simple overlay HUD for the ambient watering system.
/// Shows plant name and soil moisture feedback. Hidden when idle.
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

        // Show canvas on first interaction — force scale to 1 (may be saved at 0)
        if (showHUD && hudCanvas != null)
        {
            if (!hudCanvas.gameObject.activeSelf)
                hudCanvas.gameObject.SetActive(true);
            if (hudCanvas.transform.localScale.x < 0.01f)
                hudCanvas.transform.localScale = Vector3.one;
        }

        if (hudPanel != null)
            hudPanel.SetActive(showHUD);

        if (pourBar != null)
            pourBar.SetVisible(showHUD);

        if (!showHUD) return;

        var plant = manager.CurrentPlant;
        var pot = manager.Pot;

        if (plantNameLabel != null)
            plantNameLabel.text = plant != null ? plant.plantName : "";

        switch (manager.CurrentState)
        {
            case WateringManager.State.Pouring:
            case WateringManager.State.Absorbing:
                UpdatePouring(plant, pot);
                break;
            case WateringManager.State.Scoring:
                UpdateScoring();
                break;
        }
    }

    private void UpdatePouring(PlantDefinition plant, PotController pot)
    {
        if (pourBar != null && pot != null && plant != null)
        {
            // Show soil moisture as the fill level, pooled water on top,
            // oscillating target as the moving goal line
            pourBar.SetLevels(pot.SoilMoisture, pot.SoilMoisture + pot.PooledWater,
                manager.OscillatingTarget, plant.moistureTolerance);
            pourBar.SetOverflowing(pot.Overflowed);
        }
    }

    private void UpdateScoring()
    {
        if (pourBar != null)
        {
            pourBar.SetOverflowing(false);
            pourBar.ShowScore($"Score: {manager.lastScore}");
        }
    }
}
