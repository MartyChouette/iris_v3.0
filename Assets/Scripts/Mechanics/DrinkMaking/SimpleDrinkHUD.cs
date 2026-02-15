using TMPro;
using UnityEngine;

/// <summary>
/// Simple overlay HUD for the perfect-pour drink system.
/// Shows recipe selection panel, drink name, visual pour bar, and score.
/// </summary>
[DisallowMultipleComponent]
public class SimpleDrinkHUD : MonoBehaviour
{
    [Header("References")]
    public SimpleDrinkManager manager;

    [Header("UI")]
    public PourBarUI pourBar;
    public TMP_Text drinkNameLabel;
    public TMP_Text scoreLabel;

    [Header("Panels")]
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
        if (pourBar != null) pourBar.SetVisible(false);
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
            if (pourBar != null) pourBar.SetVisible(false);
            if (scoreLabel != null) scoreLabel.text = "Click the glass to pour";
            return;
        }

        if (pourBar != null && recipe != null)
        {
            pourBar.SetVisible(true);
            pourBar.SetLevels(manager.FillLevel, manager.FoamLevel, recipe.idealFillLevel, recipe.fillTolerance);
            pourBar.SetOverflowing(manager.Overflowed);
            pourBar.SetLiquidColor(recipe.liquidColor);
        }

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

        if (pourBar != null)
        {
            pourBar.SetOverflowing(false);
            pourBar.ShowScore($"Score: {manager.lastScore}");
        }

        if (scoreLabel != null)
        {
            scoreLabel.text = $"Fill: {(int)manager.lastFillScore}  Bonus: {(int)manager.lastBonusScore}  Overflow: {(int)manager.lastOverflowScore}";
        }
    }
}
