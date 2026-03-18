using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Scene-scoped singleton that creates a global URP Volume for FF8/PE/SotC atmosphere.
/// Teal shadows, warm highlights, soft bloom, subtle grain, desaturation.
/// Ties into MoodMachine and GameClock for time-of-day shifts.
/// Also spawns dust mote particles.
/// </summary>
public class AtmosphereController : MonoBehaviour
{
    public static AtmosphereController Instance { get; private set; }

    [Header("Color Grading")]
    [Tooltip("Shadow tint — push toward teal/blue for FF8 look.")]
    [SerializeField] private Color _shadowTint = new Color(0.75f, 0.92f, 1.0f, 1f);

    [Tooltip("Highlight tint — warm amber/cream.")]
    [SerializeField] private Color _highlightTint = new Color(1.0f, 0.95f, 0.85f, 1f);

    [Tooltip("Overall saturation adjustment (-100 to 100).")]
    [Range(-80f, 20f)]
    [SerializeField] private float _saturation = -25f;

    [Tooltip("Overall contrast adjustment.")]
    [Range(-50f, 50f)]
    [SerializeField] private float _contrast = 12f;

    [Tooltip("Post-exposure (brightness). Positive = brighter, SotC overexposed look.")]
    [Range(-2f, 3f)]
    [SerializeField] private float _postExposure = 0.3f;

    [Header("Bloom")]
    [Tooltip("Bloom threshold — lower = more glow.")]
    [Range(0f, 2f)]
    [SerializeField] private float _bloomThreshold = 0.7f;

    [Tooltip("Bloom intensity.")]
    [Range(0f, 3f)]
    [SerializeField] private float _bloomIntensity = 0.6f;

    [Tooltip("Bloom scatter — higher = wider, softer glow.")]
    [Range(0f, 1f)]
    [SerializeField] private float _bloomScatter = 0.75f;

    [Tooltip("Bloom tint color.")]
    [SerializeField] private Color _bloomTint = new Color(0.9f, 0.93f, 1.0f, 1f);

    [Header("Vignette")]
    [Range(0f, 1f)]
    [SerializeField] private float _vignetteIntensity = 0.3f;

    [Range(0.1f, 1f)]
    [SerializeField] private float _vignetteSmoothness = 0.4f;

    [Header("Film Grain")]
    [Range(0f, 1f)]
    [SerializeField] private float _grainIntensity = 0.15f;

    [Header("Dust Motes")]
    [SerializeField] private bool _enableDust = true;

    [Tooltip("Max particles in the room.")]
    [SerializeField] private int _dustMaxParticles = 200;

    [Tooltip("Particles emitted per second.")]
    [SerializeField] private float _dustEmissionRate = 12f;

    [Tooltip("Room volume size for dust spawning.")]
    [SerializeField] private Vector3 _dustVolume = new Vector3(12f, 3f, 12f);

    [Tooltip("Dust particle size range.")]
    [SerializeField] private float _dustSizeMin = 0.004f;

    [Tooltip("Dust particle size range.")]
    [SerializeField] private float _dustSizeMax = 0.012f;

    [Tooltip("Dust base color (tinted by directional light at runtime).")]
    [SerializeField] private Color _dustColor = new Color(1f, 0.97f, 0.92f, 0.2f);

