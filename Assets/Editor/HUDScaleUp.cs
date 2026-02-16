using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// One-click editor tool to scale up Watering and Drink HUD elements 4x
/// in the current scene. Finds components on inactive GameObjects too.
/// Run once, then delete this script.
/// </summary>
public static class HUDScaleUp
{
    private const float Scale = 4f;

    [MenuItem("Window/Iris/Scale Up Pour HUDs (4x)")]
    public static void ScaleUpPourHUDs()
    {
        int changed = 0;

        // ── Watering HUD — reached via the active WateringHUD component ──
        var wateringHUDs = Object.FindObjectsByType<WateringHUD>(FindObjectsSortMode.None);
        foreach (var hud in wateringHUDs)
        {
            if (hud.plantNameLabel != null)
            {
                ScaleFont(hud.plantNameLabel, ref changed);
            }

            if (hud.pourBar != null)
            {
                ScalePourBar(hud.pourBar, ref changed);
            }
        }

        // ── Drink HUD — reached via the active SimpleDrinkHUD component ──
        var drinkHUDs = Object.FindObjectsByType<SimpleDrinkHUD>(FindObjectsSortMode.None);
        foreach (var hud in drinkHUDs)
        {
            if (hud.drinkNameLabel != null)
                ScaleFont(hud.drinkNameLabel, ref changed);

            if (hud.scoreLabel != null)
                ScaleFont(hud.scoreLabel, ref changed);

            if (hud.pourBar != null)
                ScalePourBar(hud.pourBar, ref changed);

            // Scale recipe panel contents
            if (hud.recipePanel != null)
            {
                // Scale panel itself
                ScaleRect(hud.recipePanel.GetComponent<RectTransform>(), ref changed);

                // Scale all child labels
                var labels = hud.recipePanel.GetComponentsInChildren<TMP_Text>(true);
                foreach (var label in labels)
                    ScaleFont(label, ref changed);

                // Scale button RectTransforms (skip the panel root)
                var rects = hud.recipePanel.GetComponentsInChildren<RectTransform>(true);
                foreach (var rt in rects)
                {
                    if (rt.gameObject == hud.recipePanel) continue;
                    ScaleRect(rt, ref changed);
                }
            }

            // Scale HUD panel (fill/score area)
            if (hud.hudPanel != null)
            {
                ScaleRect(hud.hudPanel.GetComponent<RectTransform>(), ref changed);
            }
        }

        Debug.Log($"[HUDScaleUp] Scaled {changed} elements {Scale}x. Ctrl+Z to undo.");
    }

    private static void ScalePourBar(PourBarUI bar, ref int changed)
    {
        Undo.RecordObject(bar, "Scale PourBarUI");
        bar.barHeight *= Scale;
        bar.barWidth *= Scale;
        EditorUtility.SetDirty(bar);
        changed++;

        var rt = bar.GetComponent<RectTransform>();
        if (rt != null)
            ScaleRect(rt, ref changed);
    }

    private static void ScaleFont(TMP_Text label, ref int changed)
    {
        Undo.RecordObject(label, "Scale font");
        label.fontSize *= Scale;
        EditorUtility.SetDirty(label);
        changed++;
    }

    private static void ScaleRect(RectTransform rt, ref int changed)
    {
        if (rt == null) return;
        Undo.RecordObject(rt, "Scale RectTransform");
        rt.sizeDelta = new Vector2(rt.sizeDelta.x * Scale, rt.sizeDelta.y * Scale);
        rt.anchoredPosition *= Scale;
        EditorUtility.SetDirty(rt);
        changed++;
    }
}
