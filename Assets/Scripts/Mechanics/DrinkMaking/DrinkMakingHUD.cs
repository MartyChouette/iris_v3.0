using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for the drink-making prototype. Shows recipe info,
/// fill/foam meters, stir quality, and score breakdown per state.
/// </summary>
[DisallowMultipleComponent]
public class DrinkMakingHUD : MonoBehaviour
{
    [Header("References")]
    public DrinkMakingManager manager;
    public GlassController glass;
    public StirController stirrer;

    [Header("Labels")]
    public TMP_Text recipeNameLabel;
    public TMP_Text instructionLabel;
    public TMP_Text fillLevelLabel;
    public TMP_Text foamLevelLabel;
    public TMP_Text stirQualityLabel;
    public TMP_Text scoreLabel;

    [Header("Panels")]
    [Tooltip("Shown during Scoring state.")]
    public GameObject scoringPanel;

    [Tooltip("Shown during ChoosingRecipe state.")]
    public GameObject recipePanel;

    [Tooltip("Shown during Pouring state (Done Pouring button).")]
    public GameObject pouringPanel;

    [Tooltip("Shown during Stirring state (Done Stirring button).")]
    public GameObject stirringPanel;

    void Update()
    {
        if (manager == null) return;

        UpdatePanelVisibility();

        switch (manager.currentState)
        {
            case DrinkMakingManager.State.ChoosingRecipe:
                UpdateChoosingRecipe();
                break;
            case DrinkMakingManager.State.Pouring:
                UpdatePouring();
                break;
            case DrinkMakingManager.State.Stirring:
                UpdateStirring();
                break;
            case DrinkMakingManager.State.Scoring:
                UpdateScoring();
                break;
        }
    }

    private void UpdatePanelVisibility()
    {
        var state = manager.currentState;
        if (recipePanel != null) recipePanel.SetActive(state == DrinkMakingManager.State.ChoosingRecipe);
        if (pouringPanel != null) pouringPanel.SetActive(state == DrinkMakingManager.State.Pouring);
        if (stirringPanel != null) stirringPanel.SetActive(state == DrinkMakingManager.State.Stirring);
        if (scoringPanel != null) scoringPanel.SetActive(state == DrinkMakingManager.State.Scoring);
    }

    private void UpdateChoosingRecipe()
    {
        if (recipeNameLabel != null)
            recipeNameLabel.text = "Choose a Recipe";

        if (instructionLabel != null)
            instructionLabel.text = "Click a recipe to begin";

        ClearMeters();
    }

    private void UpdatePouring()
    {
        if (manager.activeRecipe != null && recipeNameLabel != null)
            recipeNameLabel.text = manager.activeRecipe.drinkName;

        if (instructionLabel != null)
            instructionLabel.text = "Click a bottle, then click+hold on glass to pour";

        if (glass != null)
        {
            if (fillLevelLabel != null)
                fillLevelLabel.SetText("Fill: {0}%", (int)(glass.LiquidLevel * 100f));

            if (foamLevelLabel != null)
                foamLevelLabel.SetText("Foam: {0}%", (int)(glass.FoamLevel * 100f));
        }

        if (stirQualityLabel != null)
            stirQualityLabel.text = "";

        if (scoreLabel != null)
            scoreLabel.text = "";
    }

    private void UpdateStirring()
    {
        if (instructionLabel != null)
            instructionLabel.text = "Hold click and move mouse in circles!";

        if (stirrer != null && stirQualityLabel != null)
        {
            string quality = stirrer.StirQuality > 0.8f ? "Great!"
                : stirrer.StirQuality > 0.5f ? "Good"
                : stirrer.StirQuality > 0.2f ? "Slow..."
                : "Move!";
            stirQualityLabel.text = $"Stir: {quality}";
        }
    }

    private void UpdateScoring()
    {
        if (recipeNameLabel != null && manager.activeRecipe != null)
            recipeNameLabel.text = manager.activeRecipe.drinkName;

        if (instructionLabel != null)
            instructionLabel.text = "";

        if (scoreLabel != null)
        {
            scoreLabel.SetText(
                "Score: {0}\nFill: {1}  Ingredients: {2}  Stir: {3}  Overflow: {4}",
                manager.lastScore,
                (int)manager.lastFillScore,
                (int)manager.lastIngredientScore,
                (int)manager.lastStirScore,
                (int)manager.lastOverflowScore);
        }
    }

    private void ClearMeters()
    {
        if (fillLevelLabel != null) fillLevelLabel.text = "";
        if (foamLevelLabel != null) foamLevelLabel.text = "";
        if (stirQualityLabel != null) stirQualityLabel.text = "";
        if (scoreLabel != null) scoreLabel.text = "";
    }
}
