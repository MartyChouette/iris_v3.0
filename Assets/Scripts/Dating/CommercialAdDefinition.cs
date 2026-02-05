using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Commercial Ad")]
public class CommercialAdDefinition : ScriptableObject
{
    [Header("Business Info")]
    [Tooltip("Name of the business or advertiser.")]
    public string businessName = "Local Business";

    [TextArea(2, 5)]
    [Tooltip("The commercial ad copy displayed in the newspaper.")]
    public string adText = "Visit us today!";

    [Header("Visuals")]
    [Tooltip("Optional logo sprite for the ad.")]
    public Sprite logo;

    [Header("Layout")]
    [Tooltip("Size weight for layout variety. 1.0 = normal, 2.0 = double-wide.")]
    public float sizeWeight = 1f;
}
