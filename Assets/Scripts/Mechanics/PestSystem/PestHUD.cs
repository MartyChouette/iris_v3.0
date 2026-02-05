using TMPro;
using UnityEngine;

/// <summary>
/// HUD overlay for the <see cref="PestController"/> mechanic.
/// Shows infestation status and warns when pests are near the crown.
/// </summary>
[DisallowMultipleComponent]
public class PestHUD : MonoBehaviour
{
    [Header("References")]
    public PestController pests;

    [Header("UI Elements")]
    public TMP_Text statusLabel;
    public TMP_Text warningLabel;
    public CanvasGroup warningFlash;

    private static readonly Color SafeColor = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color SpreadingColor = new Color(0.9f, 0.9f, 0.3f);
    private static readonly Color DangerColor = new Color(0.9f, 0.3f, 0.25f);

    void Update()
    {
        if (pests == null || pests.brain == null) return;

        int totalAttached = 0;
        for (int i = 0; i < pests.brain.parts.Count; i++)
        {
            if (pests.brain.parts[i] != null && pests.brain.parts[i].isAttached)
                totalAttached++;
        }

        int infested = pests.InfestedCount;

        if (statusLabel != null)
        {
            statusLabel.SetText("Infested: {0}/{1} parts", infested, totalAttached);

            float ratio = totalAttached > 0 ? (float)infested / totalAttached : 0f;
            if (ratio < 0.25f)
                statusLabel.color = SafeColor;
            else if (ratio < 0.5f)
                statusLabel.color = SpreadingColor;
            else
                statusLabel.color = DangerColor;
        }

        // Warn if pest is adjacent to crown
        bool nearCrown = IsPestNearCrown();
        if (warningLabel != null)
        {
            warningLabel.gameObject.SetActive(nearCrown);
            if (nearCrown)
                warningLabel.text = "PEST NEAR CROWN!";
        }

        if (warningFlash != null)
        {
            float targetAlpha = nearCrown ? Mathf.PingPong(Time.time * 3f, 1f) : 0f;
            warningFlash.alpha = targetAlpha;
        }
    }

    private bool IsPestNearCrown()
    {
        if (pests.brain == null) return false;

        FlowerPartRuntime crown = null;
        for (int i = 0; i < pests.brain.parts.Count; i++)
        {
            if (pests.brain.parts[i] != null && pests.brain.parts[i].kind == FlowerPartKind.Crown)
            {
                crown = pests.brain.parts[i];
                break;
            }
        }

        if (crown == null) return false;

        var infested = pests.InfestedParts;
        for (int i = 0; i < infested.Count; i++)
        {
            float dist = Vector3.Distance(infested[i].transform.position, crown.transform.position);
            if (dist < 0.3f) return true;
        }

        return false;
    }
}
