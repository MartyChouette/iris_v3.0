using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggleable highlight overlay on the object's renderers using Iris/Highlight shader.
/// Supports compound objects (multiple child renderers) as well as single-renderer objects.
/// Three independent layers:
///   1. Display (warm peach, "public item" indicator — always-on background glow)
///   2. Gaze (amber, NPC focus)
///   3. Hover (warm ivory, player mouse — strongest, on top)
/// All can be active simultaneously — additive blend means stronger highlights overpower subtle ones.
/// Attach to any clickable object (books, records, placeables, pots, plants, etc.).
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    // ── Static registry (like PlaceableObject.All) ───────────────
    private static readonly List<InteractableHighlight> s_all = new();
    public static IReadOnlyList<InteractableHighlight> All => s_all;

    [Tooltip("Drag Iris/Highlight shader here so it's included in builds.")]
    [SerializeField] private Shader _highlightShader;

    [Tooltip("Drag Iris/PSXInteractable shader here so it's included in builds.")]
    [SerializeField] private Shader _interactShader;

    private static Shader s_cachedHighlightShader;
    private static Shader s_cachedInteractShader;
    private static Material s_sharedRimMat;
    private static Material s_sharedGazeMat;
    private static Material s_sharedDisplayMat;
    private static Material s_sharedPrepLikedMat;
    private static Material s_sharedPrepDislikedMat;
    private static Material s_sharedInteractMat;

    /// <summary>Cached Iris/Highlight shader. Available for other scripts.</summary>
    public static Shader HighlightShader => s_cachedHighlightShader;

    private Renderer[] _renderers;
    private Material[][] _baseMaterialArrays;
    private bool _highlighted;
    private bool _gazeActive;
    private bool _displayActive;
    private bool _prepLikedActive;
    private bool _prepDislikedActive;

    private bool _interactActive;

    // Per-instance interact material (copies base texture for affine warp overlay)
    private Material _instanceInteractMat;

    // Per-instance glitch rim materials (only created for objects with _GlitchIntensity > 0)
    private float _glitchIntensity;
    private Material _glitchRimMat;
    private Material _glitchGazeMat;
    private Material _glitchDisplayMat;
    private Material _glitchPrepLikedMat;
    private Material _glitchPrepDislikedMat;
    private Material _glitchInteractMat;

    private static readonly int GlitchID = Shader.PropertyToID("_GlitchIntensity");
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers.Length == 0) return;

        // Cache shaders from serialized ref (survives builds) or Shader.Find fallback
        if (s_cachedHighlightShader == null)
        {
            s_cachedHighlightShader = _highlightShader;
            if (s_cachedHighlightShader == null) s_cachedHighlightShader = Shader.Find("Iris/Highlight");
        }
        if (s_cachedInteractShader == null)
        {
            s_cachedInteractShader = _interactShader;
            if (s_cachedInteractShader == null) s_cachedInteractShader = Shader.Find("Iris/PSXInteractable");
        }

        EnsureSharedMaterials();

        // Swap base materials to PSXLit so interactables always have the retro shader,
        // even if spawned after PSXRenderController's initial scene-wide swap.
        SwapToPSXLit();

        // Cache base materials per renderer (no overlays)
        _baseMaterialArrays = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _baseMaterialArrays[i] = _renderers[i].sharedMaterials;

        // Detect glitch intensity from base material
        DetectGlitch();
    }

    private void OnEnable() => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    /// <summary>
    /// Toggle hover highlight (warm ivory) on or off.
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

    /// <summary>
    /// Toggle affine-warp interactable overlay on or off.
    /// Shows pulsing PSX texture warp to indicate the object is interactable.
    /// </summary>
    public void SetInteractHighlighted(bool on)
    {
        if (_renderers == null || _renderers.Length == 0 || on == _interactActive) return;
        _interactActive = on;
        if (!on && _instanceInteractMat != null)
        {
            Destroy(_instanceInteractMat);
            _instanceInteractMat = null;
        }
        RebuildMaterials();
    }

    /// <summary>
    /// Check base materials for PSXLitGlitch and create per-instance rim materials
    /// with matching vertex jitter so highlights track the glitching mesh.
    /// </summary>
    private void DetectGlitch()
    {
        for (int r = 0; r < _renderers.Length; r++)
        {
            if (_renderers[r] == null) continue;
            foreach (var mat in _baseMaterialArrays[r])
            {
                if (mat == null) continue;
                if (!mat.HasFloat(GlitchID)) continue;
                float gi = mat.GetFloat(GlitchID);
                if (gi > 0.001f)
                {
                    _glitchIntensity = gi;
                    BuildGlitchRimMaterials();
                    return;
                }
            }
        }
    }

    private void BuildGlitchRimMaterials()
    {
        _glitchRimMat = MakeGlitchVariant(s_sharedRimMat);
        _glitchGazeMat = MakeGlitchVariant(s_sharedGazeMat);
        _glitchDisplayMat = MakeGlitchVariant(s_sharedDisplayMat);
        _glitchPrepLikedMat = MakeGlitchVariant(s_sharedPrepLikedMat);
        _glitchPrepDislikedMat = MakeGlitchVariant(s_sharedPrepDislikedMat);
        _glitchInteractMat = MakeGlitchVariant(s_sharedInteractMat);
    }

    private Material MakeGlitchVariant(Material source)
    {
        if (source == null) return null;
        var mat = new Material(source);
        mat.SetFloat(GlitchID, _glitchIntensity);
        return mat;
    }

    /// <summary>
    /// Gets a per-instance interact material with the object's base texture copied in,
    /// so the affine warp overlay shows the actual object texture.
    /// </summary>
    private Material GetInteractMat(Material[] baseMats)
    {
        if (_instanceInteractMat != null) return _instanceInteractMat;

        var source = _glitchIntensity > 0 && _glitchInteractMat != null
            ? _glitchInteractMat
            : s_sharedInteractMat;
        if (source == null) return null;

        _instanceInteractMat = new Material(source);

        // Copy texture from first base material that has one
        for (int i = 0; i < baseMats.Length; i++)
        {
            if (baseMats[i] != null && baseMats[i].HasTexture(MainTexID))
            {
                var tex = baseMats[i].GetTexture(MainTexID);
                if (tex != null)
                {
                    _instanceInteractMat.SetTexture(MainTexID, tex);
                    break;
                }
            }
        }

        return _instanceInteractMat;
    }

    private Material PickRim(Material shared, Material glitch)
    {
        return _glitchIntensity > 0 && glitch != null ? glitch : shared;
    }

    private void RebuildMaterials()
    {
        int extraCount = (_interactActive ? 1 : 0)
                       + (_displayActive ? 1 : 0)
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

            // Interact warp renders first (background — PSX affine overlay)
            if (_interactActive)
            {
                var m = GetInteractMat(baseMats);
                if (m != null) mats[slot++] = m;
            }

            // Display renders next (background — subtlest rim)
            if (_displayActive)
            {
                var m = PickRim(s_sharedDisplayMat, _glitchDisplayMat);
                if (m != null) mats[slot++] = m;
            }

            // Prep highlights (liked=green, disliked=red)
            if (_prepLikedActive)
            {
                var m = PickRim(s_sharedPrepLikedMat, _glitchPrepLikedMat);
                if (m != null) mats[slot++] = m;
            }
            if (_prepDislikedActive)
            {
                var m = PickRim(s_sharedPrepDislikedMat, _glitchPrepDislikedMat);
                if (m != null) mats[slot++] = m;
            }

            // Gaze renders second (middle)
            if (_gazeActive)
            {
                var m = PickRim(s_sharedGazeMat, _glitchGazeMat);
                if (m != null) mats[slot++] = m;
            }

            // Hover renders on top (player intent dominates)
            if (_highlighted)
            {
                var m = PickRim(s_sharedRimMat, _glitchRimMat);
                if (m != null) mats[slot++] = m;
            }

            _renderers[r].sharedMaterials = mats;
        }
    }

    private void OnDestroy()
    {
        if (_instanceInteractMat != null) Destroy(_instanceInteractMat);
        if (_glitchRimMat != null) Destroy(_glitchRimMat);
        if (_glitchGazeMat != null) Destroy(_glitchGazeMat);
        if (_glitchDisplayMat != null) Destroy(_glitchDisplayMat);
        if (_glitchPrepLikedMat != null) Destroy(_glitchPrepLikedMat);
        if (_glitchPrepDislikedMat != null) Destroy(_glitchPrepDislikedMat);
        if (_glitchInteractMat != null) Destroy(_glitchInteractMat);
    }

    private static readonly System.Collections.Generic.HashSet<string> s_swappableShaders =
        new System.Collections.Generic.HashSet<string>
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Standard"
    };

    private static Shader s_cachedPSXLitShader;

    private void SwapToPSXLit()
    {
        if (PSXRenderController.Instance == null || !PSXRenderController.Instance.enabled) return;

        if (s_cachedPSXLitShader == null) s_cachedPSXLitShader = Shader.Find("Iris/PSXLit");
        var psxShader = s_cachedPSXLitShader;
        if (psxShader == null) return;

        for (int r = 0; r < _renderers.Length; r++)
        {
            if (_renderers[r] == null) continue;
            foreach (var mat in _renderers[r].sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                if (s_swappableShaders.Contains(mat.shader.name))
                    mat.shader = psxShader;
            }
        }
    }

    /// <summary>
    /// Helper: create a highlight material with given fill + rim colors.
    /// _HighlightColor.a = flat fill alpha, _RimColor.a = rim edge alpha.
    /// </summary>
    private static Material MakeHighlightMat(Color fillColor, Color rimColor, float rimPower,
                                              float pulseSpeed = 2f, float pulseAmount = 0.1f)
    {
        var shader = s_cachedHighlightShader;
        if (shader == null) return null;
        var mat = new Material(shader);
        mat.SetColor("_HighlightColor", fillColor);
        mat.SetColor("_RimColor", rimColor);
        mat.SetFloat("_RimPower", rimPower);
        mat.SetFloat("_PulseSpeed", pulseSpeed);
        mat.SetFloat("_PulseAmount", pulseAmount);
        return mat;
    }

    private static void EnsureSharedMaterials()
    {
        if (s_sharedRimMat == null)
        {
            // Hover — warm ivory, fill + rim, gentle pulse
            s_sharedRimMat = MakeHighlightMat(
                new Color(1f, 0.95f, 0.85f, 0.25f),   // fill: warm ivory, 25% fill
                new Color(1f, 0.95f, 0.85f, 0.5f),     // rim: same tone, stronger at edges
                2.5f, 2f, 0.1f);
            if (s_sharedRimMat == null)
                Debug.LogWarning("[InteractableHighlight] Iris/Highlight shader not found.");
        }

        if (s_sharedGazeMat == null)
        {
            // Gaze — amber, NPC focus
            s_sharedGazeMat = MakeHighlightMat(
                new Color(1f, 0.75f, 0.2f, 0.2f),
                new Color(1f, 0.75f, 0.2f, 0.45f),
                2.5f, 1.5f, 0.08f);
        }

        if (s_sharedDisplayMat == null)
        {
            // Display — warm peach, subtle public item indicator
            s_sharedDisplayMat = MakeHighlightMat(
                new Color(1f, 0.85f, 0.65f, 0.12f),
                new Color(1f, 0.85f, 0.65f, 0.3f),
                3.0f, 1f, 0.05f);
        }

        if (s_sharedPrepLikedMat == null)
        {
            // Prep liked — green
            s_sharedPrepLikedMat = MakeHighlightMat(
                new Color(0.3f, 0.9f, 0.4f, 0.2f),
                new Color(0.3f, 0.9f, 0.4f, 0.45f),
                2.5f, 1.5f, 0.08f);
        }

        if (s_sharedPrepDislikedMat == null)
        {
            // Prep disliked — red
            s_sharedPrepDislikedMat = MakeHighlightMat(
                new Color(0.95f, 0.3f, 0.3f, 0.2f),
                new Color(0.95f, 0.3f, 0.3f, 0.45f),
                2.5f, 1.5f, 0.08f);
        }

        if (s_sharedInteractMat == null)
        {
            var shader = s_cachedInteractShader;
            if (shader != null)
            {
                s_sharedInteractMat = new Material(shader);
                s_sharedInteractMat.SetColor("_Color", new Color(1f, 0.95f, 0.85f, 0.3f));
                s_sharedInteractMat.SetFloat("_WarpIntensity", 0.4f);
                s_sharedInteractMat.SetFloat("_WarpSpeed", 1.2f);
                s_sharedInteractMat.SetFloat("_WarpMin", 0.3f);
                s_sharedInteractMat.SetFloat("_JitterAmount", 0.002f);
            }
        }
    }
}
