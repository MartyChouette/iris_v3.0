using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Screen-space overlay that shows text captions for audio events.
/// DontDestroyOnLoad singleton (same lifecycle as AudioManager).
/// Call CaptionDisplay.Show(caption, duration) from anywhere.
/// </summary>
public class CaptionDisplay : MonoBehaviour
{
    private static CaptionDisplay s_instance;

    private const int MaxVisible = 3;
    private const float FadeSpeed = 3f;

    private Canvas _canvas;
    private readonly List<CaptionEntry> _activeEntries = new();
    private readonly Queue<CaptionEntry> _pool = new();

    private struct CaptionEntry
    {
        public GameObject root;
        public TMP_Text text;
        public CanvasGroup group;
        public float remaining;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        if (transform.parent != null)
            transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }

    private void Update()
    {
        for (int i = _activeEntries.Count - 1; i >= 0; i--)
        {
            var entry = _activeEntries[i];
            entry.remaining -= Time.unscaledDeltaTime;

            // Fade out in the last 0.5 seconds
            if (entry.remaining < 0.5f)
                entry.group.alpha = Mathf.MoveTowards(entry.group.alpha, 0f, FadeSpeed * Time.unscaledDeltaTime);

            _activeEntries[i] = entry;

            if (entry.remaining <= 0f)
            {
                entry.root.SetActive(false);
                _pool.Enqueue(entry);
                _activeEntries.RemoveAt(i);
            }
        }

        UpdateLayout();
    }

    /// <summary>Show a caption on screen. Safe to call from anywhere.</summary>
    public static void Show(string caption, float duration = 2f)
    {
        if (s_instance == null) return;
        if (string.IsNullOrEmpty(caption)) return;
        if (!AccessibilitySettings.CaptionsEnabled) return;
        s_instance.ShowInternal(caption, duration);
    }

    private void ShowInternal(string caption, float duration)
    {
        // Evict oldest if at capacity
        while (_activeEntries.Count >= MaxVisible)
        {
            var oldest = _activeEntries[0];
            oldest.root.SetActive(false);
            _pool.Enqueue(oldest);
            _activeEntries.RemoveAt(0);
        }

        CaptionEntry entry;
        if (_pool.Count > 0)
        {
            entry = _pool.Dequeue();
            entry.root.SetActive(true);
        }
        else
        {
            entry = CreateEntry();
        }

        entry.text.text = caption;
        entry.remaining = duration;
        entry.group.alpha = 1f;
        _activeEntries.Add(entry);
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        // Stack captions from bottom up
        float y = 40f;
        for (int i = _activeEntries.Count - 1; i >= 0; i--)
        {
            var rt = _activeEntries[i].root.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, y);
            y += rt.sizeDelta.y + 8f;
        }
    }

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;

        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private CaptionEntry CreateEntry()
    {
        var go = new GameObject("Caption");
        go.transform.SetParent(_canvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(600f, 40f);

        var group = go.AddComponent<CanvasGroup>();

        // Background
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 4f);
        textRT.offsetMax = new Vector2(-12f, -4f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;

        return new CaptionEntry
        {
            root = go,
            text = tmp,
            group = group,
            remaining = 0f
        };
    }
}
