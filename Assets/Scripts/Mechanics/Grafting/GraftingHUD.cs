using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for the <see cref="GraftingController"/> mechanic.
/// Shows instructions, donor part counts, and filled slot counts.
/// </summary>
[DisallowMultipleComponent]
public class GraftingHUD : MonoBehaviour
{
    [Header("References")]
    public GraftingController controller;

    [Header("UI Elements")]
    public TMP_Text instructionLabel;
    public TMP_Text donorCountLabel;
    public TMP_Text targetCountLabel;
    public CanvasGroup holdingIndicator;

    void Update()
    {
        if (controller == null) return;

        // Instructions
        if (instructionLabel != null)
        {
            switch (controller.currentState)
            {
                case GraftingController.State.Idle:
                    instructionLabel.text = "Click a part on the LEFT flower to pick it up";
                    break;
                case GraftingController.State.Holding:
                    instructionLabel.text = "Place on a GREEN slot on the RIGHT flower (right-click to cancel)";
                    break;
            }
        }

        // Donor count
        if (donorCountLabel != null)
        {
            donorCountLabel.SetText("Donor: {0} parts", controller.DonorPartCount);
        }

        // Target count
        if (targetCountLabel != null)
        {
            targetCountLabel.SetText("Grafted: {0}/{1} slots",
                controller.FilledSlotCount, controller.TotalSlotCount);
        }

        // Holding indicator
        if (holdingIndicator != null)
        {
            holdingIndicator.alpha = controller.currentState == GraftingController.State.Holding ? 1f : 0f;
        }
    }
}
