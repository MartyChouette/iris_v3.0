using TMPro;
using UnityEngine;

/// <summary>
/// Simple overlay HUD for the perfect-pour drink system.
/// Shows recipe selection panel, drink name, fill/foam/target levels, overflow warning, and score.
/// </summary>
[DisallowMultipleComponent]
public class SimpleDrinkHUD : MonoBehaviour
{
    [Header("References")]
    public SimpleDrinkManager manager;

    [Header("Labels")]
    public TMP_Text drinkNameLabel;
    public TMP_Text fillLevelLabel;
    public TMP_Text foamLevelLabel;
    public TMP_Text targetLabel;
    public TMP_Text scoreLabel;

    [Header("Panels")]
    [Tooltip("Overflow warning (red, shown when foam > 85%).")]
    public GameObject overflowWarning;

    [Tooltip("Root panel — hidden when ChoosingRecipe.")]
    public GameObject hudPanel;

    [Tooltip("Recipe selection panel — shown only in ChoosingRecipe.")]
    public GameObject recipePanel;

    void Update()
    {
        if (manager == null) return;

        switch (manager.CurrentState)
        {
            case SimpleDrinkManager.State.ChoosingRecipe:
                UpdateChoosingRecipe();
                break;
            case SimpleDrinkManager.State.Pouring:
                UpdatePouring();
                break;
            case SimpleDrinkManager.State.Scoring:
                UpdateScoring();
                break;
        }
    }

    private void UpdateChoosingRecipe()
    {
        if (recipePanel != null) recipePanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
    }

    private void UpdatePouring()
    {
        if (recipePanel != null) recipePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        var recipe = manager.ActiveRecipe;

        if (drinkNameLabel != null)
            drinkNameLabel.text = recipe != null ? recipe.drinkName : "";

        if (!manager.PourStarted)
        {
            // Waiting for player to click the glass
            if (fillLevelLabel != null) fillLevelLabel.text = "";
            if (foamLevelLabel != null) foamLevelLabel.text = "";
            if (targetLabel != null) targetLabel.text = "";
            if (overflowWarning != null) overflowWarning.SetActive(false);
            if (scoreLabel != null) scoreLabel.text = "Click the glass to pour";
            return;
        }

        if (fillLevelLabel != null)
            fillLevelLabel.SetText("Fill: {0}%", (int)(manager.FillLevel * 100f));

        if (foamLevelLabel != null)
            foamLevelLabel.SetText("Foam: {0}%", (int)(manager.FoamLevel * 100f));

        if (targetLabel != null && recipe != null)
            targetLabel.SetText("Target: {0}%", (int)(recipe.idealFillLevel * 100f));

        if (overflowWarning != null)
            overflowWarning.SetActive(manager.FoamLevel > 0.85f);

        if (scoreLabel != null)
            scoreLabel.text = "";
    }

    private void UpdateScoring()
    {
        if (recipePanel != null) recipePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        var recipe = manager.ActiveRecipe;

        if (drinkNameLabel != null)
            drinkNameLabel.text = recipe != null ? recipe.drinkName : "";

        if (fillLevelLabel != null) fillLevelLabel.text = "";
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
