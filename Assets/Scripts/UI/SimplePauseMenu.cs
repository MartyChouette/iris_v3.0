using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped singleton pause menu. ESC toggles pause state.
/// Uses TimeScaleManager for clean priority-based time control.
/// </summary>
public class SimplePauseMenu : MonoBehaviour
{
    public static SimplePauseMenu Instance { get; private set; }

    [SerializeField] private GameObject _pauseRoot;
    [SerializeField] private SettingsPanel _settingsPanel;

    private InputAction _pauseAction;
    private bool _isPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SimplePauseMenu] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");

        if (_pauseRoot != null)
            _pauseRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _pauseAction.Enable();
    }

    private void OnDisable()
    {
        _pauseAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Check both InputAction and legacy Input as fallback —
        // InputAction.WasPressedThisFrame can fail at timeScale 0
        // if Input System is in FixedUpdate processing mode.
        bool pressed = _pauseAction.WasPressedThisFrame()
                    || Input.GetKeyDown(KeyCode.Escape);

        if (pressed)
        {
            // If settings panel is open, go back to pause menu first
            if (_isPaused && _settingsPanel != null && _settingsPanel.IsOpen)
            {
                CloseSettings();
                return;
            }

            if (_isPaused)
                Resume();
            else
                Pause();
        }
    }

    private void Pause()
    {
        _isPaused = true;
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);

        if (_pauseRoot != null)
            _pauseRoot.SetActive(true);

        Debug.Log("[SimplePauseMenu] Paused.");
    }

    public void Resume()
    {
        _isPaused = false;
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);

        // Close settings panel if it was open
        if (_settingsPanel != null && _settingsPanel.IsOpen)
            _settingsPanel.Close();

        if (_pauseRoot != null)
            _pauseRoot.SetActive(false);
    }

    /// <summary>Opens the settings panel and hides the pause menu buttons.</summary>
    public void OpenSettings()
    {
        if (_settingsPanel == null) return;

        _settingsPanel.Open();
        if (_pauseRoot != null)
            _pauseRoot.SetActive(false);
    }

    /// <summary>Called by SettingsPanel close button — returns to pause menu buttons.</summary>
    public void CloseSettings()
    {
        if (_settingsPanel != null && _settingsPanel.IsOpen)
            _settingsPanel.Close();

        if (_pauseRoot != null)
            _pauseRoot.SetActive(true);
    }

    public void QuitToMenu()
    {
        if (PlaytestFeedbackForm.Instance != null && !PlaytestFeedbackForm.Instance.IsOpen)
        {
            if (_pauseRoot != null) _pauseRoot.SetActive(false);
            PlaytestFeedbackForm.Instance.OpenWithCallback(DoQuitToMenu);
            return;
        }
        DoQuitToMenu();
    }

    private void DoQuitToMenu()
    {
        _isPaused = false;
        TimeScaleManager.ClearAll();
        SceneManager.LoadScene(0);
    }

    public void QuitToDesktop()
    {
        if (PlaytestFeedbackForm.Instance != null && !PlaytestFeedbackForm.Instance.IsOpen)
        {
            if (_pauseRoot != null) _pauseRoot.SetActive(false);
            PlaytestFeedbackForm.Instance.OpenWithCallback(DoQuitToDesktop);
            return;
        }
        DoQuitToDesktop();
    }

    private void DoQuitToDesktop()
    {
        Debug.Log("[SimplePauseMenu] Quitting application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
