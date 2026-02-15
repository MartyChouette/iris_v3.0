using UnityEngine;

/// <summary>
/// Toggleable rim-light overlay material on the object's renderer.
/// Starts disabled — call SetHighlighted(true) on hover, false on exit.
/// Attach to any clickable object (books, records, placeables, etc.).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class InteractableHighlight : MonoBehaviour
{
    private static Material s_sharedRimMat;

    private Renderer _renderer;
    private Material[] _baseMaterials;
    private Material[] _highlightedMaterials;
    private bool _highlighted;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;

        EnsureSharedMaterial();
        if (s_sharedRimMat == null) return;

        // Cache base materials (no rim) and highlighted variant (base + rim)
        _baseMaterials = _renderer.sharedMaterials;
        _highlightedMaterials = new Material[_baseMaterials.Length + 1];
        for (int i = 0; i < _baseMaterials.Length; i++)
            _highlightedMaterials[i] = _baseMaterials[i];
        _highlightedMaterials[_baseMaterials.Length] = s_sharedRimMat;

        // Start with highlight OFF
        _renderer.sharedMaterials = _baseMaterials;
    }

    /// <summary>
    /// Toggle rim light on or off.
    /// </summary>
    public void SetHighlighted(bool on)
    {
        if (_renderer == null || _baseMaterials == null || on == _highlighted) return;
        _highlighted = on;
        _renderer.sharedMaterials = on ? _highlightedMaterials : _baseMaterials;
    }

    private static void EnsureSharedMaterial()
    {
        if (s_sharedRimMat != null) return;

        var shader = Shader.Find("Iris/RimLight");
        if (shader == null)
        {
            Debug.LogWarning("[InteractableHighlight] Iris/RimLight shader not found.");
            return;
        }

        s_sharedRimMat = new Material(shader);
        s_sharedRimMat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.6f));
        s_sharedRimMat.SetFloat("_RimPower", 2.5f);
        s_sharedRimMat.SetFloat("_RimIntensity", 1.0f);
    }
}
