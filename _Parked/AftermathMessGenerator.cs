using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility that generates targeted stain records based on what happened during a date.
/// Consumed by ApartmentStainSpawner the next morning.
/// Day 1 is always random (tutorial mess).
/// </summary>
public static class AftermathMessGenerator
{
    /// <summary>
    /// Generate aftermath stain records based on a completed date.
    /// Returns an empty list on Day 1 (random stains used instead).
    /// </summary>
    public static List<AftermathStainRecord> GenerateStains(RichDateHistoryEntry entry)
    {
        var stains = new List<AftermathStainRecord>();
        if (entry == null) return stains;

        // Day 1 uses random tutorial stains — no aftermath
        if (entry.day <= 1) return stains;

        int slotIndex = 0;

        // Drink served → wine ring on coffee table
        if (!string.IsNullOrEmpty(entry.drinkServedId))
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "wine_glass",
                slotIndex = slotIndex++,
                cause = "drink_served"
            });
        }

        // Bad pour (score < 40) → liquid spill in kitchen
        if (entry.drinkScore > 0 && entry.drinkScore < 40)
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "drink_spill",
                slotIndex = slotIndex++,
                cause = "bad_pour"
            });
        }

        // Perfume disliked → residue at entrance
        if (entry.moodReaction == "Dislike" && !string.IsNullOrEmpty(entry.activePerfumeId))
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "perfume_residue",
                slotIndex = slotIndex++,
                cause = "perfume_disliked"
            });
        }

        // Good date (affection > 75) → crumbs on couch
        if (entry.finalAffection > 75f)
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "snack_crumbs",
                slotIndex = slotIndex++,
                cause = "good_date"
            });
        }

        // Bad date (affection < 30) → knocked items
        if (entry.finalAffection < 30f)
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "knocked_item",
                slotIndex = slotIndex++,
                cause = "bad_date"
            });
        }

        // 3+ items investigated → footprints
        if (entry.reactableTagsInvestigated != null && entry.reactableTagsInvestigated.Count >= 3)
        {
            stains.Add(new AftermathStainRecord
            {
                spillDefinitionId = "footprints",
                slotIndex = slotIndex++,
                cause = "explored_apartment"
            });
        }

        Debug.Log($"[AftermathMessGenerator] Generated {stains.Count} stains for day {entry.day} ({entry.characterName}).");
        return stains;
    }
}
