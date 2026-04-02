using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal vertical affection bar on the left side of the screen.
/// Shows during date phases, hidden otherwise. Fills from bottom to top.
/// </summary>
public class AffectionBar : MonoBehaviour
{
    public static AffectionBar Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Bar height in pixels.")]
    [SerializeField] private float _barHeight = 120f;

    [Tooltip("Bar width in pixels.")]
    [SerializeField] private float _barWidth = 4f;

    [Tooltip("Distance from left edge.")]
    [SerializeField] private float _edgeMargin = 20f;

    [Tooltip("Fill color at low affection.")]
    [SerializeField] private Color _lowColor = new Color(0.8f, 0.3f, 0.3f, 0.8f);

    [Tooltip("Fill color at high affection.")]
    [SerializeField] private Color _highColor = new Color(0.9f, 0.5f, 0.6f, 0.9f);

    [Tooltip("Background color.")]
    [SerializeField] private Color _bgColor = new Color(1f, 1f, 1f, 0.15f);

    private GameObject _canvasRoot;
    private Image _bgImage;
    private Image _fillImage;
    private RectTransform _fillRT;
    private float _currentFill;
    private float _targetFill;
    private bool _visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("AffectionBar");
        go.AddComponent<AffectionBar>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Subscribe to affection changes
        if (DateSessionManager.Instance != null)
            DateSessionManager.Instance.OnAffectionChanged.AddListener(OnAffectionChanged);

        // Subscribe to phase changes to show/hide
        if (DayPhaseManager.Instance != null)
            DayPhaseManager.Instance.OnPhaseChanged.AddListener(OnPhaseChanged);
    }

    private void Update()
    {
        if (!_visible) return;

        // Smooth fill
        _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, Time.deltaTime * 2f);

        if (_fillRT != null)
        {
            _fillRT.anchorMax = new Vector2(1f, _currentFill);
            _fillImage.color = Color.Lerp(_lowColor, _highColor, _currentFill);
        }
    }

    private void OnAffectionChanged(float affection)
    {
        _targetFill = Mathf.Clamp01(affection / 100f);
    }

    private void OnPhaseChanged(int phaseInt)
    {
        var phase = (DayPhaseManager.DayPhase)phaseInt;
        SetVisible(phase == DayPhaseManager.DayPhase.DateInProgress);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_canvasRoot != null)
            _canvasRoot.SetActive(visible);
    }

    private void BuildUI()
    {
        _canvasRoot = new GameObject("AffectionBarCanvas");
        _canvasRoot.transform.SetParent(transform, false);
        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Background bar
        var bgGO = new GameObject("AffectionBG");
        bgGO.transform.SetParent(_canvasRoot.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.5f);
        bgRT.anchorMax = new Vector2(0f, 0.5f);
        bgRT.pivot = new Vector2(0f, 0.5f);
        bgRT.anchoredPosition = new Vector2(_edgeMargin, 0f);
        bgRT.sizeDelta = new Vector2(_barWidth, _barHeight);
        _bgImage = bgGO.AddComponent<Image>();
        _bgImage.color = _bgColor;
        _bgImage.raycastTarget = false;

        // Fill bar (child, stretches from bottom)
        var fillGO = new GameObject("AffectionFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        _fillRT = fillGO.AddComponent<RectTransform>();
        _fillRT.anchorMin = Vector2.zero;
        _fillRT.anchorMax = new Vector2(1f, 0f); // starts empty
        _fillRT.offsetMin = Vector2.zero;
        _fillRT.offsetMax = Vector2.zero;
        _fillImage = fillGO.AddComponent<Image>();
        _fillImage.color = _lowColor;
        _fillImage.raycastTarget = false;
    }
}
