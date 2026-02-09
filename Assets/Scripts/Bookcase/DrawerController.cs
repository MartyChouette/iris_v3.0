using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a single drawer below the bookcase. Click to slide open,
/// revealing trinkets inside. Re-click or ESC to close.
/// </summary>
public class DrawerController : MonoBehaviour
{
    public enum State { Closed, Opening, Open, Closing }

    [Header("Settings")]
    [Tooltip("Distance the drawer slides forward (local -Z).")]
    [SerializeField] private float slideDistance = 0.3f;

    [Tooltip("Time to open/close in seconds.")]
    [SerializeField] private float slideDuration = 0.3f;

    [Header("Contents")]
    [Tooltip("Root GameObject containing TrinketVolume children (activated on open).")]
    [SerializeField] private GameObject contentsRoot;

    public State CurrentState { get; private set; } = State.Closed;

    private Vector3 _closedPosition;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private void Awake()
    {
        _closedPosition = transform.localPosition;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }

        if (contentsRoot != null)
            contentsRoot.SetActive(false);
    }

    public void SetContentsRoot(GameObject root)
    {
        contentsRoot = root;
    }

    public void OnHoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;
    }

    public void Open()
    {
        if (CurrentState != State.Closed) return;
        StartCoroutine(SlideRoutine(true));
    }

    public void Close()
    {
        if (CurrentState != State.Open) return;
        StartCoroutine(SlideRoutine(false));
    }

    private IEnumerator SlideRoutine(bool opening)
    {
        CurrentState = opening ? State.Opening : State.Closing;

        // Clear hover visual
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        Vector3 openPosition = _closedPosition - transform.parent.InverseTransformDirection(transform.forward) * slideDistance;
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = opening ? openPosition : _closedPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / slideDuration);
            transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.localPosition = endPos;

        if (opening)
        {
            CurrentState = State.Open;
            if (contentsRoot != null)
                contentsRoot.SetActive(true);
        }
        else
        {
            CurrentState = State.Closed;
            if (contentsRoot != null)
                contentsRoot.SetActive(false);
        }
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
