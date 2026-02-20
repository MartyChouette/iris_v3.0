using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// World-space reaction bubble above the date character. Shows a question mark
/// (notice) then the reaction icon (heart/neutral/frown) with SFX.
/// </summary>
public class DateReactionUI : MonoBehaviour
{
    [Header("Icon Sprites")]
    [SerializeField] private Sprite questionSprite;
    [SerializeField] private Sprite heartSprite;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite dislikeSprite;

    [Header("Timing")]
    [Tooltip("Seconds the question mark shows.")]
    [SerializeField] private float noticeDuration = 0.6f;

    [Tooltip("Seconds the reaction icon shows.")]
    [SerializeField] private float reactionDuration = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip likeSFX;
    [SerializeField] private AudioClip dislikeSFX;

    [Header("Offset")]
    [Tooltip("Height above the character's origin for the bubble.")]
    [SerializeField] private float bubbleHeight = 2.2f;

    // Sentiment text arrays for labeled reactions
    private static readonly string[] s_likeTexts = { "Loves it!", "Really into this!", "Great taste!" };
    private static readonly string[] s_neutralTexts = { "Hmm, okay.", "Not bad.", "Sure." };
    private static readonly string[] s_dislikeTexts = { "Not a fan...", "Yikes.", "Hard pass." };

    private SpriteRenderer _iconRenderer;
    private GameObject _bubbleGO;
    private TextMeshPro _bubbleText;
    private Coroutine _activeReaction;
    private Camera _cachedCamera;
    private WaitForSeconds _waitNotice;
    private WaitForSeconds _waitReaction;

    private void Awake()
    {
        // Create a child object for the reaction bubble
        _bubbleGO = new GameObject("ReactionBubble");
        _bubbleGO.transform.SetParent(transform);
        _bubbleGO.transform.localPosition = new Vector3(0f, bubbleHeight, 0f);

        _iconRenderer = _bubbleGO.AddComponent<SpriteRenderer>();
        _iconRenderer.sortingOrder = 100;

        _bubbleGO.SetActive(false);

        _waitNotice = new WaitForSeconds(noticeDuration);
        _waitReaction = new WaitForSeconds(reactionDuration);
    }

