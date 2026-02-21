using UnityEngine;
using TMPro;

/// <summary>
/// Scene-scoped component that auto-adds AccessibleText to every TMP_Text
/// in the scene at startup. Place on a root GameObject (e.g. HUD canvas or
/// manager). Runs once in Awake — no per-frame cost.
/// </summary>
public class IrisTextThemeApplier : MonoBehaviour
{
    [Tooltip("If true, also applies theme to TMP_Text objects that are initially inactive.")]
    [SerializeField] private bool _includeInactive = true;

    private void Awake()
    {
        ApplyThemeToAll();
    }

    /// <summary>
    /// Finds all TMP_Text in the scene and ensures each has an AccessibleText component.
    /// Safe to call multiple times (skips objects that already have the component).
    /// </summary>
    public void ApplyThemeToAll()
    {
        var allText = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        if (_includeInactive)
            allText = Resources.FindObjectsOfTypeAll<TMP_Text>();

        int added = 0;
        foreach (var tmp in allText)
        {
            if (tmp == null) continue;

            // Skip prefab assets (only apply to scene instances)
            if (!tmp.gameObject.scene.IsValid()) continue;

            if (tmp.GetComponent<AccessibleText>() == null)
            {
                tmp.gameObject.AddComponent<AccessibleText>();
                added++;
            }
        }

        if (added > 0)
            Debug.Log($"[IrisTextThemeApplier] Added AccessibleText to {added} text objects.");
    }
}
