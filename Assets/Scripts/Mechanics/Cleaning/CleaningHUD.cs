using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for cleaning. Shows context-sensitive instruction,
/// per-surface progress, and completion state.
/// </summary>
[DisallowMultipleComponent]
public class CleaningHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager reference.")]
    public CleaningManager manager;

    [Header("Labels")]
    [Tooltip("Context-sensitive instruction text.")]
    public TMP_Text instructionLabel;

    [Tooltip("Overall progress display.")]
    public TMP_Text progressLabel;

    [Tooltip("Per-surface breakdown display.")]
    public TMP_Text surfaceDetailLabel;

    [Header("Completion")]
    [Tooltip("Panel shown when all surfaces are clean.")]
    public GameObject completionPanel;

    void Update()
    {
        if (manager == null) return;

        UpdateInstruction();
        UpdateProgress();
        UpdateSurfaceDetails();
        UpdateCompletion();
    }

    private void UpdateInstruction()
    {
        if (instructionLabel == null) return;

        var surface = manager.HoveredSurface;
        if (surface == null)
        {
            instructionLabel.text = "Click stains to scrub them clean";
            return;
        }

        instructionLabel.text = "Click+drag to scrub";
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

    private void UpdateCompletion()
    {
        if (completionPanel != null)
            completionPanel.SetActive(manager.AllClean);
    }
}
