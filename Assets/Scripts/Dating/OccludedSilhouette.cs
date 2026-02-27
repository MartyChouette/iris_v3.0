using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds a ZTest Greater material pass to all child renderers, producing a
/// colored silhouette visible through walls. Attach to the date NPC root.
/// </summary>
public class OccludedSilhouette : MonoBehaviour
{
    [Tooltip("Silhouette color when occluded.")]
    [SerializeField] private Color _silhouetteColor = new Color(1f, 0.45f, 0.6f, 0.35f);

    private Material _silhouetteMat;

    private void Start()
    {
        // Build a single shared silhouette material
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[OccludedSilhouette] Sprites/Default shader not found.");
            return;
        }

        _silhouetteMat = new Material(shader);
        _silhouetteMat.SetInt("_ZTest", (int)CompareFunction.Greater);
        _silhouetteMat.SetInt("_ZWrite", 0);
        _silhouetteMat.color = _silhouetteColor;
        _silhouetteMat.renderQueue = 3100;

        // Append the silhouette material to every child renderer
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            // Skip particle systems and line renderers
            if (rend is ParticleSystemRenderer || rend is LineRenderer) continue;

            var existing = rend.sharedMaterials;
            var combined = new Material[existing.Length + 1];
            existing.CopyTo(combined, 0);
            combined[existing.Length] = _silhouetteMat;
            rend.materials = combined;
        }

        Debug.Log("[OccludedSilhouette] Silhouette material applied to child renderers.");
    }

    private void OnDestroy()
    {
        if (_silhouetteMat != null)
            Destroy(_silhouetteMat);
    }
}
