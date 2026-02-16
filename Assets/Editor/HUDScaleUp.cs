using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// One-click editor tool to scale up Watering and Drink HUD elements 4x
/// in the current scene. Run once, then delete this script.
/// </summary>
public static class HUDScaleUp
{
    [MenuItem("Window/Iris/Scale Up Pour HUDs (4x)")]
    public static void ScaleUpPourHUDs()
    {
        int changed = 0;

        // ── Scale all PourBarUI instances ──
        var pourBars = Object.FindObjectsByType<PourBarUI>(FindObjectsSortMode.None);
        foreach (var bar in pourBars)
        {
            Undo.RecordObject(bar, "Scale PourBarUI 4x");
            bar.barHeight *= 4f;
            bar.barWidth *= 4f;
            EditorUtility.SetDirty(bar);
            changed++;

            // Scale the parent RectTransform sizeDelta to match
            var rt = bar.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Scale PourBarUI RectTransform 4x");
                rt.sizeDelta = new Vector2(rt.sizeDelta.x * 4f, rt.sizeDelta.y * 4f);
                EditorUtility.SetDirty(rt);
            }
        }

        // ── Scale TMP labels in watering HUD ──
        var wateringHUDs = Object.FindObjectsByType<WateringHUD>(FindObjectsSortMode.None);
        foreach (var hud in wateringHUDs)
        {
            if (hud.plantNameLabel != null)
            {
                Undo.RecordObject(hud.plantNameLabel, "Scale plant label 4x");
                hud.plantNameLabel.fontSize *= 4f;
                EditorUtility.SetDirty(hud.plantNameLabel);
                changed++;
            }
        }

        // ── Scale TMP labels in drink HUD ──
        var drinkHUDs = Object.FindObjectsByType<SimpleDrinkHUD>(FindObjectsSortMode.None);
        foreach (var hud in drinkHUDs)
        {
            if (hud.drinkNameLabel != null)
            {
                Undo.RecordObject(hud.drinkNameLabel, "Scale drink name label 4x");
                hud.drinkNameLabel.fontSize *= 4f;
                EditorUtility.SetDirty(hud.drinkNameLabel);
                changed++;
            }
            if (hud.scoreLabel != null)
            {
                Undo.RecordObject(hud.scoreLabel, "Scale score label 4x");
                hud.scoreLabel.fontSize *= 4f;
                EditorUtility.SetDirty(hud.scoreLabel);
                changed++;
            }

            // Scale recipe panel buttons text
            if (hud.recipePanel != null)
            {
                var labels = hud.recipePanel.GetComponentsInChildren<TMP_Text>(true);
                foreach (var label in labels)
                {
                    Undo.RecordObject(label, "Scale recipe label 4x");
                    label.fontSize *= 4f;
                    EditorUtility.SetDirty(label);
                    changed++;
                }

                // Scale button sizes
                var buttons = hud.recipePanel.GetComponentsInChildren<RectTransform>(true);
                foreach (var btnRT in buttons)
                {
                    if (btnRT.gameObject == hud.recipePanel) continue; // skip panel root
                    Undo.RecordObject(btnRT, "Scale recipe button 4x");
                    btnRT.sizeDelta = new Vector2(btnRT.sizeDelta.x * 4f, btnRT.sizeDelta.y * 4f);
                    btnRT.anchoredPosition *= 4f;
                    EditorUtility.SetDirty(btnRT);
                }
            }
        }

        Debug.Log($"[HUDScaleUp] Scaled {changed} elements 4x. Ctrl+Z to undo.");
    }
}
