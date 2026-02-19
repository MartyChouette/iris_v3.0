using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays newspaper clippings on the fridge door.
/// Small paper quads with character name + day + grade text.
/// Populated from ClippingRegistry each morning.
/// </summary>
public class ClippingDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Parent transform for spawned clipping quads.")]
    [SerializeField] private Transform _clippingParent;

    [Tooltip("Fridge magnet surface for positioning.")]
    [SerializeField] private FridgeMagnetSurface _surface;

    [Header("Layout")]
    [Tooltip("Vertical spacing between clippings.")]
    [SerializeField] private float _verticalSpacing = 0.12f;

    [Tooltip("Starting offset from top of surface.")]
    [SerializeField] private Vector3 _startOffset = new Vector3(0f, 0.3f, -0.01f);

    [Tooltip("Scale of each clipping quad.")]
    [SerializeField] private Vector3 _clippingScale = new Vector3(0.15f, 0.08f, 0.001f);

    private List<GameObject> _spawnedClippings = new List<GameObject>();

    /// <summary>Refresh the display from ClippingRegistry. Called each morning.</summary>
    public void RefreshDisplay()
    {
        // Clear existing
        foreach (var go in _spawnedClippings)
        {
            if (go != null) Destroy(go);
        }
        _spawnedClippings.Clear();

        var clippings = ClippingRegistry.Clippings;
        if (clippings == null || clippings.Count == 0) return;

        Transform parent = _clippingParent != null ? _clippingParent : transform;

        for (int i = 0; i < clippings.Count; i++)
        {
            var clip = clippings[i];
            Vector3 localPos = _startOffset - new Vector3(0f, i * _verticalSpacing, 0f);

            // Create clipping quad
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Clipping_{clip.characterName}_Day{clip.day}";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = _clippingScale;

            // Paper-white material
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(0.95f, 0.92f, 0.85f);

            // Remove collider (decorative)
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Add text label (world-space TMP)
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            var tmp = textGO.AddComponent<TextMeshPro>();
            tmp.text = $"{clip.characterName}\nDay {clip.day} - {clip.grade}";
            tmp.fontSize = 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);

            _spawnedClippings.Add(go);
        }

        Debug.Log($"[ClippingDisplay] Refreshed {clippings.Count} clippings on fridge.");
    }
}
