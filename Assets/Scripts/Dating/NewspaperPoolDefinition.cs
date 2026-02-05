using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Newspaper Pool")]
public class NewspaperPoolDefinition : ScriptableObject
{
    [Header("Ad Pools")]
    [Tooltip("Full pool of personal ads to draw from each day.")]
    public List<DatePersonalDefinition> personalAds = new List<DatePersonalDefinition>();

    [Tooltip("Full pool of commercial ads to draw from each day.")]
    public List<CommercialAdDefinition> commercialAds = new List<CommercialAdDefinition>();

    [Header("Daily Layout")]
    [Tooltip("How many personal ads appear each day.")]
    public int personalAdsPerDay = 4;

    [Tooltip("How many commercial ads appear each day.")]
    public int commercialAdsPerDay = 3;

    [Header("Repeat Rules")]
    [Tooltip("Can the same ad appear on consecutive days?")]
    public bool allowRepeats;

    [Header("Newspaper")]
    [Tooltip("Title displayed at the top of the newspaper.")]
    public string newspaperTitle = "The Daily Bloom";
}
