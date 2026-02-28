using UnityEngine;

/// <summary>
/// Toggleable rim-light overlay material on the object's renderers.
/// Supports compound objects (multiple child renderers) as well as single-renderer objects.
/// Three independent layers:
///   1. Display (warm peach, "public item" indicator — always-on background glow)
///   2. Gaze (amber, NPC focus)
///   3. Hover (warm ivory, player mouse — strongest, on top)
/// All can be active simultaneously — additive blend means stronger highlights overpower subtle ones.
/// Attach to any clickable object (books, records, placeables, pots, etc.).
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    private static Material s_sharedRimMat;
    private static Material s_sharedGazeMat;
    private static Material s_sharedDisplayMat;
    private static Material s_sharedPrepLikedMat;
    private static Material s_sharedPrepDislikedMat;

    private Renderer[] _renderers;
    private Material[][] _baseMaterialArrays;
    private bool _highlighted;
    private bool _gazeActive;
    private bool _displayActive;
    private bool _prepLikedActive;
    private bool _prepDislikedActive;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers.Length == 0) return;

        EnsureSharedMaterials();

        // Cache base materials per renderer (no overlays)
        _baseMaterialArrays = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _baseMaterialArrays[i] = _renderers[i].sharedMaterials;
    }

    /// <summary>
    /// Toggle hover rim light (warm ivory) on or off.
    /// </summary>
    public void SetHighlighted(bool on)
    {
        if (_renderers == null || _renderers.Length == 0 || on == _highlighted) return;
        _highlighted = on;
        RebuildMaterials();
    }

    /// <summary>
    /// Toggle gaze rim light (amber, NPC focus) on or off.
    /// </summary>
    public void SetGazeHighlighted(bool on)
    {
        if (_renderers == null || _renderers.Length == 0 || on == _gazeActive) return;
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
        if (_renderers == null || _renderers.Length == 0 || on == _displayActive) return;
        _displayActive = on;
        RebuildMaterials();
    }

    /// <summary>
    /// Toggle prep-liked rim light (green, date likes this item) on or off.
    /// </summary>
    public void SetPrepLikedHighlighted(bool on)
    {
        if (_renderers == null || _renderers.Length == 0 || on == _prepLikedActive) return;
        _prepLikedActive = on;
        RebuildMaterials();
    }

    /// <summary>
    /// Toggle prep-disliked rim light (red, date dislikes this item) on or off.
    /// </summary>
    public void SetPrepDislikedHighlighted(bool on)
    {
        if (_renderers == null || _renderers.Length == 0 || on == _prepDislikedActive) return;
        _prepDislikedActive = on;
        RebuildMaterials();
    }

    private void RebuildMaterials()
    {
        int extraCount = (_displayActive ? 1 : 0)
                       + (_prepLikedActive ? 1 : 0)
                       + (_prepDislikedActive ? 1 : 0)
                       + (_gazeActive ? 1 : 0)
                       + (_highlighted ? 1 : 0);

        for (int r = 0; r < _renderers.Length; r++)
        {
            if (_renderers[r] == null) continue;
            var baseMats = _baseMaterialArrays[r];

            if (extraCount == 0)
            {
                _renderers[r].sharedMaterials = baseMats;
                continue;
            }

            var mats = new Material[baseMats.Length + extraCount];
            for (int i = 0; i < baseMats.Length; i++)
                mats[i] = baseMats[i];

            int slot = baseMats.Length;

            // Display renders first (background — subtlest)
            if (_displayActive && s_sharedDisplayMat != null)
                mats[slot++] = s_sharedDisplayMat;

            // Prep highlights (liked=green, disliked=red)
            if (_prepLikedActive && s_sharedPrepLikedMat != null)
                mats[slot++] = s_sharedPrepLikedMat;
            if (_prepDislikedActive && s_sharedPrepDislikedMat != null)
                mats[slot++] = s_sharedPrepDislikedMat;

            // Gaze renders second (middle)
            if (_gazeActive && s_sharedGazeMat != null)
                mats[slot++] = s_sharedGazeMat;

            // Hover renders on top (player intent dominates)
            if (_highlighted && s_sharedRimMat != null)
                mats[slot++] = s_sharedRimMat;

            _renderers[r].sharedMaterials = mats;
        }
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

        if (s_sharedPrepLikedMat == null)
        {
            var shader = Shader.Find("Iris/RimLight");
            if (shader != null)
            {
                s_sharedPrepLikedMat = new Material(shader);
                s_sharedPrepLikedMat.SetColor("_RimColor", new Color(0.3f, 0.9f, 0.4f, 0.6f));
                s_sharedPrepLikedMat.SetFloat("_RimPower", 2.5f);
                s_sharedPrepLikedMat.SetFloat("_RimIntensity", 0.7f);
            }
        }

        if (s_sharedPrepDislikedMat == null)
        {
            var shader = Shader.Find("Iris/RimLight");
            if (shader != null)
            {
                s_sharedPrepDislikedMat = new Material(shader);
                s_sharedPrepDislikedMat.SetColor("_RimColor", new Color(0.95f, 0.3f, 0.3f, 0.6f));
                s_sharedPrepDislikedMat.SetFloat("_RimPower", 2.5f);
                s_sharedPrepDislikedMat.SetFloat("_RimIntensity", 0.7f);
            }
        }
    }
}
