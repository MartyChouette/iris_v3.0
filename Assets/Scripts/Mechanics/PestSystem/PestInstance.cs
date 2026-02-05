using UnityEngine;

/// <summary>
/// Individual pest attached to a flower part. Wiggles via Perlin noise for a
/// crawling look. Self-destructs when the host part is detached.
/// </summary>
[DisallowMultipleComponent]
public class PestInstance : MonoBehaviour
{
    [HideInInspector] public FlowerPartRuntime hostPart;
    [HideInInspector] public GameObject visual;
    [HideInInspector] public PestController controller;

    /// <summary>How long this pest has been on the part.</summary>
    public float timeOnPart;

    // Perlin offsets for wiggle
    private float _perlinX;
    private float _perlinZ;
    private Vector3 _baseLocalPos;

    // Host part tinting
    private Renderer _hostRenderer;
    private Color _originalHostColor;
    private bool _tintApplied;
    private static readonly Color InfestTint = new Color(0.5f, 0.65f, 0.35f); // sickly green

    void Start()
    {
        _perlinX = Random.value * 1000f;
        _perlinZ = Random.value * 1000f;
        _baseLocalPos = transform.localPosition;

        // Tint host part
        if (hostPart != null)
        {
            _hostRenderer = hostPart.GetComponentInChildren<Renderer>();
            if (_hostRenderer != null && _hostRenderer.material != null)
            {
                _originalHostColor = _hostRenderer.material.color;
                _hostRenderer.material.color = Color.Lerp(_originalHostColor, InfestTint, 0.4f);
                _tintApplied = true;
            }
        }
    }

    void Update()
    {
        timeOnPart += Time.deltaTime;

        // Check if host was detached
        if (hostPart == null || !hostPart.isAttached)
        {
            RemoveSelf();
            return;
        }

        // Perlin wiggle
        float wiggleAmount = 0.008f;
        float speed = 3f;
        float x = (Mathf.PerlinNoise(_perlinX + Time.time * speed, 0f) - 0.5f) * wiggleAmount;
        float z = (Mathf.PerlinNoise(0f, _perlinZ + Time.time * speed) - 0.5f) * wiggleAmount;
        transform.localPosition = _baseLocalPos + new Vector3(x, 0f, z);
    }

    void OnDestroy()
    {
        RestoreHostTint();
    }

    private void RemoveSelf()
    {
        RestoreHostTint();
        if (controller != null)
            controller.OnPestRemoved(this);
        Destroy(gameObject);
    }

    private void RestoreHostTint()
    {
        if (_tintApplied && _hostRenderer != null && _hostRenderer.material != null)
        {
            _hostRenderer.material.color = _originalHostColor;
            _tintApplied = false;
        }
    }
}
