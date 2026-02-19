using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Screen-space thought panel showing Nema's internal observations.
/// Bottom-left corner, semi-transparent dark panel, italic TMP text.
/// This is how the player "learns" — Nema narrates what she notices.
/// </summary>
public class NemaThoughtBubble : MonoBehaviour
{
    public static NemaThoughtBubble Instance { get; private set; }

    [Header("References")]
    [Tooltip("The text component for the thought.")]
    [SerializeField] private TMP_Text _thoughtText;

    [Tooltip("The CanvasGroup for fade in/out.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Timing")]
    [Tooltip("Fade-in duration in seconds.")]
    [SerializeField] private float _fadeInDuration = 0.2f;

    [Tooltip("Display duration in seconds.")]
    [SerializeField] private float _displayDuration = 3f;

    [Tooltip("Fade-out duration in seconds.")]
    [SerializeField] private float _fadeOutDuration = 0.4f;

    private Coroutine _activeThought;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NemaThoughtBubble] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Show a thought with the given mood coloring.</summary>
    public void ShowThought(string text, ThoughtMood mood)
    {
        if (_activeThought != null)
            StopCoroutine(_activeThought);

        _activeThought = StartCoroutine(ThoughtSequence(text, mood));
    }

    private IEnumerator ThoughtSequence(string text, ThoughtMood mood)
    {
        if (_thoughtText != null)
        {
            _thoughtText.text = text;
            _thoughtText.fontStyle = FontStyles.Italic;
            _thoughtText.color = GetMoodColor(mood);
        }

        // Fade in (uses unscaledDeltaTime for juice slow-mo compatibility)
        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        // Hold
        float holdElapsed = 0f;
        while (holdElapsed < _displayDuration)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        _activeThought = null;
    }

    private static Color GetMoodColor(ThoughtMood mood) => mood switch
    {
        ThoughtMood.Observation => Color.white,
        ThoughtMood.Positive => new Color(1f, 0.7f, 0.75f),   // Warm pink
        ThoughtMood.Negative => new Color(0.6f, 0.7f, 0.9f),  // Muted blue
        ThoughtMood.Insight => new Color(1f, 0.84f, 0f),       // Gold
        _ => Color.white
    };
}
