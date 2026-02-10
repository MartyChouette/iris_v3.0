using System.Collections;
using UnityEngine;

/// <summary>
/// A perfume bottle on the bookcase shelf. Click to pick up, hold LMB to spray,
/// click to put down. Spray triggers EnvironmentMoodController color shift.
/// </summary>
public class PerfumeBottle : MonoBehaviour
{
    public enum State { OnShelf, Held, Spraying, Inspecting }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this perfume.")]
    [SerializeField] private PerfumeDefinition definition;

    [Header("Spray")]
    [Tooltip("Child ParticleSystem for the spray mist.")]
    [SerializeField] private ParticleSystem sprayParticles;

    [Tooltip("Seconds of continuous spray before mood change triggers.")]
    [SerializeField] private float sprayThreshold = 0.5f;

    public PerfumeDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnShelf;
    public bool SprayComplete { get; private set; }

    private Vector3 _shelfPosition;
    private Quaternion _shelfRotation;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;
    private float _sprayTimer;
    private bool _moodTriggeredThisSpray;

    private const float PickUpOffset = 0.05f;
    private const float LerpDuration = 0.2f;

    private void Awake()
    {
        _shelfPosition = transform.position;
        _shelfRotation = transform.rotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material;
            _baseColor = _instanceMaterial.color;
        }

        if (sprayParticles != null)
            sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void SetDefinition(PerfumeDefinition def) => definition = def;

    public void SetSprayParticles(ParticleSystem ps) => sprayParticles = ps;

    public void OnHoverEnter()
    {
        if (_isHovered || CurrentState != State.OnShelf) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;

        transform.position = _shelfPosition - transform.forward * 0.02f;
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

    /// <summary>
    /// Pick up the bottle (slight forward offset).
    /// </summary>
    public void PickUp()
    {
        if (CurrentState != State.OnShelf) return;
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        CurrentState = State.Held;
        StartCoroutine(LerpToPosition(_shelfPosition - transform.forward * PickUpOffset, _shelfRotation));
    }

    /// <summary>
    /// Put bottle back on shelf.
    /// </summary>
    public void PutDown()
    {
        CurrentState = State.OnShelf;
        SprayComplete = false;

        MoodMachine.Instance?.RemoveSource("Perfume");

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = false;

        StartCoroutine(LerpToPosition(_shelfPosition, _shelfRotation));
    }

    /// <summary>
    /// Begin spraying (called each frame LMB is held).
    /// </summary>
    public void StartSpray()
    {
        if (CurrentState == State.Held)
        {
            CurrentState = State.Spraying;
            _sprayTimer = 0f;
            _moodTriggeredThisSpray = false;
            SprayComplete = false;

            if (sprayParticles != null)
                sprayParticles.Play();

            if (definition != null && definition.spraySFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(definition.spraySFX);
        }
    }

    /// <summary>
    /// Called each frame during spray. Accumulates spray time.
    /// </summary>
    public void UpdateSpray()
    {
        if (CurrentState != State.Spraying) return;

        _sprayTimer += Time.deltaTime;

        if (_sprayTimer >= sprayThreshold && !_moodTriggeredThisSpray)
        {
            _moodTriggeredThisSpray = true;
            SprayComplete = true;

            if (definition != null && MoodMachine.Instance != null)
                MoodMachine.Instance.SetSource("Perfume", definition.moodValue);

            var reactable = GetComponent<ReactableTag>();
            if (reactable != null) reactable.IsActive = true;
        }
    }

    /// <summary>
    /// Stop spraying. Returns to Held state.
    /// </summary>
    public void StopSpray()
    {
        if (sprayParticles != null)
            sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        CurrentState = State.Held;
        SprayComplete = false;
    }

    private IEnumerator LerpToPosition(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < LerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / LerpDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
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
