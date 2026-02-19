using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Overlay canvas showing phase name + grade letter at each date phase checkpoint.
/// Center screen, brief display with juice effects scaled to grade quality.
/// </summary>
public class PhaseScorePresenter : MonoBehaviour
{
    public static PhaseScorePresenter Instance { get; private set; }

    [Header("References")]
    [Tooltip("Root panel for the phase score overlay.")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("Phase label (e.g. 'First Impression').")]
    [SerializeField] private TMP_Text _phaseLabel;

    [Tooltip("Grade letter display.")]
    [SerializeField] private TMP_Text _gradeLabel;

    [Tooltip("CanvasGroup for fade.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Timing")]
    [Tooltip("Duration the overlay shows for normal phases.")]
    [SerializeField] private float _normalDuration = 3f;

    [Tooltip("Duration the overlay shows for the final phase.")]
    [SerializeField] private float _finalDuration = 4f;

    [Header("Audio")]
    [SerializeField] private AudioClip _phaseGradeSFX;

    private Coroutine _activePresentation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PhaseScorePresenter] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (DateSessionManager.Instance != null)
            DateSessionManager.Instance.OnPhaseTransition += OnPhaseTransition;
    }

    private void OnPhaseTransition(DateSessionManager.DatePhase fromPhase, DateSessionManager.DatePhase toPhase, float affectionDelta)
    {
        string label;
        float totalAffection = DateSessionManager.Instance?.Affection ?? 50f;
        bool isFinal = toPhase == DateSessionManager.DatePhase.None;

        switch (fromPhase)
        {
            case DateSessionManager.DatePhase.Arrival:
                label = "First Impression";
                break;
            case DateSessionManager.DatePhase.DrinkJudging:
                label = "Drink Service";
                break;
            case DateSessionManager.DatePhase.ApartmentJudging:
                label = "The Date";
                break;
            default:
                return;
        }

        // For the final transition, use total affection for the grade
        float scoreForGrade = isFinal ? totalAffection : 50f + affectionDelta;
        string grade = DateEndScreen.ComputeGrade(Mathf.Clamp(scoreForGrade, 0f, 100f));
        float duration = isFinal ? _finalDuration : _normalDuration;

        ShowPhaseScore(label, grade, duration);
    }

    private void ShowPhaseScore(string phaseLabel, string grade, float duration)
    {
        if (_activePresentation != null)
            StopCoroutine(_activePresentation);

        _activePresentation = StartCoroutine(PresentationSequence(phaseLabel, grade, duration));
    }

    private IEnumerator PresentationSequence(string phaseLabel, string grade, float duration)
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        if (_phaseLabel != null)
            _phaseLabel.text = phaseLabel;

        if (_gradeLabel != null)
        {
            _gradeLabel.text = grade;
            _gradeLabel.color = grade switch
            {
                "S" => new Color(1f, 0.84f, 0f),
                "A" => new Color(0.4f, 1f, 0.4f),
                "B" => new Color(0.4f, 0.8f, 1f),
                "C" => new Color(1f, 0.8f, 0.4f),
                _ => new Color(1f, 0.4f, 0.4f)
            };
        }

        // Play SFX
        if (_phaseGradeSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_phaseGradeSFX);

        // Fade in
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            float fadeIn = 0.2f;
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        // Hold
        float holdElapsed = 0f;
        while (holdElapsed < duration - 0.6f) // subtract fade times
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        if (_canvasGroup != null)
        {
            float fadeOut = 0.4f;
            float elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        _activePresentation = null;
    }
}
