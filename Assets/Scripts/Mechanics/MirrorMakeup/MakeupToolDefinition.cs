using UnityEngine;

/// <summary>
/// Defines a single makeup tool (foundation, lipstick, eyeliner, star sticker)
/// with brush properties and smearing behaviour.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Makeup Tool")]
public class MakeupToolDefinition : ScriptableObject
{
    public enum ToolType { Foundation, Lipstick, Eyeliner, StarSticker }

    [Header("Identity")]
    [Tooltip("Display name shown in HUD.")]
    public string toolName;

    [Tooltip("What kind of tool this is — drives painting behaviour.")]
    public ToolType toolType;

    [Header("Brush")]
    [Tooltip("Paint colour applied to the face.")]
    public Color brushColor = Color.white;

    [Tooltip("UV-space radius of the brush (0-1).")]
    public float brushRadius = 0.03f;

    [Tooltip("Alpha strength per stamp (0-1).")]
    [Range(0f, 1f)]
    public float opacity = 0.8f;

    [Tooltip("Smooth falloff at brush edge (foundation=true, eyeliner=false).")]
    public bool softEdge = true;

    [Header("Smearing")]
    [Tooltip("Whether fast dragging causes paint to smear (lipstick only).")]
    public bool canSmear;

    [Tooltip("UV-delta/frame above which smearing kicks in.")]
    public float smearSpeedThreshold = 0.002f;

    [Tooltip("Brush radius multiplier during fast drag.")]
    public float smearWidthMultiplier = 2.5f;

    [Tooltip("Reduced opacity during smear strokes (0-1).")]
    [Range(0f, 1f)]
    public float smearOpacityFalloff = 0.4f;

    [Header("Star Sticker")]
    [Tooltip("UV radius of star shape (for StarSticker type).")]
    public float starSize = 0.025f;

    [Tooltip("Star sticker colour.")]
    public Color starColor = Color.yellow;
}
