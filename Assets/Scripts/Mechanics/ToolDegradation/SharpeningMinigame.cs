using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sharpening minigame: the player holds a button and adjusts angle (mouse Y)
/// to grind the blade against a stone. When the angle is close to the target,
/// sharpness is restored at <see cref="sharpenRate"/> per second.
/// </summary>
[DisallowMultipleComponent]
public class SharpeningMinigame : MonoBehaviour
{
    public enum State { Idle, Sharpening }

    [Header("References")]
    [Tooltip("ToolCondition to restore sharpness on.")]
    public ToolCondition tool;

    [Header("Angle")]
    [Tooltip("Target angle for ideal sharpening contact.")]
    public float targetAngle = 45f;

    [Tooltip("Degrees of tolerance from the target.")]
    public float angleTolerance = 10f;

    [Tooltip("Tight tolerance for perfect rate (degrees from target).")]
    public float perfectTolerance = 3f;

    [Header("Rates")]
    [Tooltip("Sharpness restored per second of good contact.")]
    public float sharpenRate = 0.3f;

    [Tooltip("Sharpness restored per second of perfect contact.")]
    public float perfectSharpenRate = 0.6f;

    [Header("Visuals")]
    [Tooltip("Visual for the sharpening stone.")]
    public Transform stoneVisual;

    [Tooltip("Visual for the blade during sharpening.")]
    public Transform bladeVisual;

    [Header("Audio")]
    [Tooltip("Looping grind SFX.")]
    public AudioClip sharpenLoopSFX;

    [Tooltip("Dedicated AudioSource for the sharpening loop.")]
    public AudioSource sharpenSource;

    [Header("State")]
    public State currentState = State.Idle;

    // Input
    private InputAction _sharpenAction;
    private InputAction _angleAction;
    private InputAction _toggleAction;

    // Runtime
    private float _currentAngle;

    void Awake()
    {
        _sharpenAction = new InputAction("Sharpen", InputActionType.Button,
            "<Mouse>/leftButton");
        _angleAction = new InputAction("SharpenAngle", InputActionType.Value,
            "<Mouse>/delta/y");
        _toggleAction = new InputAction("ToggleSharpen", InputActionType.Button,
            "<Keyboard>/q");
    }

    void OnEnable()
    {
        _sharpenAction.Enable();
        _angleAction.Enable();
        _toggleAction.Enable();
    }

    void OnDisable()
    {
        _sharpenAction.Disable();
        _angleAction.Disable();
        _toggleAction.Disable();
    }

    void Update()
    {
        // Toggle between Idle and Sharpening
        if (_toggleAction.WasPressedThisFrame())
        {
            if (currentState == State.Idle)
                EnterSharpening();
            else
                ExitSharpening();
        }

        if (currentState != State.Sharpening) return;
        if (tool == null) return;

        // Read mouse delta Y to adjust blade angle
        float deltaY = _angleAction.ReadValue<float>();
        _currentAngle = Mathf.Clamp(_currentAngle + deltaY * 0.5f, 0f, 90f);

        // Rotate blade visual
        if (bladeVisual != null)
            bladeVisual.localRotation = Quaternion.Euler(_currentAngle, 0f, 0f);

        // Check if holding the sharpen button
        if (!_sharpenAction.IsPressed())
        {
            StopSharpenAudio();
            return;
        }

        float angleDelta = Mathf.Abs(_currentAngle - targetAngle);

        if (angleDelta <= angleTolerance)
        {
            float rate = angleDelta <= perfectTolerance ? perfectSharpenRate : sharpenRate;
            tool.Sharpen(rate * Time.deltaTime);
            PlaySharpenAudio();
        }
        else
        {
            StopSharpenAudio();
        }
    }

    public void EnterSharpening()
    {
        currentState = State.Sharpening;
        _currentAngle = targetAngle; // Start near ideal
        if (stoneVisual != null) stoneVisual.gameObject.SetActive(true);
        if (bladeVisual != null) bladeVisual.gameObject.SetActive(true);
        Debug.Log("[SharpeningMinigame] Entered sharpening mode.");
    }

    public void ExitSharpening()
    {
        currentState = State.Idle;
        StopSharpenAudio();
        if (stoneVisual != null) stoneVisual.gameObject.SetActive(false);
        if (bladeVisual != null) bladeVisual.gameObject.SetActive(false);
        Debug.Log("[SharpeningMinigame] Exited sharpening mode.");
    }

    private void PlaySharpenAudio()
    {
        if (sharpenSource == null || sharpenLoopSFX == null) return;
        if (!sharpenSource.isPlaying)
        {
            sharpenSource.clip = sharpenLoopSFX;
            sharpenSource.loop = true;
            sharpenSource.Play();
        }
    }

    private void StopSharpenAudio()
    {
        if (sharpenSource != null && sharpenSource.isPlaying)
            sharpenSource.Stop();
    }
}
