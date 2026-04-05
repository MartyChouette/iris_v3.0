using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space UI cursor that replaces the hardware cursor during pouring.
/// Rotates smoothly based on PourDragHelper.TiltAngle to show the pour tilt.
/// Auto-spawns its own overlay canvas.
/// </summary>
public class PourCursorOverlay : MonoBehaviour
{
    public static PourCursorOverlay Instance { get; private set; }

    private Canvas _canvas;
    private RawImage _cursorImage;
    private RectTransform _cursorRT;
    private bool _active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("PourCursorOverlay");
        go.AddComponent<PourCursorOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildUI()
    {
        // Overlay canvas on top of everything
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        // No raycaster — this is purely visual, shouldn't block clicks

        var imgGO = new GameObject("PourCursor");
        imgGO.transform.SetParent(transform, false);
        _cursorImage = imgGO.AddComponent<RawImage>();
        _cursorRT = imgGO.GetComponent<RectTransform>();
        _cursorRT.sizeDelta = new Vector2(32f, 32f);
        _cursorRT.pivot = new Vector2(0.5f, 0.5f);

        _cursorImage.raycastTarget = false;
        imgGO.SetActive(false);
    }

    private void LateUpdate()
    {
        bool shouldShow = PourDragHelper.IsDragging;

        if (shouldShow && !_active)
        {
            // Grab the current context cursor texture from GlobalCursorManager
            var gcm = GlobalCursorManager.Instance;
            Texture2D tex = gcm != null ? gcm.GetCurrentCursorTexture() : null;
            if (tex != null)
            {
                _cursorImage.texture = tex;
                _cursorRT.sizeDelta = new Vector2(tex.width, tex.height);
            }

            Cursor.visible = false;
            _cursorImage.gameObject.SetActive(true);
            _active = true;
        }
        else if (!shouldShow && _active)
        {
            Cursor.visible = true;
            _cursorImage.gameObject.SetActive(false);
            _active = false;
        }

        if (!_active) return;

        // Position at mouse
        _cursorRT.position = IrisInput.CursorPosition;

        // Rotate: tilt clockwise as player drags down
        float angle = -PourDragHelper.TiltAngle;
        _cursorRT.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Fade alpha to match the cursor manager
        float alpha = 1f;
        if (GlobalCursorManager.Instance != null)
            alpha = GlobalCursorManager.Instance.CurrentAlpha;
        _cursorImage.color = new Color(1f, 1f, 1f, alpha);
    }
}
