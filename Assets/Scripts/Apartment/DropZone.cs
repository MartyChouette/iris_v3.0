using System.Collections;
using UnityEngine;

/// <summary>
/// Generic named drop zone. Items whose HomeZoneName matches this zone's name
/// are considered "at home" when placed here. Optionally destroys deposited items
/// (e.g. trash can). Pulses a highlight when the player holds a matching item.
/// </summary>
public class DropZone : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Name that PlaceableObject.HomeZoneName must match.")]
    [SerializeField] private string _zoneName = "";

    [Header("Behavior")]
    [Tooltip("If true, deposited items are destroyed (e.g. trash can).")]
    [SerializeField] private bool _destroyOnDeposit;

    [Header("Visual")]
    [Tooltip("Renderer for the zone highlight quad.")]
    [SerializeField] private Renderer _zoneRenderer;

    [Tooltip("Active pulse color (matching item held).")]
    [SerializeField] private Color _activeColor = new Color(0.4f, 0.9f, 1.0f, 0.55f);

    [Tooltip("Pulse speed (oscillations per second).")]
    [SerializeField] private float _pulseSpeed = 2f;

    [Header("Audio")]
    [Tooltip("SFX played on deposit.")]
    [SerializeField] private AudioClip _depositSFX;

    [Tooltip("SFX played when trash is destroyed.")]
    [SerializeField] private AudioClip _trashSFX;

    public string ZoneName => _zoneName;
    public bool DestroyOnDeposit => _destroyOnDeposit;
    public int DepositCount { get; private set; }

    private Material _instanceMat;
    private bool _playerHoldingMatch;

    private void Start()
    {
        if (_zoneRenderer == null)
            _zoneRenderer = GetComponent<Renderer>();

        if (_zoneRenderer != null)
        {
            if (_zoneRenderer.sharedMaterial != null)
            {
                _instanceMat = new Material(_zoneRenderer.sharedMaterial);
                _zoneRenderer.material = _instanceMat;
            }
            // Start hidden — only show when player holds a matching item
            _zoneRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (_zoneRenderer == null) return;

        // Check if player is holding an item that matches this zone (static accessor — no scene scan)
        _playerHoldingMatch = false;
        var held = ObjectGrabber.HeldObject;
        if (held != null && !string.IsNullOrEmpty(_zoneName)
            && (held.HomeZoneName == _zoneName || held.AltHomeZoneName == _zoneName))
            _playerHoldingMatch = true;

        // Toggle renderer — Color.clear on opaque materials renders as a dark rectangle,
        // so we disable the renderer entirely when no matching item is held.
        if (_playerHoldingMatch)
        {
            if (!_zoneRenderer.enabled)
                _zoneRenderer.enabled = true;

            if (_instanceMat != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);
                _instanceMat.color = Color.Lerp(_activeColor * 0.6f, _activeColor, pulse);
            }
        }
        else
        {
            if (_zoneRenderer.enabled)
                _zoneRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Register an item deposit. If destroyOnDeposit, plays shrink animation then destroys.
    /// </summary>
    public void RegisterDeposit(PlaceableObject item)
    {
        if (item == null) return;

        DepositCount++;
        item.IsAtHome = true;

        if (_destroyOnDeposit)
        {
            if (_trashSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(_trashSFX);

            StartCoroutine(ShrinkAndDestroy(item.gameObject));
        }
        else
        {
            if (_depositSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(_depositSFX);
        }

        Debug.Log($"[DropZone] {item.name} deposited at {_zoneName}. Total: {DepositCount}");
    }

    private IEnumerator ShrinkAndDestroy(GameObject go)
    {
        Vector3 startScale = go.transform.localScale;
        float elapsed = 0f;
        const float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(go);
    }

    private void OnDestroy()
    {
        if (_instanceMat != null)
            Destroy(_instanceMat);
    }
}
