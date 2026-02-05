using UnityEngine;

/// <summary>
/// Audio proximity guide that emits a tone whose pitch and volume change
/// based on how close the cutting plane is to the ideal stem cut height.
/// At the ideal position the tone sits at <see cref="perfectPitch"/> and max volume;
/// further away the pitch shifts and volume drops.
/// </summary>
[DisallowMultipleComponent]
public class StemSoundGuide : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The stem being cut.")]
    public FlowerStemRuntime stem;

    [Tooltip("The ideal definition that contains idealStemLength.")]
    public IdealFlowerDefinition ideal;

    [Tooltip("The cutting plane controller whose transform indicates the current cut height.")]
    public CuttingPlaneController planeController;

    [Tooltip("Dedicated AudioSource for the continuous tone. Loop should be enabled.")]
    public AudioSource toneSource;

    [Header("Pitch")]
    [Tooltip("Pitch when the tool is very far from the ideal cut point.")]
    public float minPitch = 0.5f;

    [Tooltip("Maximum pitch at the far extreme.")]
    public float maxPitch = 2.0f;

    [Tooltip("Pitch when exactly at the ideal cut height.")]
    public float perfectPitch = 1.0f;

    [Header("Volume")]
    [Tooltip("Maximum volume when close to ideal.")]
    [Range(0f, 1f)]
    public float maxVolume = 0.6f;

    [Header("Smoothing")]
    [Tooltip("SmoothDamp time for pitch and volume transitions.")]
    public float smoothTime = 0.05f;

    [Header("Behaviour")]
    [Tooltip("Only play the tone when the scissor tool is equipped.")]
    public bool onlyWhenToolEquipped = true;

    // Smooth damp state
    private float _currentPitch;
    private float _currentVolume;
    private float _pitchVelocity;
    private float _volumeVelocity;
    private bool _clipCreated;

    void Awake()
    {
        if (toneSource == null)
            toneSource = GetComponentInChildren<AudioSource>();

        // Create a procedural sine-wave clip if the source has none
        if (toneSource != null && toneSource.clip == null)
            CreateSineClip();

        _currentPitch = perfectPitch;
    }

    void Update()
    {
        if (stem == null || ideal == null || planeController == null || toneSource == null)
            return;

        // Optionally require the tool to be equipped
        if (onlyWhenToolEquipped && !planeController.IsToolEnabled)
        {
            if (toneSource.isPlaying)
                toneSource.Pause();
            return;
        }

        if (!toneSource.isPlaying)
            toneSource.UnPause();

        // Current cut height = distance from anchor to the cutting plane Y
        Transform planeTransform = planeController.planePoseRootOverride != null
            ? planeController.planePoseRootOverride
            : planeController.transform;

        float currentLength = Vector3.Distance(stem.StemAnchor.position, planeTransform.position);
        float idealLength = ideal.idealStemLength;
        float delta = currentLength - idealLength;
        float absDelta = Mathf.Abs(delta);

        // Map delta to pitch: at ideal -> perfectPitch, far -> minPitch or maxPitch
        float maxDelta = stem.CurrentLength; // full stem length as max range
        if (maxDelta < 0.001f) maxDelta = 1f;
        float t = Mathf.Clamp01(absDelta / maxDelta);
        float targetPitch = Mathf.Lerp(perfectPitch, delta > 0 ? maxPitch : minPitch, t);

        // Map delta to volume: louder when close, quieter when far
        float targetVolume = Mathf.Lerp(maxVolume, 0.05f, t);

        // Smooth
        _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _pitchVelocity, smoothTime);
        _currentVolume = Mathf.SmoothDamp(_currentVolume, targetVolume, ref _volumeVelocity, smoothTime);

        toneSource.pitch = _currentPitch;
        toneSource.volume = _currentVolume;
    }

    private void CreateSineClip()
    {
        if (_clipCreated) return;
        _clipCreated = true;

        int sampleRate = 44100;
        float frequency = 440f; // A4
        int sampleCount = sampleRate; // 1 second
        var clip = AudioClip.Create("SineWave", sampleCount, 1, sampleRate, false);
        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * 0.5f;
        }
        clip.SetData(data, 0);
        toneSource.clip = clip;
        toneSource.loop = true;
        toneSource.playOnAwake = false;
    }
}
