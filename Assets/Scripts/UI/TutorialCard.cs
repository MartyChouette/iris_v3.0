using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Show/dismiss controller for the tutorial card overlay.
/// Activated by MainMenuManager before loading the apartment scene.
/// Fades in with a subtle scale animation, fades out on dismiss.
/// </summary>
public class TutorialCard : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _startButton;
    [SerializeField] private AudioClip _dismissSFX;

    [Header("Animation")]
    [Tooltip("Duration of the fade-in animation.")]
    [SerializeField] private float _fadeInDuration = 0.35f;

    [Tooltip("Duration of the fade-out animation.")]
    [SerializeField] private float _fadeOutDuration = 0.2f;

    [Tooltip("Starting scale for the pop-in (1.0 = no scale).")]
    [SerializeField] private float _scaleFrom = 0.92f;

    private Action _onDismiss;
    private CanvasGroup _canvasGroup;
    private RectTransform _panelRT;
    private Coroutine _animCoroutine;

    private void Awake()
    {
        // Auto-find CanvasGroup on root (added by builder or at runtime)
        if (_root != null)
        {
            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.AddComponent<CanvasGroup>();

            // Find the panel child for scale animation
            if (_root.transform.childCount > 1)
                _panelRT = _root.transform.GetChild(1) as RectTransform; // CardPanel is child 1 (after Backdrop)
        }
    }

    /// <summary>Show the tutorial card with fade-in. When the player clicks BEGIN, onDismiss is invoked.</summary>
    public void Show(Action onDismiss)
    {
        _onDismiss = onDismiss;
        if (_root != null) _root.SetActive(true);
        if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);

        if (AccessibilitySettings.ReduceMotion)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_panelRT != null) _panelRT.localScale = Vector3.one;
        }
        else
        {
            _animCoroutine = StartCoroutine(FadeIn());
        }
    }

    private void OnStartClicked()
    {
        if (_startButton != null) _startButton.onClick.RemoveListener(OnStartClicked);

        if (_dismissSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_dismissSFX);

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);

        if (AccessibilitySettings.ReduceMotion)
        {
            FinishDismiss();
        }
        else
        {
            _animCoroutine = StartCoroutine(FadeOut());
        }
    }

    private void FinishDismiss()
    {
        if (_root != null) _root.SetActive(false);
        _onDismiss?.Invoke();
        _onDismiss = null;
    }

    // ── Animation ────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        if (_panelRT != null) _panelRT.localScale = Vector3.one * _scaleFrom;

        float elapsed = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeInDuration);
            // Ease out cubic: fast start, gentle settle
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            if (_canvasGroup != null) _canvasGroup.alpha = eased;
            if (_panelRT != null) _panelRT.localScale = Vector3.one * Mathf.Lerp(_scaleFrom, 1f, eased);
            yield return null;
        }

        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        if (_panelRT != null) _panelRT.localScale = Vector3.one;
        _animCoroutine = null;
    }

    private IEnumerator FadeOut()
    {
        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        float elapsed = 0f;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeOutDuration);
            // Ease in quad: gentle start, fast finish
            float eased = t * t;

            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            if (_panelRT != null) _panelRT.localScale = Vector3.one * Mathf.Lerp(1f, _scaleFrom, eased);
            yield return null;
        }

        _animCoroutine = null;
        FinishDismiss();
    }
}
