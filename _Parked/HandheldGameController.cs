using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Handheld game device in the apartment. Click to pick up → opens game overlay.
/// Simple flower-themed catch mini-game. Sets MoodMachine source while playing.
/// Art is swappable (colored rectangles for prototype).
/// </summary>
public class HandheldGameController : MonoBehaviour
{
    public static HandheldGameController Instance { get; private set; }

    public enum GameState { Idle, Playing }

    [Header("Game Definition")]
    [Tooltip("Current game cartridge.")]
    [SerializeField] private HandheldGameDefinition _gameDef;

    [Header("UI References")]
    [Tooltip("Root canvas for the game overlay (screen-space).")]
    [SerializeField] private GameObject _overlayRoot;

    [Tooltip("Score text display.")]
    [SerializeField] private TMP_Text _scoreText;

    [Header("Game Settings")]
    [Tooltip("Width of the play area in screen units.")]
    [SerializeField] private float _playAreaWidth = 400f;

    [Tooltip("Petal fall speed in units per second.")]
    [SerializeField] private float _petalFallSpeed = 200f;

    [Tooltip("Player move speed in units per second.")]
    [SerializeField] private float _playerMoveSpeed = 300f;

    [Tooltip("Seconds between petal spawns.")]
    [SerializeField] private float _spawnInterval = 0.8f;

    [Header("Audio")]
    [SerializeField] private AudioClip _catchSFX;
    [SerializeField] private AudioClip _missSFX;

    private InputAction _moveAction;
    private InputAction _escapeAction;

    private GameState _state = GameState.Idle;
    private int _score;
    private float _spawnTimer;
    private float _playerX;
    private List<RectTransform> _activePetals = new List<RectTransform>();
    private RectTransform _playerRect;
    private RectTransform _playArea;

    public GameState CurrentState => _state;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HandheldGameController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _moveAction = new InputAction("GameMove", InputActionType.Value, "<Keyboard>/a");
        _moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        _escapeAction = new InputAction("GameEscape", InputActionType.Button, "<Keyboard>/escape");

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _moveAction.Enable();
        _escapeAction.Enable();
    }

    private void OnDisable()
    {
        _moveAction.Disable();
        _escapeAction.Disable();
    }

    private void OnDestroy()
    {
        _moveAction?.Dispose();
        _escapeAction?.Dispose();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_state != GameState.Playing) return;

        // Escape to stop playing
        if (_escapeAction.WasPressedThisFrame())
        {
            StopPlaying();
            return;
        }

        // Move player
        float moveInput = _moveAction.ReadValue<float>();
        _playerX += moveInput * _playerMoveSpeed * Time.deltaTime;
        _playerX = Mathf.Clamp(_playerX, -_playAreaWidth * 0.5f, _playAreaWidth * 0.5f);

        if (_playerRect != null)
            _playerRect.anchoredPosition = new Vector2(_playerX, _playerRect.anchoredPosition.y);

        // Spawn petals
        float interval = _spawnInterval;
        if (_gameDef != null)
            interval /= (1f + _gameDef.difficultyLevel * 0.2f);

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= interval)
        {
            _spawnTimer = 0f;
            SpawnPetal();
        }

        // Update petals
        UpdatePetals();
    }

    /// <summary>Start playing the handheld game.</summary>
    public void StartPlaying()
    {
        if (_state == GameState.Playing) return;

        _state = GameState.Playing;
        _score = 0;
        _playerX = 0f;
        _spawnTimer = 0f;

        if (_overlayRoot != null)
            _overlayRoot.SetActive(true);

        UpdateScoreDisplay();

        // Set MoodMachine source
        float moodVal = _gameDef != null ? _gameDef.moodValue : 0.3f;
        MoodMachine.Instance?.SetSource("Gaming", moodVal);

        Debug.Log($"[HandheldGameController] Started playing: {_gameDef?.gameName ?? "Unknown Game"}");
    }

    /// <summary>Stop playing and close the overlay.</summary>
    public void StopPlaying()
    {
        if (_state != GameState.Playing) return;

        _state = GameState.Idle;

        // Clear petals
        foreach (var petal in _activePetals)
        {
            if (petal != null) Destroy(petal.gameObject);
        }
        _activePetals.Clear();

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);

        MoodMachine.Instance?.RemoveSource("Gaming");

        Debug.Log($"[HandheldGameController] Stopped playing. Score: {_score}");
    }

    private void SpawnPetal()
    {
        if (_playArea == null) return;

        var petalGO = new GameObject("Petal");
        petalGO.transform.SetParent(_playArea, false);

        var rect = petalGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(30f, 30f);
        float x = Random.Range(-_playAreaWidth * 0.5f, _playAreaWidth * 0.5f);
        rect.anchoredPosition = new Vector2(x, _playArea.rect.height * 0.5f);

        var image = petalGO.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, 0.6f, 0.7f); // Pink petal

        _activePetals.Add(rect);
    }

    private void UpdatePetals()
    {
        float speed = _petalFallSpeed;
        if (_gameDef != null)
            speed *= (1f + _gameDef.difficultyLevel * 0.15f);

        for (int i = _activePetals.Count - 1; i >= 0; i--)
        {
            var petal = _activePetals[i];
            if (petal == null)
            {
                _activePetals.RemoveAt(i);
                continue;
            }

            var pos = petal.anchoredPosition;
            pos.y -= speed * Time.deltaTime;
            petal.anchoredPosition = pos;

            // Check catch (near player)
            if (_playerRect != null && pos.y <= _playerRect.anchoredPosition.y + 20f)
            {
                float dist = Mathf.Abs(pos.x - _playerX);
                if (dist < 40f)
                {
                    // Caught!
                    _score++;
                    UpdateScoreDisplay();
                    if (_catchSFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(_catchSFX);
                }
                else
                {
                    // Missed
                    if (_missSFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(_missSFX);
                }

                Destroy(petal.gameObject);
                _activePetals.RemoveAt(i);
            }
            // Off screen
            else if (_playArea != null && pos.y < -_playArea.rect.height * 0.5f - 50f)
            {
                Destroy(petal.gameObject);
                _activePetals.RemoveAt(i);
            }
        }
    }

    private void UpdateScoreDisplay()
    {
        if (_scoreText != null)
            _scoreText.text = $"Petals: {_score}";
    }

    /// <summary>Set UI references (called by scene builder).</summary>
    public void SetUIReferences(GameObject overlayRoot, TMP_Text scoreText, RectTransform playerRect, RectTransform playArea)
    {
        _overlayRoot = overlayRoot;
        _scoreText = scoreText;
        _playerRect = playerRect;
        _playArea = playArea;
    }
}
