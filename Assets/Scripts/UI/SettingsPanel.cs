using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings panel controller. Manages tabbed sections and binds UI controls
/// to AccessibilitySettings. Opened from pause menu or main menu.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The panel root to show/hide.")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Tabs")]
    [SerializeField] private SettingsTabButton[] _tabButtons;
    [SerializeField] private GameObject[] _tabPanels;

    // ── Visual Tab ──────────────────────────────────────────────
    [Header("Visual")]
    [SerializeField] private TMP_Dropdown _colorblindDropdown;
    [SerializeField] private Toggle _highContrastToggle;
    [SerializeField] private Slider _textScaleSlider;
    [SerializeField] private TMP_Text _textScaleLabel;

    // ── Audio Tab ───────────────────────────────────────────────
    [Header("Audio")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Slider _ambienceVolumeSlider;
    [SerializeField] private Slider _uiVolumeSlider;
    [SerializeField] private Toggle _captionsToggle;

    // ── Motion Tab ──────────────────────────────────────────────
    [Header("Motion")]
    [SerializeField] private Toggle _reduceMotionToggle;
    [SerializeField] private Slider _screenShakeSlider;
    [SerializeField] private TMP_Text _screenShakeLabel;

    // ── Timing Tab ──────────────────────────────────────────────
    [Header("Timing")]
    [SerializeField] private TMP_Dropdown _timerDropdown;

    // ── Performance Tab ─────────────────────────────────────────
    [Header("Performance")]
    [SerializeField] private Slider _resolutionScaleSlider;
    [SerializeField] private TMP_Text _resolutionScaleLabel;
    [SerializeField] private TMP_Dropdown _qualityDropdown;
    [SerializeField] private Toggle _psxToggle;

    private int _currentTab;
    private bool _isOpen;
    private bool _suppressCallbacks;

    public bool IsOpen => _isOpen;

    private void Start()
    {
        // Wire up listeners
        if (_colorblindDropdown != null) _colorblindDropdown.onValueChanged.AddListener(OnColorblindChanged);
        if (_highContrastToggle != null) _highContrastToggle.onValueChanged.AddListener(OnHighContrastChanged);
        if (_textScaleSlider != null)    _textScaleSlider.onValueChanged.AddListener(OnTextScaleChanged);
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (_musicVolumeSlider != null)  _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (_sfxVolumeSlider != null)    _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        if (_ambienceVolumeSlider != null) _ambienceVolumeSlider.onValueChanged.AddListener(OnAmbienceVolumeChanged);
        if (_uiVolumeSlider != null)     _uiVolumeSlider.onValueChanged.AddListener(OnUIVolumeChanged);
        if (_captionsToggle != null)     _captionsToggle.onValueChanged.AddListener(OnCaptionsChanged);
        if (_reduceMotionToggle != null) _reduceMotionToggle.onValueChanged.AddListener(OnReduceMotionChanged);
        if (_screenShakeSlider != null)  _screenShakeSlider.onValueChanged.AddListener(OnScreenShakeChanged);
        if (_timerDropdown != null)      _timerDropdown.onValueChanged.AddListener(OnTimerChanged);
        if (_resolutionScaleSlider != null) _resolutionScaleSlider.onValueChanged.AddListener(OnResolutionScaleChanged);
        if (_qualityDropdown != null)    _qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        if (_psxToggle != null)          _psxToggle.onValueChanged.AddListener(OnPSXChanged);

        // Initialize tab buttons
        if (_tabButtons != null)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                    _tabButtons[i].Initialize(this, i);
            }
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    // ── Open / Close ────────────────────────────────────────────

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        ReadFromSettings();
        SwitchTab(0);

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        Debug.Log("[SettingsPanel] Opened.");
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        Debug.Log("[SettingsPanel] Closed.");
    }

    /// <summary>Called by the Close button. Routes through pause menu to restore buttons.</summary>
    public void UI_Close()
    {
        if (SimplePauseMenu.Instance != null)
            SimplePauseMenu.Instance.CloseSettings();
        else
            Close();
    }

    // ── Tabs ────────────────────────────────────────────────────

    public void SwitchTab(int index)
    {
        _currentTab = index;

        if (_tabPanels != null)
        {
            for (int i = 0; i < _tabPanels.Length; i++)
            {
                if (_tabPanels[i] != null)
                    _tabPanels[i].SetActive(i == index);
            }
        }

        if (_tabButtons != null)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                    _tabButtons[i].SetActive(i == index);
            }
        }
    }

    // ── Read current values into UI controls ────────────────────

    private void ReadFromSettings()
    {
        _suppressCallbacks = true;

        // Visual
        if (_colorblindDropdown != null) _colorblindDropdown.value = (int)AccessibilitySettings.CurrentColorblindMode;
        if (_highContrastToggle != null) _highContrastToggle.isOn = AccessibilitySettings.HighContrast;
        if (_textScaleSlider != null)    _textScaleSlider.value = AccessibilitySettings.TextScale;
        UpdateTextScaleLabel();

        // Audio
        if (_masterVolumeSlider != null) _masterVolumeSlider.value = AccessibilitySettings.MasterVolume;
        if (_musicVolumeSlider != null)  _musicVolumeSlider.value = AccessibilitySettings.MusicVolume;
        if (_sfxVolumeSlider != null)    _sfxVolumeSlider.value = AccessibilitySettings.SFXVolume;
        if (_ambienceVolumeSlider != null) _ambienceVolumeSlider.value = AccessibilitySettings.AmbienceVolume;
        if (_uiVolumeSlider != null)     _uiVolumeSlider.value = AccessibilitySettings.UIVolume;
        if (_captionsToggle != null)     _captionsToggle.isOn = AccessibilitySettings.CaptionsEnabled;

        // Motion
        if (_reduceMotionToggle != null) _reduceMotionToggle.isOn = AccessibilitySettings.ReduceMotion;
        if (_screenShakeSlider != null)  _screenShakeSlider.value = AccessibilitySettings.ScreenShakeScale;
        UpdateScreenShakeLabel();

        // Timing
        if (_timerDropdown != null) _timerDropdown.value = TimerMultiplierToIndex(AccessibilitySettings.TimerMultiplier);

        // Performance
        if (_resolutionScaleSlider != null) _resolutionScaleSlider.value = AccessibilitySettings.ResolutionScale;
        UpdateResolutionScaleLabel();
        if (_qualityDropdown != null) _qualityDropdown.value = Mathf.Max(0, AccessibilitySettings.QualityPreset);
        if (_psxToggle != null) _psxToggle.isOn = AccessibilitySettings.PSXEnabled;

        _suppressCallbacks = false;
    }

    // ── UI Callbacks ────────────────────────────────────────────

    private void OnColorblindChanged(int val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.CurrentColorblindMode = (AccessibilitySettings.ColorblindMode)val;
    }

    private void OnHighContrastChanged(bool val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.HighContrast = val;
    }

    private void OnTextScaleChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.TextScale = val;
        UpdateTextScaleLabel();
    }

    private void OnMasterVolumeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.MasterVolume = val;
    }

    private void OnMusicVolumeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.MusicVolume = val;
    }

    private void OnSFXVolumeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.SFXVolume = val;
    }

    private void OnAmbienceVolumeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.AmbienceVolume = val;
    }

    private void OnUIVolumeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.UIVolume = val;
    }

    private void OnCaptionsChanged(bool val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.CaptionsEnabled = val;
    }

    private void OnReduceMotionChanged(bool val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.ReduceMotion = val;
    }

    private void OnScreenShakeChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.ScreenShakeScale = val;
        UpdateScreenShakeLabel();
    }

    private void OnTimerChanged(int val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.TimerMultiplier = IndexToTimerMultiplier(val);
    }

    private void OnResolutionScaleChanged(float val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.ResolutionScale = val;
        UpdateResolutionScaleLabel();
    }

    private void OnQualityChanged(int val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.QualityPreset = val;
    }

    private void OnPSXChanged(bool val)
    {
        if (_suppressCallbacks) return;
        AccessibilitySettings.PSXEnabled = val;
    }

    // ── Reset Buttons ───────────────────────────────────────────

    public void UI_ResetAll()
    {
        AccessibilitySettings.ResetAll();
        ReadFromSettings();
    }

    // ── Timer index mapping ─────────────────────────────────────
    // 0 = Normal (1.0), 1 = Relaxed (1.5), 2 = Extended (2.0), 3 = No Timer (0)

    private static float IndexToTimerMultiplier(int index) => index switch
    {
        0 => 1.0f,
        1 => 1.5f,
        2 => 2.0f,
        3 => 0f,
        _ => 1.0f
    };

    private static int TimerMultiplierToIndex(float mult)
    {
        if (mult <= 0f)    return 3;
        if (mult <= 1.25f) return 0;
        if (mult <= 1.75f) return 1;
        return 2;
    }

    // ── Label helpers ───────────────────────────────────────────

    private void UpdateTextScaleLabel()
    {
        if (_textScaleLabel != null)
            _textScaleLabel.text = $"{AccessibilitySettings.TextScale:P0}";
    }

    private void UpdateScreenShakeLabel()
    {
        if (_screenShakeLabel != null)
            _screenShakeLabel.text = $"{AccessibilitySettings.ScreenShakeScale:P0}";
    }

    private void UpdateResolutionScaleLabel()
    {
        if (_resolutionScaleLabel != null)
            _resolutionScaleLabel.text = $"{AccessibilitySettings.ResolutionScale:P0}";
    }
}
