using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Trinket Definition")]
public class TrinketDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name of the trinket.")]
    public string trinketName = "Unnamed Trinket";

    [TextArea(2, 4)]
    [Tooltip("Short description shown during inspection.")]
    public string description = "";

    [Tooltip("Unique ID for ItemStateRegistry tracking.")]
    public string itemID = "";

    [Header("Visuals")]
    [Tooltip("Color of the trinket object.")]
    public Color color = new Color(0.7f, 0.5f, 0.3f);

    [Tooltip("Scale multiplier for the trinket mesh.")]
    [Range(0.5f, 2f)]
    public float scale = 1f;

    [Header("Placement")]
    [Tooltip("If true, trinket starts inside a drawer rather than on display.")]
    public bool startsInDrawer = true;
}
