using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable vertical fill bar for pour mechanics (watering + drink making).
/// Self-contained — builds its own child RectTransforms on Awake.
/// Scene builders just AddComponent and set properties.
/// </summary>
[DisallowMultipleComponent]
public class PourBarUI : MonoBehaviour
{
    [Header("Dimensions")]
    [Tooltip("Height of the bar in pixels.")]
    public float barHeight = 200f;

    [Tooltip("Width of the bar in pixels.")]
    public float barWidth = 40f;

    [Header("Colors")]
    [Tooltip("Background color (the 'glass').")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);

    [Tooltip("Liquid fill color.")]
    public Color liquidColor = new Color(0.3f, 0.6f, 0.9f, 0.9f);

    [Tooltip("Foam overlay color (lighter than liquid).")]
    public Color foamColor = new Color(0.7f, 0.85f, 1f, 0.7f);

    [Tooltip("Target zone band color.")]
    public Color targetZoneColor = new Color(1f, 1f, 1f, 0.2f);

    [Tooltip("Target line color.")]
    public Color targetLineColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("Overflow flash color.")]
    public Color overflowColor = new Color(1f, 0.15f, 0.1f, 0.6f);

    // ── Runtime references (built in Awake) ──────────────────────────

    private RectTransform _barRoot;
    private Image _backgroundImage;
    private RectTransform _liquidRT;
    private Image _liquidImage;
    private RectTransform _foamRT;
    private Image _foamImage;
    private RectTransform _targetZoneRT;
    private Image _targetZoneImage;
    private RectTransform _targetLineRT;
    private Image _targetLineImage;
    private TMP_Text _scoreText;

    private bool _overflowing;
    private bool _lastOverflowing;
    private bool _lastVisible;
    private bool _lastScoreVisible;
    private float _smoothFill;
    private float _smoothFoam;

    private const float LerpSpeed = 8f;

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Update the bar levels each frame.
    /// All values are 0-1 normalized.
    /// </summary>
    public void SetLevels(float fill, float foam, float target, float tolerance)
    {
        _smoothFill = Mathf.Lerp(_smoothFill, fill, Time.deltaTime * LerpSpeed);
        _smoothFoam = Mathf.Lerp(_smoothFoam, foam, Time.deltaTime * LerpSpeed);

        // Liquid fill — anchored to bottom, scales up
        if (_liquidRT != null)
        {
            _liquidRT.anchorMin = Vector2.zero;
            _liquidRT.anchorMax = new Vector2(1f, Mathf.Clamp01(_smoothFill));
            _liquidRT.offsetMin = Vector2.zero;
            _liquidRT.offsetMax = Vector2.zero;
        }

        // Foam — sits between fill and foam level
        if (_foamRT != null)
        {
            float foamBottom = Mathf.Clamp01(_smoothFill);
            float foamTop = Mathf.Clamp01(_smoothFoam);
            _foamRT.anchorMin = new Vector2(0f, foamBottom);
            _foamRT.anchorMax = new Vector2(1f, foamTop);
            _foamRT.offsetMin = Vector2.zero;
            _foamRT.offsetMax = Vector2.zero;
        }

        // Target zone band
        if (_targetZoneRT != null)
        {
            float zoneBottom = Mathf.Clamp01(target - tolerance);
            float zoneTop = Mathf.Clamp01(target + tolerance);
            _targetZoneRT.anchorMin = new Vector2(0f, zoneBottom);
            _targetZoneRT.anchorMax = new Vector2(1f, zoneTop);
            _targetZoneRT.offsetMin = Vector2.zero;
            _targetZoneRT.offsetMax = Vector2.zero;
        }

        // Target line
        if (_targetLineRT != null)
        {
            float lineY = Mathf.Clamp01(target);
            _targetLineRT.anchorMin = new Vector2(0f, lineY);
            _targetLineRT.anchorMax = new Vector2(1f, lineY);
            _targetLineRT.offsetMin = new Vector2(0f, -1f);
            _targetLineRT.offsetMax = new Vector2(0f, 1f);
        }

        // Hide score text while actively pouring
        if (_scoreText != null && _lastScoreVisible)
        {
            _scoreText.gameObject.SetActive(false);
            _lastScoreVisible = false;
        }
    }

    /// <summary>
    /// Toggle the overflow flash effect.
    /// </summary>
    public void SetOverflowing(bool overflowing)
    {
        _overflowing = overflowing;
    }

    /// <summary>
    /// Show score text below the bar and freeze the fill display.
    /// </summary>
    public void ShowScore(string text)
    {
        if (_scoreText != null)
        {
            if (!_lastScoreVisible)
            {
                _scoreText.gameObject.SetActive(true);
                _lastScoreVisible = true;
            }
            _scoreText.text = text;
        }
    }

    /// <summary>
    /// Change the liquid fill color at runtime.
    /// </summary>
    public void SetLiquidColor(Color c)
    {
        liquidColor = c;
        if (_liquidImage != null)
            _liquidImage.color = c;
    }

    /// <summary>
    /// Show or hide the entire bar.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_barRoot != null && visible != _lastVisible)
        {
            _barRoot.gameObject.SetActive(visible);
            _lastVisible = visible;
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        BuildBar();
    }

    void Update()
    {
        if (_backgroundImage == null) return;

        if (_overflowing)
        {
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            _backgroundImage.color = Color.Lerp(backgroundColor, overflowColor, pulse);
            _lastOverflowing = true;
        }
        else if (_lastOverflowing)
        {
            _backgroundImage.color = backgroundColor;
            _lastOverflowing = false;
        }
    }

    // ── Build child hierarchy ────────────────────────────────────────

    private void BuildBar()
    {
        var myRT = GetComponent<RectTransform>();
        if (myRT == null)
            myRT = gameObject.AddComponent<RectTransform>();

        // Bar root container
        var barRootGO = new GameObject("BarRoot");
        barRootGO.transform.SetParent(transform, false);
        _barRoot = barRootGO.AddComponent<RectTransform>();
        _barRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _barRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _barRoot.sizeDelta = new Vector2(barWidth, barHeight);
        _barRoot.anchoredPosition = Vector2.zero;

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(_barRoot, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        _backgroundImage = bgGO.AddComponent<Image>();
        _backgroundImage.color = backgroundColor;

        // Liquid fill
        var liquidGO = new GameObject("Liquid");
        liquidGO.transform.SetParent(_barRoot, false);
        _liquidRT = liquidGO.AddComponent<RectTransform>();
        _liquidRT.anchorMin = Vector2.zero;
        _liquidRT.anchorMax = Vector2.zero;
        _liquidRT.offsetMin = Vector2.zero;
        _liquidRT.offsetMax = Vector2.zero;
        _liquidImage = liquidGO.AddComponent<Image>();
        _liquidImage.color = liquidColor;

        // Foam overlay
        var foamGO = new GameObject("Foam");
        foamGO.transform.SetParent(_barRoot, false);
        _foamRT = foamGO.AddComponent<RectTransform>();
        _foamRT.anchorMin = Vector2.zero;
        _foamRT.anchorMax = Vector2.zero;
        _foamRT.offsetMin = Vector2.zero;
        _foamRT.offsetMax = Vector2.zero;
        _foamImage = foamGO.AddComponent<Image>();
        _foamImage.color = foamColor;

        // Target zone band
        var zoneGO = new GameObject("TargetZone");
        zoneGO.transform.SetParent(_barRoot, false);
        _targetZoneRT = zoneGO.AddComponent<RectTransform>();
        _targetZoneRT.anchorMin = Vector2.zero;
        _targetZoneRT.anchorMax = Vector2.zero;
        _targetZoneRT.offsetMin = Vector2.zero;
        _targetZoneRT.offsetMax = Vector2.zero;
        _targetZoneImage = zoneGO.AddComponent<Image>();
        _targetZoneImage.color = targetZoneColor;

        // Target line (thin horizontal marker)
        var lineGO = new GameObject("TargetLine");
        lineGO.transform.SetParent(_barRoot, false);
        _targetLineRT = lineGO.AddComponent<RectTransform>();
        _targetLineRT.anchorMin = new Vector2(0f, 0.5f);
        _targetLineRT.anchorMax = new Vector2(1f, 0.5f);
        _targetLineRT.offsetMin = new Vector2(0f, -1f);
        _targetLineRT.offsetMax = new Vector2(0f, 1f);
        _targetLineImage = lineGO.AddComponent<Image>();
        _targetLineImage.color = targetLineColor;

        // Score text below the bar
        var scoreGO = new GameObject("ScoreText");
        scoreGO.transform.SetParent(transform, false);
        var scoreRT = scoreGO.AddComponent<RectTransform>();
        scoreRT.anchorMin = new Vector2(0.5f, 0.5f);
        scoreRT.anchorMax = new Vector2(0.5f, 0.5f);
        scoreRT.sizeDelta = new Vector2(120f, 30f);
        scoreRT.anchoredPosition = new Vector2(0f, -(barHeight * 0.5f + 20f));
        _scoreText = scoreGO.AddComponent<TextMeshProUGUI>();
        _scoreText.fontSize = 18f;
        _scoreText.alignment = TextAlignmentOptions.Center;
        _scoreText.color = Color.white;
        _scoreText.gameObject.SetActive(false);
    }
}
