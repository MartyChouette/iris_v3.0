using System.Collections;
using UnityEngine;
using TMPro;

public class BookVolume : MonoBehaviour
{
    public enum State { OnShelf, PullingOut, Reading, PuttingBack }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this book's content and appearance.")]
    [SerializeField] private BookDefinition definition;

    [Header("Pages")]
    [Tooltip("Parent GameObject containing the page quads (activated during Reading).")]
    [SerializeField] private GameObject pagesRoot;

    [Tooltip("TMP_Text components for left and right pages.")]
    [SerializeField] private TMP_Text[] pageLabels = new TMP_Text[2];

    [Header("Navigation")]
    [Tooltip("Page indicator text (e.g. '1/2') shown at bottom center.")]
    [SerializeField] private TMP_Text pageIndicator;

    [Tooltip("Left nav arrow text.")]
    [SerializeField] private TMP_Text navLeft;

    [Tooltip("Right nav arrow text.")]
    [SerializeField] private TMP_Text navRight;

    [Header("Hidden Items")]
    [Tooltip("Hidden item overlay text (shown when on the correct spread).")]
    [SerializeField] private TMP_Text hiddenItemLabel;

    public BookDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnShelf;

    private Vector3 _shelfPosition;
    private Quaternion _shelfRotation;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private int _currentSpread;
    private int _totalSpreads;

    private const float HoverSlideDistance = 0.03f;
    private const float PullOutDuration = 0.25f;
    private const float PutBackDuration = 0.2f;

    private void Awake()
    {
        _shelfPosition = transform.position;
        _shelfRotation = transform.rotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material; // creates instance
            _baseColor = _instanceMaterial.color;
        }

        if (pagesRoot != null)
            pagesRoot.SetActive(false);
    }

    public void SetDefinition(BookDefinition def)
    {
        definition = def;
    }

    public void SetPagesRoot(GameObject root)
    {
        pagesRoot = root;
    }

    public void SetPageLabels(TMP_Text[] labels)
    {
        pageLabels = labels;
    }

    public void OnHoverEnter()
    {
        if (CurrentState != State.OnShelf || _isHovered) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;

        // Slide toward camera (negative Z in local space = toward viewer)
        transform.position = _shelfPosition - transform.forward * HoverSlideDistance;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        if (CurrentState == State.OnShelf)
            transform.position = _shelfPosition;
    }

    public void PullOut(Transform readingAnchor)
    {
        if (CurrentState != State.OnShelf) return;

        // Clear hover state
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        StartCoroutine(PullOutRoutine(readingAnchor));
    }

    public void PutBack()
    {
        if (CurrentState != State.Reading) return;
        StartCoroutine(PutBackRoutine());
    }

    /// <summary>
    /// Navigate to the next spread (called by click on right edge).
    /// </summary>
    public void NextSpread()
    {
        if (CurrentState != State.Reading) return;
        if (_currentSpread < _totalSpreads - 1)
        {
            _currentSpread++;
            PopulateSpread();
        }
    }

    /// <summary>
    /// Navigate to the previous spread (called by click on left edge).
    /// </summary>
    public void PrevSpread()
    {
        if (CurrentState != State.Reading) return;
        if (_currentSpread > 0)
        {
            _currentSpread--;
            PopulateSpread();
        }
    }

    public int CurrentSpreadIndex => _currentSpread;
    public int TotalSpreads => _totalSpreads;

    private IEnumerator PullOutRoutine(Transform readingAnchor)
    {
        CurrentState = State.PullingOut;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < PullOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / PullOutDuration);

            transform.position = Vector3.Lerp(startPos, readingAnchor.position, t);
            transform.rotation = Quaternion.Slerp(startRot, readingAnchor.rotation, t);

            yield return null;
        }

        transform.position = readingAnchor.position;
        transform.rotation = readingAnchor.rotation;

        // Parent to reading anchor so it follows camera
        transform.SetParent(readingAnchor, true);

        EnterReading();
    }

    private void EnterReading()
    {
        CurrentState = State.Reading;

        // Hide the book mesh so pages canvas renders in front
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        // Also hide the spine title canvas
        var spineTitle = transform.Find("SpineTitle");
        if (spineTitle != null) spineTitle.gameObject.SetActive(false);

        // Calculate total spreads (2 pages per spread)
        int pageCount = (definition != null && definition.pageTexts != null)
            ? definition.pageTexts.Length : 0;
        _totalSpreads = Mathf.Max(1, Mathf.CeilToInt(pageCount / 2f));
        _currentSpread = 0;

        if (pagesRoot != null)
            pagesRoot.SetActive(true);

        PopulateSpread();
    }

    private void PopulateSpread()
    {
        if (definition == null || pageLabels == null) return;

        int leftIdx = _currentSpread * 2;
        int rightIdx = leftIdx + 1;

        // Left page
        if (pageLabels.Length > 0 && pageLabels[0] != null)
        {
            pageLabels[0].text = (definition.pageTexts != null && leftIdx < definition.pageTexts.Length)
                ? (definition.pageTexts[leftIdx] ?? "") : "";
        }

        // Right page
        if (pageLabels.Length > 1 && pageLabels[1] != null)
        {
            pageLabels[1].text = (definition.pageTexts != null && rightIdx < definition.pageTexts.Length)
                ? (definition.pageTexts[rightIdx] ?? "") : "";
        }

        // Page indicator
        if (pageIndicator != null)
        {
            if (_totalSpreads > 1)
            {
                pageIndicator.gameObject.SetActive(true);
                pageIndicator.text = $"{_currentSpread + 1}/{_totalSpreads}";
            }
            else
            {
                pageIndicator.gameObject.SetActive(false);
            }
        }

        // Nav arrows
        if (navLeft != null)
            navLeft.gameObject.SetActive(_currentSpread > 0);
        if (navRight != null)
            navRight.gameObject.SetActive(_currentSpread < _totalSpreads - 1);

        // Hidden item
        if (hiddenItemLabel != null)
        {
            bool showHidden = definition.hasHiddenItem && definition.hiddenItemPage == _currentSpread;
            hiddenItemLabel.gameObject.SetActive(showHidden);
            if (showHidden && !string.IsNullOrEmpty(definition.hiddenItemDescription))
                hiddenItemLabel.text = definition.hiddenItemDescription;
        }

        // Fire static event for collectible system
        if (definition.hasHiddenItem && definition.hiddenItemPage == _currentSpread)
            OnPageViewed?.Invoke(definition, _currentSpread);
    }

    /// <summary>Static event fired when a page with a hidden item is viewed.</summary>
    public static event System.Action<BookDefinition, int> OnPageViewed;

    private IEnumerator PutBackRoutine()
    {
        CurrentState = State.PuttingBack;

        if (pagesRoot != null)
            pagesRoot.SetActive(false);

        // Show the book mesh again
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        // Show the spine title
        var spineTitle = transform.Find("SpineTitle");
        if (spineTitle != null) spineTitle.gameObject.SetActive(true);

        // Unparent from reading anchor
        transform.SetParent(null, true);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < PutBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / PutBackDuration);

            transform.position = Vector3.Lerp(startPos, _shelfPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, _shelfRotation, t);

            yield return null;
        }

        transform.position = _shelfPosition;
        transform.rotation = _shelfRotation;

        CurrentState = State.OnShelf;
    }

    private void OnDestroy()
    {
        if (_instanceMaterial != null)
            Destroy(_instanceMaterial);
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