    // ── Runtime refs ──
    private Volume _volume;
    private VolumeProfile _profile;
    private ColorAdjustments _colorAdj;
    private LiftGammaGain _liftGammaGain;
    private Bloom _bloom;
    private Vignette _vignette;
    private FilmGrain _filmGrain;
    private ParticleSystem _dustPS;
    private Material _dustMat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        BuildVolume();
        if (_enableDust) BuildDustParticles();
        ApplySettings();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_profile != null) Destroy(_profile);
        if (_dustMat != null) Destroy(_dustMat);
    }

    private void LateUpdate()
    {
        // ── MoodMachine integration ──
        // Mood 0 = clear/happy, 1 = dark/stormy
        // Clear: full bloom, bright exposure (SotC overexposed)
        // Stormy: reduced bloom, more grain, darker (PE moody)
        if (MoodMachine.Instance != null && _bloom != null)
        {
            float mood = MoodMachine.Instance.Mood;

            _bloom.intensity.Override(Mathf.Lerp(_bloomIntensity, _bloomIntensity * 0.25f, mood));
            _bloom.scatter.Override(Mathf.Lerp(_bloomScatter, _bloomScatter * 0.5f, mood));
            _filmGrain.intensity.Override(Mathf.Lerp(_grainIntensity, Mathf.Min(_grainIntensity * 2.5f, 0.5f), mood));
            _colorAdj.postExposure.Override(Mathf.Lerp(_postExposure, _postExposure - 0.4f, mood));
            _vignette.intensity.Override(Mathf.Lerp(_vignetteIntensity, Mathf.Min(_vignetteIntensity + 0.15f, 0.6f), mood));
        }

        // ── Tint dust by directional light color ──
        if (_dustPS != null && _dustMat != null)
        {
            Color lightCol = Color.white;
            float lightInt = 1f;
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                lightCol = sun.color;
                lightInt = sun.intensity;
            }
            Color dustLit = _dustColor * lightCol * Mathf.Min(lightInt, 1.5f);
            dustLit.a = _dustColor.a;
            _dustMat.SetColor("_BaseColor", dustLit);
        }
    }

    private void OnValidate()
    {
        if (_volume != null) ApplySettings();
    }

    // ═══════════════════════════════════════
    //  Volume setup
    // ═══════════════════════════════════════

    private void BuildVolume()
    {
        var volumeGO = new GameObject("AtmosphereVolume");
        volumeGO.transform.SetParent(transform, false);
        volumeGO.layer = gameObject.layer;

        _volume = volumeGO.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 50; // above camera presets (default 0)

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = _profile;

        _colorAdj = _profile.Add<ColorAdjustments>();
        _liftGammaGain = _profile.Add<LiftGammaGain>();
        _bloom = _profile.Add<Bloom>();
        _vignette = _profile.Add<Vignette>();
        _filmGrain = _profile.Add<FilmGrain>();
    }

    // ═══════════════════════════════════════
    //  Apply settings to volume overrides
    // ═══════════════════════════════════════

    public void ApplySettings()
    {
        if (_colorAdj == null) return;

        // Color Adjustments
        _colorAdj.saturation.Override(_saturation);
        _colorAdj.contrast.Override(_contrast);
        _colorAdj.postExposure.Override(_postExposure);

        // Three-way color grading via Lift/Gamma/Gain
        // Lift (shadows): teal-blue push
        _colorAdj.colorFilter.Override(Color.white); // neutral filter, use LGG instead

        Vector4 lift = new Vector4(
            _shadowTint.r,
            _shadowTint.g,
            _shadowTint.b,
            0f // exposure offset
        );
        Vector4 gamma = new Vector4(1f, 1f, 1f, 0f); // neutral midtones
        Vector4 gain = new Vector4(
            _highlightTint.r,
            _highlightTint.g,
            _highlightTint.b,
            0f
        );
        _liftGammaGain.lift.Override(lift);
        _liftGammaGain.gamma.Override(gamma);
        _liftGammaGain.gain.Override(gain);

        // Bloom — soft, wide (SotC overexposed feel)
        _bloom.threshold.Override(_bloomThreshold);
        _bloom.intensity.Override(_bloomIntensity);
        _bloom.scatter.Override(_bloomScatter);
        _bloom.tint.Override(_bloomTint);

        // Vignette — subtle edge darkening
        _vignette.intensity.Override(_vignetteIntensity);
        _vignette.smoothness.Override(_vignetteSmoothness);

        // Film grain — pairs with PSX dithering
        _filmGrain.intensity.Override(_grainIntensity);
        _filmGrain.type.Override(FilmGrainLookup.Medium3);
    }

    // ═══════════════════════════════════════
    //  Dust motes
    // ═══════════════════════════════════════

    private void BuildDustParticles()
    {
        var dustGO = new GameObject("DustMotes");
        dustGO.transform.SetParent(transform, false);
        // Center dust on the apartment (slightly above floor)
        dustGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        _dustPS = dustGO.AddComponent<ParticleSystem>();

        // Stop auto-play so we can configure first
        _dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _dustPS.main;
        main.maxParticles = _dustMaxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 20f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
        main.startSize = new ParticleSystem.MinMaxCurve(_dustSizeMin, _dustSizeMax);
        main.startColor = _dustColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.002f; // very slight upward drift
        main.loop = true;
        main.playOnAwake = false;

        var emission = _dustPS.emission;
        emission.rateOverTime = _dustEmissionRate;

        var shape = _dustPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = _dustVolume;

        // Slow drifting noise
        var noise = _dustPS.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.02f);
        noise.frequency = 0.15f;
        noise.scrollSpeed = 0.05f;
        noise.damping = true;

        // Fade in/out over lifetime
        var colorOverLifetime = _dustPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // Material — simple unlit particle
        var renderer = dustGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            _dustMat = new Material(shader);
            _dustMat.SetColor("_BaseColor", _dustColor);
            // Additive blend so dust catches light naturally
            _dustMat.SetFloat("_Surface", 1f); // transparent
            _dustMat.SetFloat("_Blend", 1f);   // additive
            _dustMat.renderQueue = 3000;
            renderer.material = _dustMat;
        }

        _dustPS.Play();
    }

    // ═══════════════════════════════════════
    //  Public tuning API (for debug panel)
    // ═══════════════════════════════════════

    public float Saturation { get => _saturation; set { _saturation = value; ApplySettings(); } }
    public float Contrast { get => _contrast; set { _contrast = value; ApplySettings(); } }
    public float PostExposure { get => _postExposure; set { _postExposure = value; ApplySettings(); } }
    public float BloomThreshold { get => _bloomThreshold; set { _bloomThreshold = value; ApplySettings(); } }
    public float BloomIntensity { get => _bloomIntensity; set { _bloomIntensity = value; ApplySettings(); } }
    public float BloomScatter { get => _bloomScatter; set { _bloomScatter = value; ApplySettings(); } }
    public float VignetteIntensity { get => _vignetteIntensity; set { _vignetteIntensity = value; ApplySettings(); } }
    public float GrainIntensity { get => _grainIntensity; set { _grainIntensity = value; ApplySettings(); } }
    public Color ShadowTint { get => _shadowTint; set { _shadowTint = value; ApplySettings(); } }
    public Color HighlightTint { get => _highlightTint; set { _highlightTint = value; ApplySettings(); } }
}
