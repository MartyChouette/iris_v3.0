using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD overlay for the mirror makeup prototype. Shows tool name,
/// pimple coverage count, and context-sensitive instructions.
/// </summary>
[DisallowMultipleComponent]
public class MirrorMakeupHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager reference.")]
    public MirrorMakeupManager manager;

    [Header("Labels")]
    [Tooltip("Current tool name display.")]
    public TMP_Text toolNameLabel;

    [Tooltip("Context instruction text.")]
    public TMP_Text instructionLabel;

    [Tooltip("Pimple coverage counter.")]
    public TMP_Text pimpleCountLabel;

    [Header("Tool Buttons")]
    [Tooltip("Parent panel containing tool selection buttons.")]
    public GameObject toolButtonPanel;

    [Tooltip("Array of tool buttons matching the tools array order.")]
    public Button[] toolButtons;

    [Tooltip("The inspect/deselect button.")]
    public Button inspectButton;

    // Button highlight colours
    private static readonly Color _normalColor = new Color(0.25f, 0.25f, 0.35f);
    private static readonly Color _selectedColor = new Color(0.45f, 0.65f, 0.45f);

    void Update()
    {
        if (manager == null) return;

        UpdateToolName();
        UpdateInstruction();
        UpdatePimpleCount();
        UpdateButtonHighlights();
    }

    private void UpdateToolName()
    {
        if (toolNameLabel == null) return;

        var tool = manager.ActiveTool;
        toolNameLabel.text = tool != null ? tool.toolName : "Inspect Mode";
    }

    private void UpdateInstruction()
    {
        if (instructionLabel == null) return;

        var tool = manager.ActiveTool;
        if (tool == null)
        {
            instructionLabel.text = "Move mouse to look around — find all your pimples!";
            return;
        }

        switch (tool.toolType)
        {
            case MakeupToolDefinition.ToolType.Foundation:
                instructionLabel.text = "Click+drag to apply foundation";
                break;
            case MakeupToolDefinition.ToolType.Lipstick:
                instructionLabel.text = "Careful! Drag slowly or it'll smear!";
                break;
            case MakeupToolDefinition.ToolType.Eyeliner:
                instructionLabel.text = "Draw along your eye line";
                break;
            case MakeupToolDefinition.ToolType.StarSticker:
                instructionLabel.text = manager.HoldingSticker
                    ? "Click on a pimple to place the star!"
                    : "Click the sticker pad to peel off a star";
                break;
        }
    }

    private void UpdatePimpleCount()
    {
        if (pimpleCountLabel == null || manager.Canvas == null) return;

        pimpleCountLabel.SetText("Pimples: {0}/{1} covered",
            manager.Canvas.CoveredPimpleCount,
            manager.Canvas.TotalPimpleCount);
    }

    private void UpdateButtonHighlights()
    {
        if (toolButtons == null) return;

        for (int i = 0; i < toolButtons.Length; i++)
        {
            if (toolButtons[i] == null) continue;
            var img = toolButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == manager.SelectedToolIndex) ? _selectedColor : _normalColor;
        }

        if (inspectButton != null)
        {
            var inspImg = inspectButton.GetComponent<Image>();
            if (inspImg != null)
                inspImg.color = (manager.SelectedToolIndex < 0) ? _selectedColor : _normalColor;
        }
    }
}
