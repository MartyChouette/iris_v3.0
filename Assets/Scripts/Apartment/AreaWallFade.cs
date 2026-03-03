using System;
using UnityEngine;

/// <summary>
/// Per-area dissolve control for walls using the PSXLitDissolvable shader.
/// Add to any wall renderer. For each camera area that should dissolve this
/// wall, add an entry with the area index and target dissolve amount.
/// WallOcclusionFader reads this as the dissolve floor for the current area.
/// </summary>
public class AreaWallFade : MonoBehaviour
{
    [Serializable]
    public struct AreaDissolve
    {
        [Tooltip("Area index (matches ApartmentManager.areas[] order: 0=Kitchen, 1=Living Room, 2=Entrance).")]
        public int areaIndex;

        [Tooltip("Dissolve amount when this area is active (0 = fully visible, 1 = fully dissolved).")]
        [Range(0f, 1f)]
        public float dissolveAmount;
    }

    [Tooltip("Per-area dissolve settings. Areas not listed default to 0 (fully visible).")]
    [SerializeField] private AreaDissolve[] _areaDissolves = Array.Empty<AreaDissolve>();

    /// <summary>
    /// Returns the dissolve floor for the given area index.
    /// Returns 0 if the area isn't listed.
    /// </summary>
    public float GetDissolveForArea(int areaIndex)
    {
        for (int i = 0; i < _areaDissolves.Length; i++)
        {
            if (_areaDissolves[i].areaIndex == areaIndex)
                return _areaDissolves[i].dissolveAmount;
        }
        return 0f;
    }
}
