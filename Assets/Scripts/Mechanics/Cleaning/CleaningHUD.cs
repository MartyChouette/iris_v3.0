using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD overlay for the kitchen cleaning prototype. Shows current tool,
/// context-sensitive instructions, per-surface progress, and completion state.
/// </summary>
[DisallowMultipleComponent]
public class CleaningHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager reference.")]
    public CleaningManager manager;

    [Header("Labels")]
    [Tooltip("Current tool name display.")]
    public TMP_Text toolNameLabel;

    [Tooltip("Context-sensitive instruction text.")]
    public TMP_Text instructionLabel;

    [Tooltip("Overall progress display.")]
    public TMP_Text progressLabel;

    [Tooltip("Per-surface breakdown display.")]
    public TMP_Text surfaceDetailLabel;

    [Header("Tool Buttons")]
    [Tooltip("Sponge selection button.")]
    public Button spongeButton;

    [Tooltip("Spray bottle selection button.")]
    public Button sprayButton;

    [Header("Completion")]
    [Tooltip("Panel shown when all surfaces are clean.")]
    public GameObject completionPanel;

    // Button highlight colours
    private static readonly Color _normalColor = new Color(0.25f, 0.25f, 0.35f);
    private static readonly Color _selectedColor = new Color(0.45f, 0.65f, 0.45f);

    void Update()
    {
        if (manager == null) return;

        UpdateToolName();
        UpdateInstruction();
        UpdateProgress();
        UpdateSurfaceDetails();
        UpdateButtonHighlights();
        UpdateCompletion();
    }

    private void UpdateToolName()
    {
        if (toolNameLabel == null) return;

        toolNameLabel.text = manager.CurrentTool == CleaningManager.Tool.Sponge
            ? "Sponge"
            : "Spray Bottle";
    }

    private void UpdateInstruction()
    {
        if (instructionLabel == null) return;

        var surface = manager.HoveredSurface;

        if (surface == null)
        {
            instructionLabel.text = "Move mouse over a dirty surface";
            return;
        }

        var def = surface.Definition;
        if (def == null)
        {
            instructionLabel.text = "";
            return;
        }

        switch (manager.CurrentTool)
        {
            case CleaningManager.Tool.Sponge:
                if (def.stubbornness > 0.5f)
                    instructionLabel.text = "This is stubborn! Try spraying first";
                else
                    instructionLabel.text = "Click+drag to wipe";
                break;

            case CleaningManager.Tool.SprayBottle:
                instructionLabel.text = "Click+hold to spray cleaning fluid";
                break;
        }
    }

    private void UpdateProgress()
    {
        if (progressLabel == null) return;

        int pct = Mathf.RoundToInt(manager.OverallCleanPercent * 100f);
        progressLabel.SetText("Overall: {0}%", pct);
    }

    private void UpdateSurfaceDetails()
    {
        if (surfaceDetailLabel == null || manager.Surfaces == null) return;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < manager.Surfaces.Length; i++)
        {
            var s = manager.Surfaces[i];
            if (s == null || s.Definition == null) continue;

            int pct = Mathf.RoundToInt(s.CleanPercent * 100f);
            string status = s.IsFullyClean ? "Done" : $"{pct}%";
            sb.AppendLine($"{s.Definition.displayName}: {status}");
        }

        surfaceDetailLabel.text = sb.ToString();
    }

    private void UpdateButtonHighlights()
    {
        bool isSponge = manager.CurrentTool == CleaningManager.Tool.Sponge;

        if (spongeButton != null)
        {
            var img = spongeButton.GetComponent<Image>();
            if (img != null)
                img.color = isSponge ? _selectedColor : _normalColor;
        }

        if (sprayButton != null)
        {
            var img = sprayButton.GetComponent<Image>();
            if (img != null)
                img.color = !isSponge ? _selectedColor : _normalColor;
        }
    }

    private void UpdateCompletion()
    {
        if (completionPanel != null)
            completionPanel.SetActive(manager.AllClean);
    }
}
