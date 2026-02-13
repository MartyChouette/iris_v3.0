using System.Collections;
using UnityEngine;

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

    private SpriteRenderer _iconRenderer;
    private GameObject _bubbleGO;
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
}
