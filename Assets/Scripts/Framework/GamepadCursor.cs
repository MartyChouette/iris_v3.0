using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a visible cursor reticle when gamepad is the active input device.
/// Created automatically by IrisInput. Follows IrisInput.CursorPosition.
/// Hidden when mouse/keyboard or touch is active.
/// </summary>
public class GamepadCursor : MonoBehaviour
{
    private RectTransform _cursorRect;
    private CanvasGroup _group;

    private void Awake()
    {
        // Screen-space overlay canvas at highest sort order
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        // Cursor container
        var cursorGO = new GameObject("CursorReticle");
        cursorGO.transform.SetParent(transform, false);

        _cursorRect = cursorGO.AddComponent<RectTransform>();
        _cursorRect.sizeDelta = new Vector2(28f, 28f);

        // Outer ring
        var ring = cursorGO.AddComponent<RawImage>();
        ring.texture = CreateReticleTexture();
        ring.color = new Color(1f, 1f, 1f, 0.85f);
        ring.raycastTarget = false;

        // Center dot
        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(cursorGO.transform, false);
        var dotRect = dotGO.AddComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(4f, 4f);
        var dotImg = dotGO.AddComponent<RawImage>();
        dotImg.color = Color.white;
        dotImg.raycastTarget = false;

        // Canvas group for fade
        _group = cursorGO.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;
    }

    private void LateUpdate()
    {
        bool show = IrisInput.IsGamepad;
        _group.alpha = show ? 1f : 0f;

        if (show && _cursorRect != null)
            _cursorRect.position = IrisInput.CursorPosition;
    }

    /// <summary>Procedural 32x32 ring texture for the gamepad cursor reticle.</summary>
    private static Texture2D CreateReticleTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = size * 0.5f;
        float outerR = center;
        float innerR = center - 2.5f;

        var clear = Color.clear;
        var white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                tex.SetPixel(x, y, (dist >= innerR && dist <= outerR) ? white : clear);
            }
        }

        tex.Apply();
        return tex;
    }
}
