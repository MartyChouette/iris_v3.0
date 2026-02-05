using System.Collections.Generic;
using UnityEngine;

public class CutPathEvaluator : MonoBehaviour
{
    [Header("Sampling")]
    [Tooltip("NxN sample grid within each phone number rect.")]
    [SerializeField] private int gridResolution = 20;

    [Tooltip("Minimum overlap fraction to count as 'selected'.")]
    [SerializeField] private float minimumCoverage = 0.15f;

    /// <summary>
    /// Given a closed cut polygon (UV coords), determine which personal ad's
    /// phone number has the most coverage. Returns null if none qualifies.
    /// </summary>
    public DatePersonalDefinition Evaluate(List<Vector2> cutPolygonUV, NewspaperAdSlot[] slots)
    {
        if (cutPolygonUV == null || cutPolygonUV.Count < 3) return null;
        if (slots == null || slots.Length == 0) return null;

        DatePersonalDefinition bestDef = null;
        float bestCoverage = 0f;

        for (int s = 0; s < slots.Length; s++)
        {
            var slot = slots[s];
            if (slot == null || !slot.IsPersonalAd) continue;

            float coverage = slot.GetPhoneNumberCoverage(cutPolygonUV);

            if (coverage < minimumCoverage) continue;

            if (coverage > bestCoverage)
            {
                bestCoverage = coverage;
                bestDef = slot.PersonalDef;
            }
        }

        if (bestDef != null)
            Debug.Log($"[CutPathEvaluator] Winner: {bestDef.characterName} ({bestCoverage:P0} coverage)");

        return bestDef;
    }

    /// <summary>
    /// Point-in-polygon test using ray casting (odd-even rule).
    /// </summary>
    public static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int n = polygon.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y)
                          / (polygon[j].y - polygon[i].y) + polygon[i].x)
                inside = !inside;
        }
        return inside;
    }
}
