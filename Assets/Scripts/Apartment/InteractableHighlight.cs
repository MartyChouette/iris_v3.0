using UnityEngine;

/// <summary>
/// Toggleable rim-light overlay material on the object's renderer.
/// Supports three independent layers:
///   1. Display (warm peach, "public item" indicator — always-on background glow)
///   2. Gaze (amber, NPC focus)
///   3. Hover (warm ivory, player mouse — strongest, on top)
/// All can be active simultaneously — additive blend means stronger highlights overpower subtle ones.
/// Attach to any clickable object (books, records, placeables, etc.).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class InteractableHighlight : MonoBehaviour
{
    private static Material s_sharedRimMat;
    private static Material s_sharedGazeMat;
    private static Material s_sharedDisplayMat;

    private Renderer _renderer;
    private Material[] _baseMaterials;
    private bool _highlighted;
    private bool _gazeActive;
    private bool _displayActive;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;

        EnsureSharedMaterials();

        // Cache base materials (no overlays)
        _baseMaterials = _renderer.sharedMaterials;

        // Start with all highlights OFF
        _renderer.sharedMaterials = _baseMaterials;
    }

    /// <summary>
    /// Toggle hover rim light (warm ivory) on or off.
    /// </summary>
    public void SetHighlighted(bool on)
    {
        if (_renderer == null || _baseMaterials == null || on == _highlighted) return;
        _highlighted = on;
        RebuildMaterials();
    }

    /// <summary>
    /// Toggle gaze rim light (amber, NPC focus) on or off.
    /// </summary>
    public void SetGazeHighlighted(bool on)
    {
        if (_renderer == null || _baseMaterials == null || on == _gazeActive) return;
        _gazeActive = on;
        RebuildMaterials();
    }

    /// <summary>
    /// Toggle display rim light (warm peach, public item indicator) on or off.
    /// Items with ReactableTag.IsActive and !IsPrivate use this to show they're
    /// visible to date NPCs.
    /// </summary>
    public void SetDisplayHighlighted(bool on)
    {
        if (_renderer == null || _baseMaterials == null || on == _displayActive) return;
        _displayActive = on;
        RebuildMaterials();
    }

    private void RebuildMaterials()
    {
        int extraCount = (_displayActive ? 1 : 0)
                       + (_gazeActive ? 1 : 0)
                       + (_highlighted ? 1 : 0);

        if (extraCount == 0)
        {
            _renderer.sharedMaterials = _baseMaterials;
            return;
        }

        var mats = new Material[_baseMaterials.Length + extraCount];
        for (int i = 0; i < _baseMaterials.Length; i++)
            mats[i] = _baseMaterials[i];

        int slot = _baseMaterials.Length;

        // Display renders first (background — subtlest)
        if (_displayActive && s_sharedDisplayMat != null)
            mats[slot++] = s_sharedDisplayMat;

        // Gaze renders second (middle)
        if (_gazeActive && s_sharedGazeMat != null)
            mats[slot++] = s_sharedGazeMat;

        // Hover renders on top (player intent dominates)
        if (_highlighted && s_sharedRimMat != null)
            mats[slot++] = s_sharedRimMat;

        _renderer.sharedMaterials = mats;
    }

    private static void EnsureSharedMaterials()
    {
        if (s_sharedRimMat == null)
        {
            var shader = Shader.Find("Iris/RimLight");
            if (shader == null)
            {
                Debug.LogWarning("[InteractableHighlight] Iris/RimLight shader not found.");
            }
            else
            {
                s_sharedRimMat = new Material(shader);
                s_sharedRimMat.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.5f));
                s_sharedRimMat.SetFloat("_RimPower", 3.0f);
                s_sharedRimMat.SetFloat("_RimIntensity", 0.85f);
            }
        }

        if (s_sharedGazeMat == null)
        {
            var shader = Shader.Find("Iris/RimLight");
            if (shader == null)
            {
                Debug.LogWarning("[InteractableHighlight] Iris/RimLight shader not found for gaze material.");
            }
            else
            {
                s_sharedGazeMat = new Material(shader);
                s_sharedGazeMat.SetColor("_RimColor", new Color(1f, 0.75f, 0.2f, 0.5f));
                s_sharedGazeMat.SetFloat("_RimPower", 3.0f);
                s_sharedGazeMat.SetFloat("_RimIntensity", 0.8f);
            }
        }

        if (s_sharedDisplayMat == null)
        {
            var shader = Shader.Find("Iris/RimLight");
            if (shader == null)
            {
                Debug.LogWarning("[InteractableHighlight] Iris/RimLight shader not found for display material.");
            }
            else
            {
                s_sharedDisplayMat = new Material(shader);
                s_sharedDisplayMat.SetColor("_RimColor", new Color(1f, 0.85f, 0.65f, 0.35f));
                s_sharedDisplayMat.SetFloat("_RimPower", 3.8f);
                s_sharedDisplayMat.SetFloat("_RimIntensity", 0.35f);
            }
        }
    }
}
