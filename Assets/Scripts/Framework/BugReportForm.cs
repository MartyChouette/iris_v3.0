using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene-scoped singleton bug report overlay.
/// F9 opens/closes. Captures screenshot before showing form.
/// Sends to Discord webhook + saves JSON locally.
/// </summary>
public class BugReportForm : MonoBehaviour
{
    public static BugReportForm Instance { get; private set; }

    private static string s_sessionId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_sessionId = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (Instance != null) return;
        var go = new GameObject("BugReportForm");
        go.AddComponent<BugReportForm>();
    }

    // ── Severity ──
    private static readonly string[] SeverityLabels = { "Low", "Medium", "High", "Critical" };
    private static readonly Color[] SeverityColors =
    {
        new Color(0.34f, 0.65f, 0.38f),  // Low — green
        new Color(0.85f, 0.75f, 0.25f),  // Medium — yellow
        new Color(0.85f, 0.35f, 0.25f),  // High — red-orange
        new Color(0.65f, 0.18f, 0.20f),  // Critical — dark red
    };
    private static readonly int[] SeverityDiscordColors =
    {
        0x57F287,  // Low — green
        0xFEE75C,  // Medium — yellow
        0xED4245,  // High — red
        0xA12D33,  // Critical — dark red
    };

    // ── UI colors ──
    private static readonly Color PanelBg = new Color(0.12f, 0.10f, 0.10f, 0.97f);
    private static readonly Color FieldBg = new Color(0.18f, 0.16f, 0.16f);
    private static readonly Color BtnSubmit = new Color(0.25f, 0.55f, 0.35f);
    private static readonly Color BtnClose = new Color(0.45f, 0.2f, 0.2f);
    private static readonly Color LabelColor = new Color(0.7f, 0.7f, 0.68f);
    private static readonly Color TitleColor = new Color(0.95f, 0.55f, 0.50f);

    // ── UI references ──
    private GameObject _canvasRoot;
    private TMP_InputField _descriptionField;
    private TMP_InputField _reproField;
    private Image[] _severityBtnImages;
    private TextMeshProUGUI _confirmText;
    private int _selectedSeverity = 1; // default: Medium

    private InputAction _toggleAction;
    private bool _isOpen;
    public bool IsOpen => _isOpen;
    private byte[] _pendingScreenshot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (string.IsNullOrEmpty(s_sessionId))
            s_sessionId = Guid.NewGuid().ToString();

        _toggleAction = new InputAction("BugReportToggle", InputActionType.Button, "<Keyboard>/f9");

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

    private void Update()
    {
        bool pressed = _toggleAction.WasPressedThisFrame() || Input.GetKeyDown(KeyCode.F9);
        if (!pressed) return;

        if (_isOpen)
            CloseForm();
        else
            StartCoroutine(CaptureAndOpen());
    }

    // ═══════════════════════════════════════
    //  Open / Close
    // ═══════════════════════════════════════

    private IEnumerator CaptureAndOpen()
    {
        if (_isOpen) yield break;

        // Capture screenshot BEFORE showing form
        yield return new WaitForEndOfFrame();

        try
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                _pendingScreenshot = tex.EncodeToJPG(75);
                Destroy(tex);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BugReportForm] Screenshot capture failed: {e.Message}");
            _pendingScreenshot = null;
        }

        _isOpen = true;
        _selectedSeverity = 1;
        RefreshSeverityButtons();
        _descriptionField.text = "";
        _reproField.text = "";
        _confirmText.text = "";

        _canvasRoot.SetActive(true);
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);

        Debug.Log("[BugReportForm] Opened.");
    }

    private void CloseForm()
    {
        _isOpen = false;
        _canvasRoot.SetActive(false);
        _pendingScreenshot = null;
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);

        Debug.Log("[BugReportForm] Closed.");
    }

    // ═══════════════════════════════════════
    //  Submit
    // ═══════════════════════════════════════

    private void OnSubmit()
    {
        string desc = _descriptionField.text;
        if (string.IsNullOrWhiteSpace(desc))
        {
            _confirmText.text = "Please describe the bug.";
            _confirmText.color = new Color(0.95f, 0.55f, 0.50f);
            return;
        }

        StartCoroutine(SubmitCoroutine());
    }

    private IEnumerator SubmitCoroutine()
    {
        _confirmText.text = "Sending...";
        _confirmText.color = LabelColor;

        string desc = _descriptionField.text;
        string repro = _reproField.text;
        string severity = SeverityLabels[_selectedSeverity];

        // Gather telemetry
        var state = GatherGameState();

        // Save locally
        SaveLocal(desc, repro, severity, state);

        // Post to Discord
        string webhookUrl = DiscordWebhookConfig.Instance != null
            ? DiscordWebhookConfig.Instance.BugReportWebhookURL
            : "";

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            var fields = new List<(string name, string value, bool inline)>
            {
                ("Severity", severity, true),
                ("Build", Application.version, true),
                ("Play Time", $"{Time.realtimeSinceStartup / 60f:F1} min", true),
            };

            if (!string.IsNullOrEmpty(state.day))
                fields.Add(("Day / Phase", state.day, true));
            if (!string.IsNullOrEmpty(state.dateChar))
                fields.Add(("Date Character", state.dateChar, true));
            if (!string.IsNullOrEmpty(repro))
                fields.Add(("Steps to Reproduce", repro, false));
            if (!string.IsNullOrEmpty(state.system))
                fields.Add(("System", state.system, false));
            if (!string.IsNullOrEmpty(state.recentErrors))
                fields.Add(("Recent Errors", $"```\n{state.recentErrors}\n```", false));

            yield return StartCoroutine(DiscordWebhookService.PostEmbed(
                webhookUrl,
                "Bug Report",
                desc,
                SeverityDiscordColors[_selectedSeverity],
                fields.ToArray(),
                $"Session: {s_sessionId?.Substring(0, 8) ?? "?"} | {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                _pendingScreenshot));
        }

        _confirmText.text = "Sent! Thank you.";
        _confirmText.color = new Color(0.4f, 0.9f, 0.5f);

        yield return new WaitForSecondsRealtime(1.5f);

        _isOpen = false;
        _canvasRoot.SetActive(false);
        _pendingScreenshot = null;
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);
    }

    // ═══════════════════════════════════════
    //  Local save
    // ═══════════════════════════════════════

    private void SaveLocal(string desc, string repro, string severity, GameState state)
    {
        string folder = Path.Combine(Application.persistentDataPath, "BugReports");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Save screenshot
        if (_pendingScreenshot != null && _pendingScreenshot.Length > 0)
        {
            string imgPath = Path.Combine(folder, $"bug_{stamp}.jpg");
            File.WriteAllBytes(imgPath, _pendingScreenshot);
        }

        // Save JSON
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"timestamp\": \"{DateTime.UtcNow:o}\",");
        sb.AppendLine($"  \"sessionId\": \"{s_sessionId}\",");
        sb.AppendLine($"  \"buildVersion\": \"{Application.version}\",");
        sb.AppendLine($"  \"severity\": \"{severity}\",");
        sb.AppendLine($"  \"description\": \"{EscapeJsonValue(desc)}\",");
        sb.AppendLine($"  \"reproSteps\": \"{EscapeJsonValue(repro)}\",");
        sb.AppendLine($"  \"playTimeSeconds\": {Time.realtimeSinceStartup:F1},");
        sb.AppendLine($"  \"gameState\": \"{EscapeJsonValue(state.day)}\",");
        sb.AppendLine($"  \"dateCharacter\": \"{EscapeJsonValue(state.dateChar)}\",");
        sb.AppendLine($"  \"system\": \"{EscapeJsonValue(state.system)}\",");
        sb.AppendLine($"  \"resolution\": \"{Screen.width}x{Screen.height}\"");
        sb.AppendLine("}");

        string jsonPath = Path.Combine(folder, $"bug_{stamp}.json");
        File.WriteAllText(jsonPath, sb.ToString());

        Debug.Log($"[BugReportForm] Saved bug report to {jsonPath}");
    }

    private static string EscapeJsonValue(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // ═══════════════════════════════════════
    //  Telemetry
    // ═══════════════════════════════════════

    private struct GameState
    {
        public string day;
        public string dateChar;
        public string system;
        public string recentErrors;
    }

    private GameState GatherGameState()
    {
        var state = new GameState();

        if (!PlaytestConsentScreen.HasConsent && PlaytestConsentScreen.WasShown)
            return state;

        // Day / Phase
        int day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : -1;
        string phase = DayPhaseManager.Instance != null
            ? DayPhaseManager.Instance.CurrentPhase.ToString()
            : "Unknown";
        state.day = day >= 0 ? $"Day {day} — {phase}" : phase;

        // Date character
        state.dateChar = DateSessionManager.Instance != null
            && DateSessionManager.Instance.CurrentDate != null
            ? DateSessionManager.Instance.CurrentDate.characterName
            : "None";

        // System
        state.system = $"{SystemInfo.operatingSystem} | {SystemInfo.graphicsDeviceName} | {SystemInfo.systemMemorySize}MB";

        // Recent errors from crash log
        state.recentErrors = ReadRecentErrors();

        return state;
    }

    private static string ReadRecentErrors()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "iris_crashlog.txt");
            if (!File.Exists(path)) return "";

            var lines = File.ReadAllLines(path);
            int start = Mathf.Max(0, lines.Length - 15);
            var sb = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
                sb.AppendLine(lines[i]);

            string result = sb.ToString().Trim();
            if (result.Length > 900)
                result = result.Substring(result.Length - 900);
            return result;
        }
        catch
        {
            return "";
        }
    }

    // ═══════════════════════════════════════
    //  Severity buttons
    // ═══════════════════════════════════════

    private void SelectSeverity(int index)
    {
        _selectedSeverity = index;
        RefreshSeverityButtons();
    }

    private void RefreshSeverityButtons()
    {
        for (int i = 0; i < SeverityLabels.Length; i++)
        {
            _severityBtnImages[i].color = (i == _selectedSeverity)
                ? SeverityColors[i]
                : new Color(0.25f, 0.25f, 0.25f);
        }
    }

    // ═══════════════════════════════════════
    //  Runtime UI construction
    // ═══════════════════════════════════════

    private void BuildUI()
    {
        // ── Canvas ──
        _canvasRoot = new GameObject("BugReportCanvas");
        _canvasRoot.transform.SetParent(transform, false);

        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasRoot.AddComponent<GraphicRaycaster>();

        // ── Dim background ──
        var dimBg = MakeChild(_canvasRoot, "DimBg");
        var dimRT = dimBg.AddComponent<RectTransform>();
        StretchFill(dimRT);
        dimBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // ── Panel ──
        var panel = MakeChild(_canvasRoot, "Panel");
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(660f, 520f);
        panel.AddComponent<Image>().color = PanelBg;

        float y = -20f;

        // ── Title ──
        y = AddLabel(panel, "Title", "Bug Report", 28f, y, 40f, TitleColor, TextAlignmentOptions.Center);

        y -= 5f;
        y = AddLabel(panel, "ScreenshotNote", "Screenshot captured automatically", 14f, y, 20f,
            new Color(0.5f, 0.5f, 0.48f), TextAlignmentOptions.Center);

        // ── Severity row ──
        y -= 10f;
        y = AddLabel(panel, "SevLabel", "Severity:", 17f, y, 24f, LabelColor, TextAlignmentOptions.Left);
        y -= 5f;

        _severityBtnImages = new Image[SeverityLabels.Length];
        float btnW = 120f;
        float spacing = 130f;
        float totalW = spacing * (SeverityLabels.Length - 1);
        float startX = -totalW / 2f;

        for (int i = 0; i < SeverityLabels.Length; i++)
        {
            int idx = i; // capture for closure
            var btnGO = MakeChild(panel, $"SevBtn_{i}");
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 1f);
            btnRT.anchorMax = new Vector2(0.5f, 1f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(startX + i * spacing, y - 18f);
            btnRT.sizeDelta = new Vector2(btnW, 36f);

            var img = btnGO.AddComponent<Image>();
            _severityBtnImages[i] = img;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SelectSeverity(idx));

            var txtGO = MakeChild(btnGO, "Label");
            var txtRT = txtGO.AddComponent<RectTransform>();
            StretchFill(txtRT);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = SeverityLabels[i];
            tmp.fontSize = 17f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }
        y -= 45f;

        // ── Description field ──
        y -= 8f;
        y = AddLabel(panel, "DescLabel", "What happened?", 17f, y, 24f, LabelColor, TextAlignmentOptions.Left);
        _descriptionField = AddInputField(panel, "DescField", y, 120f, "Describe the bug...");
        y -= 130f;

        // ── Repro steps field ──
        y -= 5f;
        y = AddLabel(panel, "ReproLabel", "Steps to reproduce (optional):", 17f, y, 24f, LabelColor, TextAlignmentOptions.Left);
        _reproField = AddInputField(panel, "ReproField", y, 70f, "1. Did X  2. Then Y  3. Bug occurred");
        y -= 80f;

        // ── Confirm text ──
        var confirmGO = MakeChild(panel, "ConfirmText");
        var confirmRT = confirmGO.AddComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.5f, 1f);
        confirmRT.anchorMax = new Vector2(0.5f, 1f);
        confirmRT.pivot = new Vector2(0.5f, 1f);
        confirmRT.anchoredPosition = new Vector2(0f, y);
        confirmRT.sizeDelta = new Vector2(600f, 25f);
        _confirmText = confirmGO.AddComponent<TextMeshProUGUI>();
        _confirmText.text = "";
        _confirmText.fontSize = 16f;
        _confirmText.alignment = TextAlignmentOptions.Center;
        y -= 30f;

        // ── Buttons ──
        var submitBtn = AddButton(panel, "SubmitBtn", "Submit", -90f, y - 10f, 160f, 42f, BtnSubmit);
        submitBtn.onClick.AddListener(OnSubmit);

        var closeBtn = AddButton(panel, "CloseBtn", "Close (F9)", 90f, y - 10f, 160f, 42f, BtnClose);
        closeBtn.onClick.AddListener(CloseForm);
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

        fieldGO.AddComponent<Image>().color = FieldBg;

        var textArea = MakeChild(fieldGO, "TextArea");
        var textAreaRT = textArea.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = new Vector2(10f, 5f);
        textAreaRT.offsetMax = new Vector2(-10f, -5f);
        textArea.AddComponent<RectMask2D>();

        var inputTextGO = MakeChild(textArea, "Text");
        var inputTextRT = inputTextGO.AddComponent<RectTransform>();
        StretchFill(inputTextRT);
        var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 16f;
        inputTMP.color = new Color(0.9f, 0.9f, 0.88f);
        inputTMP.textWrappingMode = TextWrappingModes.Normal;

        var phGO = MakeChild(textArea, "Placeholder");
        var phRT = phGO.AddComponent<RectTransform>();
        StretchFill(phRT);
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 16f;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.5f, 0.5f, 0.48f);
        phTMP.textWrappingMode = TextWrappingModes.Normal;

        var inputField = fieldGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = phTMP;
        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        inputField.characterLimit = 1000;
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
        btnRT.anchoredPosition = new Vector2(x, yPos);
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
        tmp.fontSize = 19f;
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
