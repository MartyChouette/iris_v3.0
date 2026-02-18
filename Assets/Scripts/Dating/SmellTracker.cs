/// <summary>
/// Static utility that sums smell values across all ReactableTags.
/// Smell travels through drawers — private items are included.
/// </summary>
public static class SmellTracker
{
    /// <summary>Threshold above which perfume judgment gets downgraded.</summary>
    public static float SmellThreshold = 1.0f;

    /// <summary>Total smell from all registered ReactableTags (private included).</summary>
    public static float TotalSmell
    {
        get
        {
            float total = 0f;
            foreach (var tag in ReactableTag.All)
                total += tag.SmellAmount;
            return total;
        }
    }
}
