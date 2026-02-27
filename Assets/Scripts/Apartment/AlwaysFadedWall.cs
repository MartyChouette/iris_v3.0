using UnityEngine;

/// <summary>
/// Marker component for walls that should always be partially dissolved
/// (classic iso game wall cutaway). WallOcclusionFader uses this as a
/// minimum dissolve floor — the wall never goes below BaseDissolve.
/// </summary>
public class AlwaysFadedWall : MonoBehaviour
{
    [Tooltip("Minimum dissolve amount (0 = fully visible, 1 = fully dissolved). " +
             "The wall is always at least this dissolved.")]
    [Range(0f, 1f)]
    [SerializeField] private float _baseDissolve = 0.6f;

    public float BaseDissolve => _baseDissolve;
}
