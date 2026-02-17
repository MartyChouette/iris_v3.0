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

    private SimpleDrinkManager.State _lastState = (SimpleDrinkManager.State)(-1);
    private bool _lastPourStarted;

    void Update()
    {
        if (manager == null) return;

        var state = manager.CurrentState;
        bool stateChanged = state != _lastState;
        _lastState = state;

        switch (state)
        {
            case SimpleDrinkManager.State.ChoosingRecipe:
                if (stateChanged) ApplyChoosingRecipePanels();
                break;
            case SimpleDrinkManager.State.Pouring:
                if (stateChanged) ApplyPouringPanels();
                UpdatePouring();
                break;
            case SimpleDrinkManager.State.Scoring:
                if (stateChanged) ApplyScoringPanels();
                break;
        }
    }

    private void ApplyChoosingRecipePanels()
    {
        if (recipePanel != null) recipePanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (pourBar != null) pourBar.SetVisible(false);
    }

    private void ApplyPouringPanels()
    {
        if (recipePanel != null) recipePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);
        _lastPourStarted = false;

        var recipe = manager.ActiveRecipe;
        if (drinkNameLabel != null)
            drinkNameLabel.text = recipe != null ? recipe.drinkName : "";
        if (pourBar != null) pourBar.SetVisible(false);
        if (scoreLabel != null) scoreLabel.text = "Click the glass to pour";
    }

    private void UpdatePouring()
    {
        bool pourStarted = manager.PourStarted;
        if (pourStarted != _lastPourStarted)
        {
            _lastPourStarted = pourStarted;
            if (pourBar != null) pourBar.SetVisible(pourStarted);
            if (!pourStarted && scoreLabel != null) scoreLabel.text = "Click the glass to pour";
        }

        if (!pourStarted) return;

        var recipe = manager.ActiveRecipe;
        if (pourBar != null && recipe != null)
        {
            pourBar.SetLevels(manager.FillLevel, manager.FoamLevel, manager.OscillatingTarget, recipe.fillTolerance);
            pourBar.SetOverflowing(manager.Overflowed);
            pourBar.SetLiquidColor(recipe.liquidColor);
        }

        if (scoreLabel != null)
            scoreLabel.text = "";
    }

    private void ApplyScoringPanels()
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
            scoreLabel.text = $"Fill: {(int)manager.lastFillScore}  Bonus: {(int)manager.lastBonusScore}  Overflow: {(int)manager.lastOverflowScore}";
    }
}
