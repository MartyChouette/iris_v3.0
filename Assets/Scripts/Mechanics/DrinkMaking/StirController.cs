using UnityEngine;

/// <summary>
/// Detects circular mouse motion over the glass and evaluates stir quality.
/// Uses a ring buffer of recent positions to compute angular velocity.
/// </summary>
[DisallowMultipleComponent]
public class StirController : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Minimum mouse movement radius to count as circular (world units).")]
    public float minRadius = 0.01f;

    [Tooltip("Rolling time window in seconds for angle accumulation.")]
    public float sampleWindow = 0.5f;

    [Tooltip("Minimum angular speed (rad/sec) for 'good' stir.")]
    public float perfectSpeedMin = 1.5f;

    [Tooltip("Maximum angular speed before 'too fast'.")]
    public float perfectSpeedMax = 4f;

    [Header("State (Read-Only)")]
    [SerializeField] private float _stirQuality;
    [SerializeField] private float _sustainedTime;

    // Ring buffer
    private const int BufferSize = 64;
    private readonly Vector2[] _positions = new Vector2[BufferSize];
    private readonly float[] _timestamps = new float[BufferSize];
    private int _writeIndex;
    private int _sampleCount;
    private float _totalTime;

    // ── Public API ─────────────────────────────────────────────────────

    public float StirQuality => _stirQuality;
    public float SustainedTime => _sustainedTime;

    /// <summary>
    /// Called by <see cref="DrinkMakingManager"/> each frame during the stir phase.
    /// </summary>
    public void UpdateStir(Vector2 worldPos, float dt)
    {
        _totalTime += dt;

        // Write into ring buffer
        _positions[_writeIndex] = worldPos;
        _timestamps[_writeIndex] = _totalTime;
        _writeIndex = (_writeIndex + 1) % BufferSize;
        _sampleCount = Mathf.Min(_sampleCount + 1, BufferSize);

        if (_sampleCount < 4) return;

        // Determine window bounds
        float windowStart = _totalTime - sampleWindow;

        // Compute centroid of samples in window
        Vector2 centroid = Vector2.zero;
        int count = 0;
        for (int i = 0; i < _sampleCount; i++)
        {
            int idx = ((_writeIndex - 1 - i) % BufferSize + BufferSize) % BufferSize;
            if (_timestamps[idx] < windowStart) break;
            centroid += _positions[idx];
            count++;
        }

        if (count < 3) return;
        centroid /= count;

        // Accumulate angle swept in the window
        float totalAngle = 0f;
        float avgRadius = 0f;
        Vector2 prevDir = Vector2.zero;
        bool hasPrev = false;

        for (int i = count - 1; i >= 0; i--)
        {
            int idx = ((_writeIndex - 1 - i) % BufferSize + BufferSize) % BufferSize;
            Vector2 dir = _positions[idx] - centroid;
            float r = dir.magnitude;
            avgRadius += r;

            if (hasPrev && r > 0.0001f)
            {
                float angle = Vector2.SignedAngle(prevDir, dir) * Mathf.Deg2Rad;
                totalAngle += angle;
            }

            prevDir = dir;
            hasPrev = r > 0.0001f;
        }

        avgRadius /= count;
        float elapsed = Mathf.Max(_totalTime - (_totalTime - sampleWindow), 0.001f);
        float angularSpeed = Mathf.Abs(totalAngle) / Mathf.Min(elapsed, sampleWindow);

        // Quality evaluation
        if (avgRadius < minRadius)
        {
            _stirQuality = 0f;
        }
        else if (angularSpeed >= perfectSpeedMin && angularSpeed <= perfectSpeedMax)
        {
            _stirQuality = 1f;
        }
        else if (angularSpeed < perfectSpeedMin)
        {
            _stirQuality = Mathf.Clamp01(angularSpeed / perfectSpeedMin);
        }
        else // too fast
        {
            float overshoot = angularSpeed - perfectSpeedMax;
            _stirQuality = Mathf.Clamp01(1f - overshoot / perfectSpeedMax);
        }

        // Sustained time
        if (_stirQuality > 0.5f)
            _sustainedTime += dt;
        else
            _sustainedTime = Mathf.MoveTowards(_sustainedTime, 0f, dt * 2f);
    }

    /// <summary>Reset all stir tracking state.</summary>
    public void Reset()
    {
        _stirQuality = 0f;
        _sustainedTime = 0f;
        _writeIndex = 0;
        _sampleCount = 0;
        _totalTime = 0f;
    }
}
