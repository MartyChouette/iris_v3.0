using UnityEngine;
using System.Collections;

/// <summary>
/// Central audio hub for Iris: owns persistent AudioSources for common mix lanes.
/// Survives scene loads via DontDestroyOnLoad.
/// Volume levels driven by AccessibilitySettings (master + per-channel).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Channels (2D)")]
    public AudioSource sfxSource;
    public AudioSource ambienceSource;
    public AudioSource weatherSource;
    public AudioSource musicSource;
    public AudioSource environmentSource;
    public AudioSource uiSource;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool _wasDetachedToRoot = false;

    // Cached per-channel volumes (applied on top of base volume args)
    private float _masterVol = 1f;
    private float _sfxVol    = 1f;
    private float _musicVol  = 1f;
    private float _ambVol    = 1f;
    private float _uiVol     = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null, worldPositionStays: true);
            _wasDetachedToRoot = true;
        }

        DontDestroyOnLoad(gameObject);

        EnsureSource(ref sfxSource, "Audio_SFX");
        EnsureSource(ref ambienceSource, "Audio_Ambience");
        EnsureSource(ref weatherSource, "Audio_Weather");
        EnsureSource(ref musicSource, "Audio_Music");
        EnsureSource(ref environmentSource, "Audio_Environment");
        EnsureSource(ref uiSource, "Audio_UI");

        AccessibilitySettings.OnSettingsChanged += ApplyVolumeSettings;
        ApplyVolumeSettings();
    }

    private void OnDestroy()
    {
        AccessibilitySettings.OnSettingsChanged -= ApplyVolumeSettings;
        if (Instance == this)
            Instance = null;
    }

    // ─── Volume Integration ──────────────────────────────────────

    /// <summary>
    /// Reads master + per-channel volumes from AccessibilitySettings
    /// and applies them to looping sources.
    /// </summary>
    private void ApplyVolumeSettings()
    {
        _masterVol = AccessibilitySettings.MasterVolume;
        _sfxVol    = AccessibilitySettings.SFXVolume;
        _musicVol  = AccessibilitySettings.MusicVolume;
        _ambVol    = AccessibilitySettings.AmbienceVolume;
        _uiVol     = AccessibilitySettings.UIVolume;

        // Update looping source volumes live
        if (IsValid(musicSource) && musicSource.isPlaying)
            musicSource.volume = _masterVol * _musicVol;
        if (IsValid(ambienceSource) && ambienceSource.isPlaying)
            ambienceSource.volume = _masterVol * _ambVol;
        if (IsValid(weatherSource) && weatherSource.isPlaying)
            weatherSource.volume = _masterVol * _ambVol;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void EnsureSource(ref AudioSource src, string childName)
    {
        if (src != null)
            return;

        Transform child = transform.Find(childName);
        if (child != null)
            src = child.GetComponent<AudioSource>();

        if (src == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
        }

        if (src.transform.parent != transform)
            src.transform.SetParent(transform, worldPositionStays: true);
    }

    private static bool IsValid(AudioSource src) => src != null;

    private static void ShowCaption(string caption, float duration = 2f)
    {
        if (string.IsNullOrEmpty(caption)) return;
        if (!AccessibilitySettings.CaptionsEnabled) return;
        CaptionDisplay.Show(caption, duration);
    }

    // ─── One-shot channels ───────────────────────────────────────

    public void PlaySFX(AudioClip clip, float volume = 1f, string caption = null)
    {
        if (clip == null || !IsValid(sfxSource)) return;
        sfxSource.PlayOneShot(clip, volume * _masterVol * _sfxVol);
        if (debugLogs) Debug.Log("[AudioManager] SFX: " + clip.name, this);
        ShowCaption(caption);
    }

    public void PlayEnvironment(AudioClip clip, float volume = 1f, string caption = null)
    {
        if (clip == null || !IsValid(environmentSource)) return;
        environmentSource.PlayOneShot(clip, volume * _masterVol * _sfxVol);
        ShowCaption(caption);
    }

    public void PlayUI(AudioClip clip, float volume = 1f, string caption = null)
    {
        if (clip == null || !IsValid(uiSource)) return;
        uiSource.PlayOneShot(clip, volume * _masterVol * _uiVol);
        ShowCaption(caption);
    }

    // ─── Ambience ────────────────────────────────────────────────

    public void PlayAmbience(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(ambienceSource)) return;
        ambienceSource.clip = clip;
        ambienceSource.volume = volume * _masterVol * _ambVol;
        ambienceSource.loop = loop;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        if (!IsValid(ambienceSource)) return;
        ambienceSource.Stop();
    }

    // ─── Weather ─────────────────────────────────────────────────

    public void PlayWeather(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(weatherSource)) return;
        weatherSource.clip = clip;
        weatherSource.volume = volume * _masterVol * _ambVol;
        weatherSource.loop = loop;
        weatherSource.Play();
    }

    public void StopWeather()
    {
        if (!IsValid(weatherSource)) return;
        weatherSource.Stop();
    }

    // ─── Music ───────────────────────────────────────────────────

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(musicSource)) return;
        musicSource.clip = clip;
        musicSource.volume = volume * _masterVol * _musicVol;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic(float fadeTime = 0f)
    {
        if (!IsValid(musicSource)) return;

        if (fadeTime <= 0f)
        {
            musicSource.Stop();
        }
        else
        {
            StartCoroutine(FadeOutMusic(fadeTime));
        }
    }

    private IEnumerator FadeOutMusic(float time)
    {
        if (!IsValid(musicSource))
            yield break;

        float start = musicSource.volume;
        float t = 0f;

        while (t < time && IsValid(musicSource))
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, 0f, t / time);
            yield return null;
        }

        if (IsValid(musicSource))
        {
            musicSource.Stop();
            musicSource.volume = start;
        }
    }

    // ─── Dual SFX ────────────────────────────────────────────────

    public void PlayDualSFX(AudioClip first, AudioClip second, float delay)
    {
        if (!IsValid(sfxSource)) return;
        if (first == null && second == null) return;
        StartCoroutine(PlayDualRoutine(first, second, delay));
    }

    private IEnumerator PlayDualRoutine(AudioClip first, AudioClip second, float delay)
    {
        if (!IsValid(sfxSource))
            yield break;

        if (first != null)
            sfxSource.PlayOneShot(first, _masterVol * _sfxVol);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsValid(sfxSource))
            yield break;

        if (second != null)
            sfxSource.PlayOneShot(second, _masterVol * _sfxVol);
    }
}
