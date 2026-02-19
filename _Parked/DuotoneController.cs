using UnityEngine;

/// <summary>
/// Runtime controller for the Iris/Fullscreen/Duotone fullscreen shader.
/// Drives the material's _Blend property to fade the effect in/out.
/// Toggle with a key, or call SetEnabled() / SetColors() from code.
///
/// Setup:
///   1. Create a Material using shader "Iris/Fullscreen/Duotone"
///   2. On your URP Renderer Data → Add Renderer Feature → Full Screen Pass Renderer Feature
///   3. Assign the material, set injection point to "After Rendering Post Processing"
///   4. Attach this script to any GO, drag in the material reference
///   5. The feature stays always-on; _Blend=0 means passthrough (no perf cost worth worrying about)
/// </summary>
public class DuotoneController : MonoBehaviour
{
    public static DuotoneController Instance { get; private set; }

    [Header("Material")]
    [Tooltip("Material using Iris/Fullscreen/Duotone shader.")]
    [SerializeField] private Material _duotoneMaterial;

    [Header("Colors")]
    [SerializeField] private Color _colorDark = Color.black;
    [SerializeField] private Color _colorLight = Color.white;

    [Header("Threshold")]
    [SerializeField, Range(0f, 1f)] private float _threshold = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float _softness = 0f;

    [Header("Toggle")]
    [Tooltip("Key to toggle the effect on/off at runtime.")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.None;

    [Header("Transition")]
    [Tooltip("Seconds to fade in/out. 0 = instant.")]
    [SerializeField] private float _transitionDuration = 0.3f;

    private float _blend;
    private float _blendTarget;
    private bool _enabled;

    // Shader property IDs
    private static readonly int PropColorDark  = Shader.PropertyToID("_ColorDark");
    private static readonly int PropColorLight = Shader.PropertyToID("_ColorLight");
    private static readonly int PropThreshold  = Shader.PropertyToID("_Threshold");
    private static readonly int PropSoftness   = Shader.PropertyToID("_Softness");
    private static readonly int PropBlend      = Shader.PropertyToID("_Blend");

    public bool IsEnabled => _enabled;
    public float Blend => _blend;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _blend = 0f;
        _blendTarget = 0f;
        PushAll();
    }

    private void OnDestroy()
    {
        // Reset blend so the material doesn't stay tinted in editor
        if (_duotoneMaterial != null)
            _duotoneMaterial.SetFloat(PropBlend, 0f);

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
            SetEnabled(!_enabled);

        // Animate blend toward target
        if (!Mathf.Approximately(_blend, _blendTarget))
        {
            if (_transitionDuration <= 0f)
            {
                _blend = _blendTarget;
            }
            else
            {
                float speed = 1f / _transitionDuration;
                _blend = Mathf.MoveTowards(_blend, _blendTarget, speed * Time.unscaledDeltaTime);
            }

            if (_duotoneMaterial != null)
                _duotoneMaterial.SetFloat(PropBlend, _blend);
        }
    }

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>Toggle the duotone effect on or off (animates via _Blend).</summary>
    public void SetEnabled(bool on)
    {
        _enabled = on;
        _blendTarget = on ? 1f : 0f;
    }

    /// <summary>Set the two duotone colors.</summary>
    public void SetColors(Color dark, Color light)
    {
        _colorDark = dark;
        _colorLight = light;
        if (_duotoneMaterial != null)
        {
            _duotoneMaterial.SetColor(PropColorDark, _colorDark);
            _duotoneMaterial.SetColor(PropColorLight, _colorLight);
        }
    }

    /// <summary>Change threshold and optionally softness at runtime.</summary>
    public void SetThreshold(float threshold, float softness = -1f)
    {
        _threshold = Mathf.Clamp01(threshold);
        if (softness >= 0f) _softness = Mathf.Clamp(softness, 0f, 0.5f);
        if (_duotoneMaterial != null)
        {
            _duotoneMaterial.SetFloat(PropThreshold, _threshold);
            _duotoneMaterial.SetFloat(PropSoftness, _softness);
        }
    }

    /// <summary>Instantly set blend amount (0 = off, 1 = full duotone).</summary>
    public void SetBlendImmediate(float blend)
    {
        _blend = Mathf.Clamp01(blend);
        _blendTarget = _blend;
        _enabled = _blend > 0f;
        if (_duotoneMaterial != null)
            _duotoneMaterial.SetFloat(PropBlend, _blend);
    }

    // ─── Internals ───────────────────────────────────────────────

    private void PushAll()
    {
        if (_duotoneMaterial == null) return;
        _duotoneMaterial.SetColor(PropColorDark, _colorDark);
        _duotoneMaterial.SetColor(PropColorLight, _colorLight);
        _duotoneMaterial.SetFloat(PropThreshold, _threshold);
        _duotoneMaterial.SetFloat(PropSoftness, _softness);
        _duotoneMaterial.SetFloat(PropBlend, _blend);
    }
}
