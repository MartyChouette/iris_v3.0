using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// F3 debug overlay for the apartment scene. Shows game mode, FPS, day/phase info,
/// date session stats, affection, date history, and system state.
/// Matches the FlowerHUD_DebugTelemetry style.
/// </summary>
[DisallowMultipleComponent]
public class ApartmentDebugHUD : MonoBehaviour
{
    public enum GameMode { ShortDemo, ShowDemo, Full }

    [Header("UI")]
    [Tooltip("CanvasGroup for fade toggle.")]
    public CanvasGroup root;

    [Tooltip("TMP label that receives the debug text.")]
    public TMP_Text telemetryLabel;

    [Header("Behavior")]
    public bool startVisible = false;
    public KeyCode toggleKey = KeyCode.F3;

    [Tooltip("Seconds between text rebuilds.")]
    public float updateInterval = 0.15f;

    [Header("Game Mode")]
    [Tooltip("Current play mode — shown in the readout.")]
    public GameMode gameMode = GameMode.Full;

    // FPS tracking
    private float[] _fpsSamples;
    private int _fpsIndex;
    private int _fpsSampleCount;
    private const int FpsSampleSize = 60;

    private float _timer;
    private readonly StringBuilder _sb = new StringBuilder(1024);

    private void Awake()
    {
        if (root == null) root = GetComponentInChildren<CanvasGroup>(true);
        _fpsSamples = new float[FpsSampleSize];
        SetVisible(startVisible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetVisible(!IsVisible());

        // Always track FPS (even when hidden, so average is ready)
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            _fpsSamples[_fpsIndex] = 1f / dt;
            _fpsIndex = (_fpsIndex + 1) % FpsSampleSize;
            if (_fpsSampleCount < FpsSampleSize) _fpsSampleCount++;
        }

        if (!IsVisible()) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        BuildTelemetry();
    }

    private void BuildTelemetry()
    {
        _sb.Clear();

        // ── Header ──
        _sb.AppendLine("<b>APARTMENT DEBUG</b>  [F3 toggle]");
        _sb.AppendLine();

        // ── Mode ──
        string modeName = gameMode switch
        {
            GameMode.ShortDemo => "SHORT DEMO",
            GameMode.ShowDemo => "SHOW DEMO",
            GameMode.Full => "FULL",
            _ => "???"
        };
        _sb.AppendLine($"Mode: {modeName}");

        // ── FPS ──
        float avgFps = 0f;
        if (_fpsSampleCount > 0)
        {
            float sum = 0f;
            for (int i = 0; i < _fpsSampleCount; i++)
                sum += _fpsSamples[i];
            avgFps = sum / _fpsSampleCount;
        }
        _sb.AppendLine($"FPS: {avgFps:F1} avg ({1f / Time.unscaledDeltaTime:F0} now)");
        _sb.AppendLine();

        // ── Clock / Day ──
        var clock = GameClock.Instance;
        if (clock != null)
        {
            float hour = Mathf.Repeat(clock.CurrentHour, 24f);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            _sb.AppendLine($"Day {clock.CurrentDay}  {h:D2}:{m:D2}");
        }
        else
        {
            _sb.AppendLine("Day --  --:--");
        }

        // ── Day Phase ──
        var dpm = DayPhaseManager.Instance;
        if (dpm != null)
        {
            _sb.AppendLine($"Phase: {dpm.CurrentPhase}");
        }

        // ── Apartment ──
        var apt = ApartmentManager.Instance;
        if (apt != null)
        {
            _sb.AppendLine($"Area: {apt.CurrentAreaIndex} ({apt.CurrentState})");
        }

        // ── Camera Preset ──
        var camTest = FindFirstObjectByType<CameraTestController>();
        if (camTest != null && camTest.ActivePresetIndex >= 0)
        {
            _sb.AppendLine($"Preset: V{camTest.ActivePresetIndex + 1}");
        }

        _sb.AppendLine();

        // ── Date Session ──
        var dsm = DateSessionManager.Instance;
        if (dsm != null)
        {
            _sb.AppendLine($"<b>DATE SESSION</b>");
            _sb.AppendLine($"  State: {dsm.CurrentState}");

            if (dsm.CurrentDate != null)
            {
                _sb.AppendLine($"  Date: {dsm.CurrentDate.characterName}");
                _sb.AppendLine($"  Phase: {dsm.CurrentDatePhase}");
                _sb.AppendLine($"  Affection: {dsm.Affection:F1}/100");
            }
            else
            {
                _sb.AppendLine("  Date: (none)");
            }
        }

        // ── Phone ──
        var phone = PhoneController.Instance;
        if (phone != null)
        {
            _sb.AppendLine($"  Phone: {phone.CurrentPhoneState}");
        }

        // ── Mood ──
        var mood = MoodMachine.Instance;
        if (mood != null)
        {
            _sb.AppendLine($"  Mood: {mood.Mood:F2}");
        }

        _sb.AppendLine();

        // ── Date History ──
        var history = DateHistory.Entries;
        if (history != null && history.Count > 0)
        {
            _sb.AppendLine("<b>DATE HISTORY</b>");
            foreach (var entry in history)
            {
                _sb.AppendLine($"  Day {entry.day}: {entry.characterName} — {entry.grade} ({entry.finalAffection:F0}%)");
            }
        }
        else
        {
            _sb.AppendLine("Date History: (none)");
        }

        telemetryLabel.text = _sb.ToString();
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.alpha = visible ? 1f : 0f;
            root.interactable = visible;
            root.blocksRaycasts = false; // never block game input
        }
        else if (telemetryLabel != null)
        {
            telemetryLabel.gameObject.SetActive(visible);
        }
    }

    public bool IsVisible()
    {
        if (root != null) return root.alpha > 0.5f;
        if (telemetryLabel != null) return telemetryLabel.gameObject.activeSelf;
        return false;
    }
}
