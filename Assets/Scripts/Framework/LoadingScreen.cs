using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DontDestroyOnLoad loading screen with async scene loading,
/// procedural spinning flower, progress bar, and animated text.
/// Self-destructs after the new scene finishes loading.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private static LoadingScreen _instance;

    private CanvasGroup _canvasGroup;
    private Image _barFill;
    private TMP_Text _loadingText;
    private RectTransform _flowerPivot;

    private float _ellipsisTimer;
    private int _ellipsisCount;

    // ═══════════════════════════════════════════════════════════════
    // Static entry point
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates the loading screen and begins async scene load.
    /// Duplicate calls while already loading are ignored.
    /// </summary>
    public static void LoadScene(int buildIndex)
    {
        if (_instance != null) return;

        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LoadingScreen>();
        _instance.Build();
        _instance.StartCoroutine(_instance.LoadCoroutine(buildIndex));
    }

    /// <summary>
    /// Creates the loading screen using a pre-started AsyncOperation
    /// (with allowSceneActivation = false). Sets activation to true
    /// and monitors progress. If the preload is already at 0.9, the
    /// scene activates nearly instantly.
    /// </summary>
    public static void LoadPreloaded(AsyncOperation preloadedOp)
    {
        if (_instance != null) return;

        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LoadingScreen>();
        _instance.Build();
        _instance.StartCoroutine(_instance.ActivatePreloadedCoroutine(preloadedOp));
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // Build UI procedurally
    // ═══════════════════════════════════════════════════════════════

    private void Build()
    {
        // Canvas — overlay, highest sort order
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // Black background
        var bg = CreateChild<Image>("Background");
        Stretch(bg.rectTransform);
        bg.color = Color.black;

        // Flower pivot (centered, above middle)
        var pivotGO = new GameObject("FlowerPivot");
        pivotGO.transform.SetParent(transform, false);
        _flowerPivot = pivotGO.AddComponent<RectTransform>();
        _flowerPivot.anchorMin = new Vector2(0.5f, 0.5f);
        _flowerPivot.anchorMax = new Vector2(0.5f, 0.5f);
        _flowerPivot.anchoredPosition = new Vector2(0f, 60f);
        _flowerPivot.sizeDelta = new Vector2(100f, 100f);

        BuildFlower(_flowerPivot);

        // Progress bar background
        var barBgGO = new GameObject("BarBG");
        barBgGO.transform.SetParent(transform, false);
        var barBgRT = barBgGO.AddComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.5f, 0.5f);
        barBgRT.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRT.anchoredPosition = new Vector2(0f, -40f);
        barBgRT.sizeDelta = new Vector2(300f, 4f);
        var barBgImg = barBgGO.AddComponent<Image>();
        barBgImg.color = new Color(0.2f, 0.2f, 0.2f);

        // Progress bar fill
        var fillGO = new GameObject("BarFill");
        fillGO.transform.SetParent(barBgGO.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = Vector2.zero;
        fillRT.anchoredPosition = Vector2.zero;
        _barFill = fillGO.AddComponent<Image>();
        _barFill.color = new Color(0.95f, 0.9f, 0.8f); // eggshell

        // Loading text
        var textGO = new GameObject("LoadingText");
        textGO.transform.SetParent(transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.5f);
        textRT.anchorMax = new Vector2(0.5f, 0.5f);
        textRT.anchoredPosition = new Vector2(0f, -70f);
        textRT.sizeDelta = new Vector2(300f, 40f);
        _loadingText = textGO.AddComponent<TextMeshProUGUI>();
        _loadingText.text = "Loading.";
        _loadingText.fontSize = 24f;
        _loadingText.alignment = TextAlignmentOptions.Center;
        _loadingText.color = new Color(0.95f, 0.9f, 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════
    // Procedural flower
    // ═══════════════════════════════════════════════════════════════

    private void BuildFlower(RectTransform parent)
    {
        // 8 petals arranged radially
        Color petalColor = new Color(1f, 0.85f, 0.88f); // soft pink
        float petalDist = 28f;
        Vector2 petalSize = new Vector2(22f, 36f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            float rad = angle * Mathf.Deg2Rad;
            var petal = CreateChild<Image>("Petal" + i, parent);
            petal.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            petal.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            petal.rectTransform.anchoredPosition = new Vector2(
                Mathf.Cos(rad) * petalDist,
                Mathf.Sin(rad) * petalDist);
            petal.rectTransform.sizeDelta = petalSize;
            petal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            petal.color = petalColor;

            // Round the petals using a trick: default UI sprite is fine,
            // but we can soften them by using the Knob sprite if available
            petal.sprite = GetRoundSprite();
            petal.type = Image.Type.Simple;
        }

        // Center circle (yellow)
        var center = CreateChild<Image>("Center", parent);
        center.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        center.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        center.rectTransform.anchoredPosition = Vector2.zero;
        center.rectTransform.sizeDelta = new Vector2(30f, 30f);
        center.color = new Color(1f, 0.88f, 0.4f); // warm yellow
        center.sprite = GetRoundSprite();
    }

    private static Sprite GetRoundSprite()
    {
        // Unity built-in "Knob" sprite — round circle
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
    }

    // ═══════════════════════════════════════════════════════════════
    // Async load coroutine
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator LoadCoroutine(int buildIndex)
    {
        // Brief hold so menu fade can finish
        float holdTimer = 0.3f;
        while (holdTimer > 0f)
        {
            holdTimer -= Time.unscaledDeltaTime;
            yield return null;
        }

        var op = SceneManager.LoadSceneAsync(buildIndex);
        if (op == null)
        {
            Debug.LogError("[LoadingScreen] LoadSceneAsync returned null.");
            Destroy(gameObject);
            yield break;
        }

        // Update visuals while loading
        while (!op.isDone)
        {
            // Unity async progress goes 0→0.9 then jumps to 1.0 on activation
            float t = Mathf.Clamp01(op.progress / 0.9f);
            SetProgress(t);
            UpdateFlower();
            UpdateEllipsis();
            yield return null;
        }

        SetProgress(1f);

        // Wait one frame for new scene Awake/Start
        yield return null;

        // Fade out
        float fadeTime = 0.5f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
            UpdateFlower();
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator ActivatePreloadedCoroutine(AsyncOperation op)
    {
        // Brief hold so menu fade can finish
        float holdTimer = 0.3f;
        while (holdTimer > 0f)
        {
            holdTimer -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (op == null)
        {
            Debug.LogError("[LoadingScreen] Preloaded AsyncOperation is null.");
            Destroy(gameObject);
            yield break;
        }

        // Show current preload progress before activating
        float preProgress = Mathf.Clamp01(op.progress / 0.9f);
        SetProgress(preProgress);

        // Allow the scene to activate
        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            float t = Mathf.Clamp01(op.progress / 0.9f);
            SetProgress(t);
            UpdateFlower();
            UpdateEllipsis();
            yield return null;
        }

        SetProgress(1f);

        // Wait one frame for new scene Awake/Start
        yield return null;

        // Fade out
        float fadeTime = 0.5f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
            UpdateFlower();
            yield return null;
        }

        Destroy(gameObject);
    }

    // ═══════════════════════════════════════════════════════════════
    // Visual updates
    // ═══════════════════════════════════════════════════════════════

    private void SetProgress(float t)
    {
        if (_barFill != null)
            _barFill.rectTransform.anchorMax = new Vector2(t, 1f);
    }

    private void UpdateFlower()
    {
        if (_flowerPivot == null) return;

        // Slow rotation
        _flowerPivot.Rotate(0f, 0f, -30f * Time.unscaledDeltaTime);

        // Gentle scale pulse
        float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 2f);
        _flowerPivot.localScale = new Vector3(pulse, pulse, 1f);
    }

    private void UpdateEllipsis()
    {
        _ellipsisTimer += Time.unscaledDeltaTime;
        if (_ellipsisTimer >= 0.5f)
        {
            _ellipsisTimer = 0f;
            _ellipsisCount = (_ellipsisCount + 1) % 3;
        }

        if (_loadingText != null)
        {
            _loadingText.text = _ellipsisCount switch
            {
                0 => "Loading.",
                1 => "Loading..",
                _ => "Loading..."
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private T CreateChild<T>(string name, RectTransform parent = null) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.AddComponent<RectTransform>();
        return go.AddComponent<T>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
