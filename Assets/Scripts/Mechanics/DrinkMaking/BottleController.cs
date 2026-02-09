using UnityEngine;

/// <summary>
/// Represents a selectable bottle on the counter. The player clicks to select,
/// and the <see cref="DrinkMakingManager"/> delegates pour commands.
/// No InputActions here — all input handled by the manager.
/// </summary>
[DisallowMultipleComponent]
public class BottleController : MonoBehaviour
{
    public enum State { Idle, Selected, Pouring }

    [Header("Data")]
    [Tooltip("The ingredient this bottle contains.")]
    public DrinkIngredientDefinition ingredient;

    [Tooltip("Renderer used for selection highlight.")]
    public Renderer bottleRenderer;

    [Header("Visual")]
    [Tooltip("Colour multiplied onto the bottle when selected.")]
    public Color selectedTint = new Color(1f, 1f, 0.5f);

    [Tooltip("Rotation angle when pouring.")]
    public float tiltAngle = 45f;

    [Header("Audio")]
    [Tooltip("Looping pour sound effect.")]
    public AudioClip pourSFX;

    [Header("Runtime (Read-Only)")]
    public State currentState = State.Idle;

    private Color _originalColor;
    private Quaternion _originalRotation;
    private MaterialPropertyBlock _mpb;

    void Awake()
    {
        _originalRotation = transform.localRotation;
        _mpb = new MaterialPropertyBlock();

        if (bottleRenderer != null)
        {
            bottleRenderer.GetPropertyBlock(_mpb);
            _originalColor = _mpb.GetColor("_BaseColor");
            if (_originalColor == Color.clear)
            {
                // Fallback: read from shared material
                _originalColor = bottleRenderer.sharedMaterial != null
                    ? bottleRenderer.sharedMaterial.color
                    : Color.white;
            }
        }
    }

    /// <summary>Highlight the bottle as selected.</summary>
    public void Select()
    {
        currentState = State.Selected;
        ApplyTint(_originalColor * selectedTint);
    }

    /// <summary>Remove highlight.</summary>
    public void Deselect()
    {
        currentState = State.Idle;
        ApplyTint(_originalColor);
        transform.localRotation = _originalRotation;
    }

    /// <summary>Tilt the bottle and play pour SFX.</summary>
    public void StartPour()
    {
        currentState = State.Pouring;
        transform.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, tiltAngle);

        if (AudioManager.Instance != null && pourSFX != null)
            AudioManager.Instance.PlaySFX(pourSFX);
    }

    /// <summary>Return to upright and stop SFX.</summary>
    public void StopPour()
    {
        currentState = State.Selected;
        transform.localRotation = _originalRotation;
    }

    private void ApplyTint(Color color)
    {
        if (bottleRenderer == null) return;
        _mpb.SetColor("_BaseColor", color);
        bottleRenderer.SetPropertyBlock(_mpb);
    }
}
