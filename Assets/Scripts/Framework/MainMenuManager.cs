using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main menu controller with three panels:
/// ModeSelect → GamePanel → SaveSlots.
///
/// Static ActiveConfig persists across scene loads so GameClock / DayPhaseManager
/// can read the selected mode's pacing values on Start().
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── Static state (survives scene load) ───────────────────────
    public static GameModeConfig ActiveConfig { get; private set; }

    private enum MenuState { ModeSelect, GamePanel, SaveSlots }

    // ── Scene ────────────────────────────────────────────────────
    [Header("Scene")]
    [Tooltip("Build index of the apartment scene. -1 = next scene after this one.")]
    [SerializeField] private int _apartmentSceneIndex = -1;

    [Header("Fade")]
    [Tooltip("Fade duration before loading. 0 = instant.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    // ── Game Mode Configs ────────────────────────────────────────
    [Header("Game Mode Configs")]
    [SerializeField] private GameModeConfig _demoConfig;
    [SerializeField] private GameModeConfig _showcaseConfig;
    [SerializeField] private GameModeConfig _fullConfig;

    // ── Panels ───────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject _modeSelectPanel;
    [SerializeField] private GameObject _gamePanel;
    [SerializeField] private GameObject _saveSlotPanel;

    // ── Mode Select Buttons ──────────────────────────────────────
    [Header("Mode Select")]
    [SerializeField] private Button _demoButton;
    [SerializeField] private Button _showcaseButton;
    [SerializeField] private Button _fullButton;

    // ── Game Panel ───────────────────────────────────────────────
    [Header("Game Panel")]
    [SerializeField] private TMP_Text _modeNameLabel;
    [SerializeField] private TMP_Text _modeDescLabel;
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _loadSaveButton;
    [SerializeField] private Button _gamePanelBackButton;
    [SerializeField] private Button _quitButton;

    // ── Save Slots ───────────────────────────────────────────────
    [Header("Save Slots")]
    [SerializeField] private Button _slot0Button;
    [SerializeField] private Button _slot1Button;
    [SerializeField] private Button _slot2Button;
    [SerializeField] private TMP_Text _slot0Label;
    [SerializeField] private TMP_Text _slot1Label;
    [SerializeField] private TMP_Text _slot2Label;
    [SerializeField] private Button _saveSlotBackButton;

    // ── Tutorial ─────────────────────────────────────────────────
    [Header("Tutorial")]
    [SerializeField] private TutorialCard _tutorialCard;

    // ── Runtime ──────────────────────────────────────────────────
    private MenuState _state;
    private GameModeConfig _selectedConfig;
    private bool _loading;
    private InputAction _escapeAction;
    private bool _quitConfirmShowing;
    private GameObject _quitConfirmPanel;

    // ═══════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _escapeAction = new InputAction("MenuEscape", InputActionType.Button, "<Keyboard>/escape");
    }

    private void OnEnable() => _escapeAction.Enable();
    private void OnDisable() => _escapeAction.Disable();

    private void OnDestroy() => _escapeAction?.Dispose();

    private void Start()
    {
        // Wire button listeners at runtime
        if (_demoButton != null) _demoButton.onClick.AddListener(OnDemoClicked);
        if (_showcaseButton != null) _showcaseButton.onClick.AddListener(OnShowcaseClicked);
        if (_fullButton != null) _fullButton.onClick.AddListener(OnFullClicked);

        if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGame);
        if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
        if (_loadSaveButton != null) _loadSaveButton.onClick.AddListener(OnLoadSave);
        if (_gamePanelBackButton != null) _gamePanelBackButton.onClick.AddListener(OnGamePanelBack);
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);

        if (_slot0Button != null) _slot0Button.onClick.AddListener(() => OnSlotClicked(0));
        if (_slot1Button != null) _slot1Button.onClick.AddListener(() => OnSlotClicked(1));
        if (_slot2Button != null) _slot2Button.onClick.AddListener(() => OnSlotClicked(2));
        if (_saveSlotBackButton != null) _saveSlotBackButton.onClick.AddListener(OnSaveSlotBack);

        BuildQuitConfirmPanel();

        ShowPanel(MenuState.ModeSelect);

        // Fade in from black
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.FadeIn(_fadeDuration);
    }

    private void Update()
    {
        bool pressed = _escapeAction.WasPressedThisFrame() || Input.GetKeyDown(KeyCode.Escape);
        if (!pressed || _loading) return;

        if (_quitConfirmShowing)
        {
            HideQuitConfirm();
            return;
        }

        switch (_state)
        {
            case MenuState.ModeSelect:
                ShowQuitConfirm();
                break;
            case MenuState.GamePanel:
                OnGamePanelBack();
                break;
            case MenuState.SaveSlots:
                OnSaveSlotBack();
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Panel management
    // ═══════════════════════════════════════════════════════════════

    private void ShowPanel(MenuState state)
    {
        _state = state;
        if (_modeSelectPanel != null) _modeSelectPanel.SetActive(state == MenuState.ModeSelect);
        if (_gamePanel != null) _gamePanel.SetActive(state == MenuState.GamePanel);
        if (_saveSlotPanel != null) _saveSlotPanel.SetActive(state == MenuState.SaveSlots);
    }

    // ═══════════════════════════════════════════════════════════════
    // Mode Select
    // ═══════════════════════════════════════════════════════════════

    public void OnDemoClicked() => SelectMode(_demoConfig);
    public void OnShowcaseClicked() => SelectMode(_showcaseConfig);
    public void OnFullClicked() => SelectMode(_fullConfig);

    private void SelectMode(GameModeConfig config)
    {
        if (config == null) return;
        _selectedConfig = config;

        // Update game panel labels
        if (_modeNameLabel != null) _modeNameLabel.text = config.modeName;
        if (_modeDescLabel != null) _modeDescLabel.text = config.modeDescription;

        // Continue button — visible only if a matching save exists
        int continueSlot = FindMostRecentSlot(config.modeName);
        if (_continueButton != null)
            _continueButton.gameObject.SetActive(continueSlot >= 0);

        ShowPanel(MenuState.GamePanel);
    }

    // ═══════════════════════════════════════════════════════════════
    // Game Panel
    // ═══════════════════════════════════════════════════════════════

    public void OnNewGame()
    {
        if (_loading) return;

        ActiveConfig = _selectedConfig;

        // Find first empty slot
        int slot = FindFirstEmptySlot();
        if (slot < 0) slot = 0; // overwrite slot 0 if all full
        SaveManager.ActiveSlot = slot;

        // Delete any existing save in this slot so NameEntryScreen starts fresh
        SaveManager.DeleteSlot(slot);

        // Clear all static registries to prevent stale state from previous games
        DateHistory.LoadFrom(null);
        ItemStateRegistry.Clear();
        PlayerData.PlayerName = "Nema";

        // Show consent screen, then tutorial, then load
        PlaytestConsentScreen.ShowIfNeeded(() =>
        {
            if (_tutorialCard != null)
                _tutorialCard.Show(() => LoadApartment());
            else
                LoadApartment();
        });
    }

    public void OnContinue()
    {
        if (_loading || _selectedConfig == null) return;

        int slot = FindMostRecentSlot(_selectedConfig.modeName);
        if (slot < 0) return;

        ActiveConfig = _selectedConfig;
        SaveManager.ActiveSlot = slot;
        PlaytestConsentScreen.ShowIfNeeded(() => LoadApartment());
    }

    public void OnLoadSave()
    {
        RefreshSlotLabels();
        ShowPanel(MenuState.SaveSlots);
    }

    public void OnGamePanelBack()
    {
        ShowPanel(MenuState.ModeSelect);
    }

    public void QuitGame()
    {
        ShowQuitConfirm();
    }

    private void ShowQuitConfirm()
    {
        _quitConfirmShowing = true;
        if (_quitConfirmPanel != null)
            _quitConfirmPanel.SetActive(true);
    }

    private void HideQuitConfirm()
    {
        _quitConfirmShowing = false;
        if (_quitConfirmPanel != null)
            _quitConfirmPanel.SetActive(false);
    }

    private void DoQuitToDesktop()
    {
        Debug.Log("[MainMenuManager] Quitting application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ═══════════════════════════════════════════════════════════════
    // Save Slots
    // ═══════════════════════════════════════════════════════════════

    private void RefreshSlotLabels()
    {
        TMP_Text[] labels = { _slot0Label, _slot1Label, _slot2Label };
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (labels[i] == null) continue;

            var data = SaveManager.PeekSlot(i);
            if (data != null)
            {
                string mode = string.IsNullOrEmpty(data.gameModeName) ? "Unknown" : data.gameModeName;
                labels[i].text = $"Slot {i + 1}: {mode} \u2014 Day {data.currentDay}";
            }
            else
            {
                labels[i].text = $"Slot {i + 1}: Empty";
            }
        }
    }

    public void OnSlotClicked(int slot)
    {
        if (_loading) return;
        if (!SaveManager.HasSave(slot)) return;

        ActiveConfig = _selectedConfig;
        SaveManager.ActiveSlot = slot;
        PlaytestConsentScreen.ShowIfNeeded(() => LoadApartment());
    }

    public void OnSaveSlotBack()
    {
        ShowPanel(MenuState.GamePanel);
    }

    // ═══════════════════════════════════════════════════════════════
    // Quit Confirm Panel (built at runtime)
    // ═══════════════════════════════════════════════════════════════

    private void BuildQuitConfirmPanel()
    {
        // Overlay canvas so it sits above everything
        _quitConfirmPanel = new GameObject("QuitConfirmCanvas");
        _quitConfirmPanel.transform.SetParent(transform, false);

        var canvas = _quitConfirmPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = _quitConfirmPanel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _quitConfirmPanel.AddComponent<GraphicRaycaster>();

        // Dim bg
        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(_quitConfirmPanel.transform, false);
        var dimRT = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.sizeDelta = Vector2.zero;
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);

        // Panel
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(_quitConfirmPanel.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(500f, 200f);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(panelGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.5f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.sizeDelta = Vector2.zero;
        labelRT.anchoredPosition = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Quit to desktop?";
        labelTMP.fontSize = 28f;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color = new Color(0.95f, 0.92f, 0.85f);

        // Yes button
        MakeQuitConfirmButton(panelGO.transform, "Yes", new Vector2(-80f, -50f), DoQuitToDesktop);

        // No button
        MakeQuitConfirmButton(panelGO.transform, "No", new Vector2(80f, -50f), HideQuitConfirm);

        _quitConfirmPanel.SetActive(false);
    }

    private void MakeQuitConfirmButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject($"Btn_{label}");
        btnGO.transform.SetParent(parent, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = pos;
        btnRT.sizeDelta = new Vector2(140f, 45f);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.22f, 0.22f, 0.26f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(btnGO.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    // ═══════════════════════════════════════════════════════════════
    // Scene loading
    // ═══════════════════════════════════════════════════════════════

    private void LoadApartment()
    {
        if (_loading) return;
        _loading = true;

        int targetIndex = _apartmentSceneIndex >= 0
            ? _apartmentSceneIndex
            : SceneManager.GetActiveScene().buildIndex + 1;

        if (ScreenFade.Instance != null && _fadeDuration > 0f)
            StartCoroutine(FadeAndLoad(targetIndex));
        else
            SceneManager.LoadScene(targetIndex);
    }

    private IEnumerator FadeAndLoad(int sceneIndex)
    {
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);
        SceneManager.LoadScene(sceneIndex);
    }

    // ═══════════════════════════════════════════════════════════════
    // Slot helpers
    // ═══════════════════════════════════════════════════════════════

    private static int FindFirstEmptySlot()
    {
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (!SaveManager.HasSave(i)) return i;
        }
        return -1;
    }

    private static int FindMostRecentSlot(string modeName)
    {
        int bestSlot = -1;
        int bestDay = -1;

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            var data = SaveManager.PeekSlot(i);
            if (data == null) continue;
            if (data.gameModeName != modeName) continue;

            if (data.currentDay > bestDay)
            {
                bestDay = data.currentDay;
                bestSlot = i;
            }
        }

        return bestSlot;
    }
}