    private void LateUpdate()
    {
        // Billboard: face camera
        if (_bubbleGO.activeSelf)
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera != null)
                _bubbleGO.transform.rotation = _cachedCamera.transform.rotation;
        }
    }

    /// <summary>Show a reaction sequence: notice → reaction icon → fade out.</summary>
    public void ShowReaction(ReactionType type)
    {
        if (_activeReaction != null)
            StopCoroutine(_activeReaction);

        _activeReaction = StartCoroutine(ReactionSequence(type));
    }

    private IEnumerator ReactionSequence(ReactionType type)
    {
        _bubbleGO.SetActive(true);

        // Notice phase — question mark
        _iconRenderer.sprite = questionSprite;
        _iconRenderer.color = Color.white;
        _bubbleGO.transform.localScale = Vector3.one * 0.5f;

        yield return _waitNotice;

        // Reaction phase — show appropriate icon
        _iconRenderer.sprite = type switch
        {
            ReactionType.Like => heartSprite,
            ReactionType.Dislike => dislikeSprite,
            _ => neutralSprite
        };

        // Color tint
        _iconRenderer.color = type switch
        {
            ReactionType.Like => new Color(1f, 0.4f, 0.5f),
            ReactionType.Dislike => new Color(0.5f, 0.5f, 1f),
            _ => Color.white
        };

        // Play SFX
        AudioClip clip = type switch
        {
            ReactionType.Like => likeSFX,
            ReactionType.Dislike => dislikeSFX,
            _ => null
        };
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);

        // Pop-in scale
        _bubbleGO.transform.localScale = Vector3.one * 0.7f;

        yield return _waitReaction;

        // Fade out
        float fadeTime = 0.3f;
        float elapsed = 0f;
        Color startColor = _iconRenderer.color;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            _iconRenderer.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }

        _bubbleGO.SetActive(false);
        _activeReaction = null;
    }

    /// <summary>Show a text message above the character for the given duration.</summary>
    public void ShowText(string message, float duration)
    {
        if (_activeReaction != null)
            StopCoroutine(_activeReaction);

        _activeReaction = StartCoroutine(TextSequence(message, duration));
    }

    private IEnumerator TextSequence(string message, float duration)
    {
        _bubbleGO.SetActive(true);
        _iconRenderer.enabled = false;

        EnsureBubbleText();

        _bubbleText.text = message;
        _bubbleText.gameObject.SetActive(true);
        _bubbleGO.transform.localScale = Vector3.one * 0.5f;

        yield return new WaitForSeconds(duration);

        // Fade out
        float fadeTime = 0.3f;
        float elapsed = 0f;
        Color startColor = _bubbleText.color;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            _bubbleText.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }

        _bubbleText.gameObject.SetActive(false);
        _bubbleText.color = startColor;
        _iconRenderer.enabled = true;
        _bubbleGO.SetActive(false);
        _activeReaction = null;
    }

    /// <summary>Show a labeled reaction: topic label → reaction icon + sentiment text → fade out.</summary>
    public void ShowLabeledReaction(ReactionType type, string topicLabel)
    {
        if (_activeReaction != null)
            StopCoroutine(_activeReaction);

        _activeReaction = StartCoroutine(LabeledReactionSequence(type, topicLabel));
    }

    private IEnumerator LabeledReactionSequence(ReactionType type, string topicLabel)
    {
        _bubbleGO.SetActive(true);
        EnsureBubbleText();

        // --- Phase 1: Topic label (0.7s) ---
        _iconRenderer.sprite = questionSprite;
        _iconRenderer.color = Color.white;
        _iconRenderer.enabled = true;
        _bubbleGO.transform.localScale = Vector3.one * 0.5f;

        _bubbleText.text = topicLabel;
        _bubbleText.color = Color.white;
        _bubbleText.gameObject.SetActive(true);
        // Position text above icon
        _bubbleText.rectTransform.localPosition = new Vector3(0f, 0.4f, 0f);

        yield return new WaitForSeconds(0.7f);

        // --- Phase 2: Reaction icon + sentiment (1.8s) ---
        _iconRenderer.sprite = type switch
        {
            ReactionType.Like => heartSprite,
            ReactionType.Dislike => dislikeSprite,
            _ => neutralSprite
        };

        _iconRenderer.color = type switch
        {
            ReactionType.Like => new Color(1f, 0.4f, 0.5f),
            ReactionType.Dislike => new Color(0.5f, 0.5f, 1f),
            _ => Color.white
        };

        // Play SFX
        AudioClip clip = type switch
        {
            ReactionType.Like => likeSFX,
            ReactionType.Dislike => dislikeSFX,
            _ => null
        };
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);

        _bubbleText.text = GetRandomSentiment(type);
        _bubbleText.color = _iconRenderer.color;
        // Position text below icon
        _bubbleText.rectTransform.localPosition = new Vector3(0f, -0.4f, 0f);

        _bubbleGO.transform.localScale = Vector3.one * 0.7f;

        yield return new WaitForSeconds(1.8f);

        // --- Phase 3: Fade out (0.3s) ---
        float fadeTime = 0.3f;
        float elapsed = 0f;
        Color iconStart = _iconRenderer.color;
        Color textStart = _bubbleText.color;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            _iconRenderer.color = new Color(iconStart.r, iconStart.g, iconStart.b, 1f - t);
            _bubbleText.color = new Color(textStart.r, textStart.g, textStart.b, 1f - t);
            yield return null;
        }

        _bubbleText.gameObject.SetActive(false);
        _bubbleText.rectTransform.localPosition = Vector3.zero;
        _bubbleText.color = Color.white;
        _iconRenderer.enabled = true;
        _bubbleGO.SetActive(false);
        _activeReaction = null;
    }

    private void EnsureBubbleText()
    {
        if (_bubbleText != null) return;

        var textGO = new GameObject("BubbleText");
        textGO.transform.SetParent(_bubbleGO.transform, false);
        textGO.transform.localPosition = Vector3.zero;
        _bubbleText = textGO.AddComponent<TextMeshPro>();
        _bubbleText.fontSize = 5f;
        _bubbleText.alignment = TextAlignmentOptions.Center;
        _bubbleText.color = Color.white;
        _bubbleText.outlineWidth = 0.2f;
        _bubbleText.outlineColor = new Color32(0, 0, 0, 200);
        _bubbleText.rectTransform.sizeDelta = new Vector2(4f, 2f);
        _bubbleText.enableWordWrapping = true;
    }

    private static string GetRandomSentiment(ReactionType type)
    {
        var arr = type switch
        {
            ReactionType.Like => s_likeTexts,
            ReactionType.Dislike => s_dislikeTexts,
            _ => s_neutralTexts
        };
        return arr[UnityEngine.Random.Range(0, arr.Length)];
    }
}
