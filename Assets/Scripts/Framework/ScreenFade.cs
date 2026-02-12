using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-scoped singleton that fades the screen to/from black via a full-screen CanvasGroup overlay.
/// Built by ApartmentSceneBuilder.BuildScreenFade().
/// </summary>
public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }

    [SerializeField] private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ScreenFade] Duplicate instance destroyed.");
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

    /// <summary>
    /// Fade to black (alpha 0 → 1). Blocks raycasts while faded.
    /// </summary>
    public Coroutine FadeOut(float duration)
    {
        return StartCoroutine(FadeCoroutine(0f, 1f, duration, true));
    }

    /// <summary>
    /// Fade in from black (alpha 1 → 0). Unblocks raycasts when done.
    /// </summary>
    public Coroutine FadeIn(float duration)
    {
        return StartCoroutine(FadeCoroutine(1f, 0f, duration, false));
    }

    private IEnumerator FadeCoroutine(float from, float to, float duration, bool blockWhenDone)
    {
        if (_canvasGroup == null) yield break;

        _canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        _canvasGroup.alpha = to;
        _canvasGroup.blocksRaycasts = blockWhenDone;
    }
}
