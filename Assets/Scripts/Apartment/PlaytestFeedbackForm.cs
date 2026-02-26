using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene-scoped singleton playtest feedback form.
/// F8 opens/closes. Also auto-pops after date ends and on quit.
/// Collects star ratings + free-text + auto-telemetry.
/// Saves JSON + screenshot to Application.persistentDataPath/PlaytestFeedback/.
/// </summary>
public class PlaytestFeedbackForm : MonoBehaviour
{
    public static PlaytestFeedbackForm Instance { get; private set; }

    // ── Session ID (one per play session) ──
    private static string s_sessionId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_sessionId = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("PlaytestFeedbackForm");
        go.AddComponent<PlaytestFeedbackForm>();
    }

    // ── Star rating questions ──
    private const int RatingCount = 4;
    private static readonly string[] RatingLabels = new[]
    {
        "How much did you enjoy playing?",
        "How did picking up and putting things down feel?",
        "How did you feel about the date?",
        "How did you feel about the flower trimming scene?"
    };

    // ── UI references (built at runtime) ──
    private GameObject _canvasRoot;
    private GameObject _formPanel;
    private CanvasGroup _canvasGroup;
    private TMP_InputField _positiveField;
    private TMP_InputField _negativeField;
    private TMP_InputField _bugField;
    private Image[][] _starImages;       // [question][star]
    private int[] _selectedRatings;      // [question] = 0-5
    private TextMeshProUGUI _confirmText;
    private Button _submitButton;
    private Button _closeButton;

    private InputAction _toggleAction;
    private bool _isOpen;
    public bool IsOpen => _isOpen;
    private FeedbackPayload _currentPayload;
    private Action _onCloseCallback;

    private static readonly Color StarOff = new Color(0.3f, 0.3f, 0.3f);
    private static readonly Color StarOn = new Color(0.95f, 0.8f, 0.2f);
    private static readonly Color FieldBg = new Color(0.18f, 0.18f, 0.2f);
    private static readonly Color PanelBg = new Color(0.1f, 0.1f, 0.12f, 0.97f);
    private static readonly Color BtnSubmit = new Color(0.25f, 0.55f, 0.35f);
    private static readonly Color BtnClose = new Color(0.45f, 0.2f, 0.2f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlaytestFeedbackForm] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (string.IsNullOrEmpty(s_sessionId))
            s_sessionId = Guid.NewGuid().ToString();

        _toggleAction = new InputAction("FeedbackToggle", InputActionType.Button, "<Keyboard>/f8");

        BuildUI();
        _canvasRoot.SetActive(false);
    }

    private void OnEnable() => _toggleAction.Enable();
    private void OnDisable() => _toggleAction.Disable();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _toggleAction?.Dispose();
    }

    private void Start()
    {
        // Subscribe to date end — show form when continue is clicked
        if (DateSessionManager.Instance != null)
            DateSessionManager.Instance.OnDateSessionEnded.AddListener(OnDateEnded);
    }

    private void Update()
    {
        bool pressed = _toggleAction.WasPressedThisFrame() || Input.GetKeyDown(KeyCode.F8);
        if (!pressed) return;

        if (_isOpen)
            CloseForm();
        else
            OpenForm();
    }

    private void OnDateEnded(DatePersonalDefinition date, float affection)
    {
        // Small delay so DateEndScreen shows first, then pop form after they dismiss it
        StartCoroutine(OpenAfterDateEndScreen());
    }

    private IEnumerator OpenAfterDateEndScreen()
    {
        // Wait for DateEndScreen to be dismissed
        while (DateEndScreen.Instance != null && DateEndScreen.Instance.IsShowing)
        {
            yield return null;
        }

        // Extra beat so it doesn't feel jarring
        yield return new WaitForSecondsRealtime(0.5f);

        if (!_isOpen)
            OpenForm();
    }

    /// <summary>
    /// Opens the form and calls onClose when the player submits or closes.
    /// Used by SimplePauseMenu to show feedback before quitting.
    /// </summary>
    public void OpenWithCallback(Action onClose)
    {
        _onCloseCallback = onClose;
        OpenForm();
    }

    // ═══════════════════════════════════════
    //  Open / Close
    // ═══════════════════════════════════════

    public void OpenForm()
    {
        if (_isOpen) return;
        _isOpen = true;
        for (int i = 0; i < RatingCount; i++)
            _selectedRatings[i] = 0;
        RefreshAllStars();

        _positiveField.text = "";
        _negativeField.text = "";
        _bugField.text = "";
        _confirmText.text = "";

        _currentPayload = GatherTelemetry();

        _canvasRoot.SetActive(true);
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);

        Debug.Log("[PlaytestFeedbackForm] Opened.");
    }

    private void CloseForm()
    {
        _isOpen = false;
        _canvasRoot.SetActive(false);
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();

        Debug.Log("[PlaytestFeedbackForm] Closed without submitting.");
    }

    // ═══════════════════════════════════════
    //  Submit
    // ═══════════════════════════════════════

    private void OnSubmit()
    {
        if (_currentPayload == null) return;

        _currentPayload.enjoymentRating = _selectedRatings[0];
        _currentPayload.grabFeelRating = _selectedRatings[1];
        _currentPayload.dateFeelRating = _selectedRatings[2];
        _currentPayload.flowerFeelRating = _selectedRatings[3];
        _currentPayload.feedbackPositive = _positiveField.text;
        _currentPayload.feedbackNegative = _negativeField.text;
        _currentPayload.bugReport = _bugField.text;

        SaveFeedback(_currentPayload);
        StartCoroutine(CaptureScreenshotAndSave());
    }

    private void SaveFeedback(FeedbackPayload payload)
    {
        string folder = Path.Combine(Application.persistentDataPath, "PlaytestFeedback");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string jsonPath = Path.Combine(folder, $"feedback_{stamp}.json");
        string json = JsonUtility.ToJson(payload, true);
        File.WriteAllText(jsonPath, json);

        Debug.Log($"[PlaytestFeedbackForm] Saved feedback to {jsonPath}");
    }

    private IEnumerator CaptureScreenshotAndSave()
    {
        // Hide form briefly so screenshot captures gameplay
        _canvasRoot.SetActive(false);
        yield return new WaitForEndOfFrame();

        string folder = Path.Combine(Application.persistentDataPath, "PlaytestFeedback");
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string pngPath = Path.Combine(folder, $"feedback_{stamp}.png");

        try
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(pngPath, bytes);
                Destroy(tex);
                Debug.Log($"[PlaytestFeedbackForm] Screenshot saved to {pngPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlaytestFeedbackForm] Screenshot failed: {e.Message}");
        }

        // Show confirmation
        _canvasRoot.SetActive(true);
        _confirmText.text = "Saved! Thank you!";
        _confirmText.color = new Color(0.4f, 0.9f, 0.5f);

        yield return new WaitForSecondsRealtime(1.5f);

        _isOpen = false;
        _canvasRoot.SetActive(false);
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();

        Debug.Log("[PlaytestFeedbackForm] Submitted and closed.");
    }

    // ═══════════════════════════════════════
    //  Telemetry gathering
    // ═══════════════════════════════════════

    private FeedbackPayload GatherTelemetry()
    {
        var p = new FeedbackPayload();

        // Telemetry only gathered if player consented
        if (PlaytestConsentScreen.HasConsent)
        {
            p.sessionId = s_sessionId;
            p.timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601
            p.buildVersion = Application.version;
            p.playTimeSeconds = Time.realtimeSinceStartup;
            p.currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : -1;
            p.currentPhase = DayPhaseManager.Instance != null
                ? DayPhaseManager.Instance.CurrentPhase.ToString()
                : "Unknown";
            p.tidiness = TidyScorer.Instance != null ? TidyScorer.Instance.OverallTidiness : -1f;
            p.mood = MoodMachine.Instance != null ? MoodMachine.Instance.Mood : -1f;
            p.dateCount = DateHistory.Entries.Count;
            p.currentDateCharacter = DateSessionManager.Instance != null
                && DateSessionManager.Instance.CurrentDate != null
                ? DateSessionManager.Instance.CurrentDate.characterName
                : "None";
            p.currentAffection = DateSessionManager.Instance != null
                ? DateSessionManager.Instance.Affection
                : 0f;
            p.systemInfo = $"{SystemInfo.operatingSystem} | {SystemInfo.graphicsDeviceName} | {SystemInfo.systemMemorySize}MB RAM";
            p.screenResolution = $"{Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio}Hz";
            p.accessibilityNotes = $"TextScale={AccessibilitySettings.TextScale:F2}, " +
                $"TimerMult={AccessibilitySettings.TimerMultiplier:F1}, " +
                $"PSX={AccessibilitySettings.PSXEnabled}";
        }

        return p;
    }

    // ═══════════════════════════════════════
    //  Star ratings
    // ═══════════════════════════════════════

    private void SelectRating(int questionIndex, int rating)
    {
        _selectedRatings[questionIndex] = rating;
        RefreshStarRow(questionIndex);
    }

    private void RefreshStarRow(int questionIndex)
    {
        if (_starImages == null || _starImages[questionIndex] == null) return;
        var row = _starImages[questionIndex];
        int selected = _selectedRatings[questionIndex];
        for (int i = 0; i < row.Length; i++)
            row[i].color = (i < selected) ? StarOn : StarOff;
    }

    private void RefreshAllStars()
    {
        for (int i = 0; i < RatingCount; i++)
            RefreshStarRow(i);
    }

    // ═══════════════════════════════════════
    //  Runtime UI construction
    // ═══════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas ──
        _canvasRoot = new GameObject("FeedbackFormCanvas");
        _canvasRoot.transform.SetParent(transform, false);

        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasRoot.AddComponent<GraphicRaycaster>();

        // ── Dim background ──
        var dimBg = MakeChild(_canvasRoot, "DimBg");
        var dimRT = dimBg.AddComponent<RectTransform>();
        StretchFill(dimRT);
        var dimImg = dimBg.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.7f);

        // ── Panel ──
        var panel = MakeChild(_canvasRoot, "Panel");
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(700f, 950f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = PanelBg;
        _formPanel = panel;

        _starImages = new Image[RatingCount][];
        _selectedRatings = new int[RatingCount];

        float yPos = -25f;

        // ── Title ──
        yPos = AddLabel(panel, "Title", "Playtest Feedback", 28f, yPos, 40f,
            new Color(0.95f, 0.92f, 0.85f), TextAlignmentOptions.Center);

        // ── Star rating rows ──
        for (int q = 0; q < RatingCount; q++)
        {
            yPos -= 8f;
            yPos = AddLabel(panel, $"RatingLabel_{q}", RatingLabels[q], 17f, yPos, 24f,
                new Color(0.7f, 0.7f, 0.68f), TextAlignmentOptions.Center);
            yPos -= 3f;
            BuildStarRow(panel, yPos, q);
            yPos -= 50f;
        }

        // ── Text fields ──
        yPos -= 5f;
        yPos = AddLabel(panel, "PosLabel", "What did you enjoy?", 17f, yPos, 24f,
            new Color(0.7f, 0.7f, 0.68f), TextAlignmentOptions.Left);
        _positiveField = AddInputField(panel, "PositiveField", yPos, 70f, "Type here...");
        yPos -= 80f;

        yPos = AddLabel(panel, "NegLabel", "What was confusing or frustrating?", 17f, yPos, 24f,
            new Color(0.7f, 0.7f, 0.68f), TextAlignmentOptions.Left);
        _negativeField = AddInputField(panel, "NegativeField", yPos, 70f, "Type here...");
        yPos -= 80f;

        yPos = AddLabel(panel, "BugLabel", "Any bugs to report?", 17f, yPos, 24f,
            new Color(0.7f, 0.7f, 0.68f), TextAlignmentOptions.Left);
        _bugField = AddInputField(panel, "BugField", yPos, 70f, "Type here...");
        yPos -= 80f;

        // ── Confirm text ──
        var confirmGO = MakeChild(panel, "ConfirmText");
        var confirmRT = confirmGO.AddComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.5f, 1f);
        confirmRT.anchorMax = new Vector2(0.5f, 1f);
        confirmRT.pivot = new Vector2(0.5f, 1f);
        confirmRT.anchoredPosition = new Vector2(0f, yPos);
        confirmRT.sizeDelta = new Vector2(600f, 30f);
        _confirmText = confirmGO.AddComponent<TextMeshProUGUI>();
        _confirmText.text = "";
        _confirmText.fontSize = 18f;
        _confirmText.alignment = TextAlignmentOptions.Center;
        _confirmText.color = new Color(0.4f, 0.9f, 0.5f);
        yPos -= 40f;

        // ── Buttons ──
        _submitButton = AddButton(panel, "SubmitBtn", "Submit", -100f, yPos, 180f, 45f, BtnSubmit);
        _submitButton.onClick.AddListener(OnSubmit);

        _closeButton = AddButton(panel, "CloseBtn", "Close", 100f, yPos, 180f, 45f, BtnClose);
        _closeButton.onClick.AddListener(CloseForm);
    }

    private void BuildStarRow(GameObject parent, float yPos, int questionIndex)
    {
        var rowImages = new Image[5];

        float starSize = 40f;
        float spacing = 50f;
        float startX = -2f * spacing;

        for (int i = 0; i < 5; i++)
        {
            int rating = i + 1;         // capture for closure
            int qIdx = questionIndex;   // capture for closure

            var starGO = MakeChild(parent, $"Star_{questionIndex}_{rating}");
            var rt = starGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * spacing, yPos - starSize * 0.5f);
            rt.sizeDelta = new Vector2(starSize, starSize);

            var img = starGO.AddComponent<Image>();
            img.color = StarOff;
            rowImages[i] = img;

            var btn = starGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SelectRating(qIdx, rating));

            // Star number label
            var labelGO = MakeChild(starGO, "Num");
            var labelRT = labelGO.AddComponent<RectTransform>();
            StretchFill(labelRT);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = rating.ToString();
            tmp.fontSize = 20f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        _starImages[questionIndex] = rowImages;
    }

    // ── UI Factory Helpers ──

    private float AddLabel(GameObject parent, string name, string text, float fontSize,
        float yPos, float height, Color color, TextAlignmentOptions align)
    {
        var go = MakeChild(parent, name);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 1f);
        rt.anchorMax = new Vector2(0.92f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(0f, height);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;

        return yPos - height;
    }

    private TMP_InputField AddInputField(GameObject parent, string name, float yPos,
        float height, string placeholder)
    {
        var fieldGO = MakeChild(parent, name);
        var fieldRT = fieldGO.AddComponent<RectTransform>();
        fieldRT.anchorMin = new Vector2(0.08f, 1f);
        fieldRT.anchorMax = new Vector2(0.92f, 1f);
        fieldRT.pivot = new Vector2(0.5f, 1f);
        fieldRT.anchoredPosition = new Vector2(0f, yPos);
        fieldRT.sizeDelta = new Vector2(0f, height);

        var fieldImg = fieldGO.AddComponent<Image>();
        fieldImg.color = FieldBg;

        // Text area
        var textArea = MakeChild(fieldGO, "TextArea");
        var textAreaRT = textArea.AddComponent<RectTransform>();
        textAreaRT.anchorMin = new Vector2(0f, 0f);
        textAreaRT.anchorMax = new Vector2(1f, 1f);
        textAreaRT.offsetMin = new Vector2(10f, 5f);
        textAreaRT.offsetMax = new Vector2(-10f, -5f);
        var textAreaMask = textArea.AddComponent<RectMask2D>();

        // Input text
        var inputTextGO = MakeChild(textArea, "Text");
        var inputTextRT = inputTextGO.AddComponent<RectTransform>();
        StretchFill(inputTextRT);
        var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 16f;
        inputTMP.color = new Color(0.9f, 0.9f, 0.88f);
        inputTMP.enableWordWrapping = true;

        // Placeholder
        var phGO = MakeChild(textArea, "Placeholder");
        var phRT = phGO.AddComponent<RectTransform>();
        StretchFill(phRT);
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 16f;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.5f, 0.5f, 0.48f);
        phTMP.enableWordWrapping = true;

        // TMP_InputField component
        var inputField = fieldGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = phTMP;
        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        inputField.characterLimit = 500;
        inputField.richText = false;

        return inputField;
    }

    private Button AddButton(GameObject parent, string name, string label,
        float x, float yPos, float w, float h, Color color)
    {
        var btnGO = MakeChild(parent, name);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 1f);
        btnRT.anchorMax = new Vector2(0.5f, 1f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(x, yPos - h * 0.5f);
        btnRT.sizeDelta = new Vector2(w, h);

        var img = btnGO.AddComponent<Image>();
        img.color = color;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = MakeChild(btnGO, "Label");
        var txtRT = txtGO.AddComponent<RectTransform>();
        StretchFill(txtRT);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
