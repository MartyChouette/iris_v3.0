using UnityEngine;

/// <summary>
/// Stores camera positions for each date phase so the camera can frame
/// Nema and the date character nicely during transitions.
/// Positions are captured in the editor via Inspector buttons.
/// During date phase transitions, ApartmentManager lerps to these positions.
/// </summary>
public class DateCameraFraming : MonoBehaviour
{
    public static DateCameraFraming Instance { get; private set; }

    [System.Serializable]
    public struct PhaseFrame
    {
        public string label;
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
        [HideInInspector] public bool captured;
    }

    [Header("Phase Camera Positions")]
    [Tooltip("Camera framing for each date phase. Capture via Inspector buttons.")]
    public PhaseFrame arrival = new() { label = "Arrival (Entrance)" };
    public PhaseFrame kitchen = new() { label = "Kitchen (Drinks)" };
    public PhaseFrame couch   = new() { label = "Couch (Warming Up)" };

    [Header("Transition")]
    [Tooltip("Seconds to lerp the camera to the phase position.")]
    [SerializeField] private float _transitionDuration = 1.2f;

    private Coroutine _lerpRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Lerp the apartment camera to the framing for the given date phase.
    /// Call after fade-in so the player sees the smooth move.
    /// </summary>
    public void FramePhase(DateSessionManager.DatePhase phase)
    {
        PhaseFrame frame = phase switch
        {
            DateSessionManager.DatePhase.Arrival => arrival,
            DateSessionManager.DatePhase.BackgroundJudging => kitchen,
            DateSessionManager.DatePhase.Reveal => couch,
            _ => default
        };

        if (!frame.captured) return;
        if (ApartmentManager.Instance == null) return;

        if (_lerpRoutine != null) StopCoroutine(_lerpRoutine);
        _lerpRoutine = StartCoroutine(LerpToFrame(frame));
    }

    /// <summary>Immediately snap to a phase frame (no lerp). Use during fade-to-black.</summary>
    public void SnapToPhase(DateSessionManager.DatePhase phase)
    {
        PhaseFrame frame = phase switch
        {
            DateSessionManager.DatePhase.Arrival => arrival,
            DateSessionManager.DatePhase.BackgroundJudging => kitchen,
            DateSessionManager.DatePhase.Reveal => couch,
            _ => default
        };

        if (!frame.captured) return;
        ApartmentManager.Instance?.SetPresetBase(
            frame.position,
            Quaternion.Euler(frame.rotation),
            frame.fov);
    }

    /// <summary>Release camera back to normal apartment browsing.</summary>
    public void Release()
    {
        if (_lerpRoutine != null) { StopCoroutine(_lerpRoutine); _lerpRoutine = null; }
        ApartmentManager.Instance?.ClearPresetBase();
    }

    private System.Collections.IEnumerator LerpToFrame(PhaseFrame frame)
    {
        var am = ApartmentManager.Instance;
        if (am == null) yield break;

        // Read current camera state as start
        var cam = Camera.main;
        if (cam == null) yield break;

        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        float startFov = cam.fieldOfView;

        Vector3 endPos = frame.position;
        Quaternion endRot = Quaternion.Euler(frame.rotation);
        float endFov = frame.fov;

        float elapsed = 0f;
        while (elapsed < _transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _transitionDuration);

            am.SetPresetBase(
                Vector3.Lerp(startPos, endPos, t),
                Quaternion.Slerp(startRot, endRot, t),
                Mathf.Lerp(startFov, endFov, t));

            yield return null;
        }

        am.SetPresetBase(endPos, endRot, endFov);
        _lerpRoutine = null;
    }
}
