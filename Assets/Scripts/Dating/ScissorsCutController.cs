using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ScissorsCutController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The newspaper surface for stamping cuts.")]
    [SerializeField] private NewspaperSurface surface;

    [Tooltip("Camera used for raycasting.")]
    [SerializeField] private Camera cam;

    [Tooltip("Layer mask for the newspaper surface.")]
    [SerializeField] private LayerMask newspaperLayer;

    [Tooltip("3D scissors model/sprite that follows the cut path.")]
    [SerializeField] private Transform scissorsVisual;

    [Header("Cut Path")]
    [Tooltip("Minimum world distance between path points.")]
    [SerializeField] private float minPointDistance = 0.005f;

    [Tooltip("Max total path length before auto-close.")]
    [SerializeField] private float maxPathLength = 5f;

    [Tooltip("Color of the cut line.")]
    [SerializeField] private Color cutLineColor = Color.black;

    [Header("Scissors Follow")]
    [Tooltip("Scissors move at this multiplier of player draw speed.")]
    [SerializeField] private float scissorsSpeedMultiplier = 1.3f;

    [Tooltip("Minimum scissors speed (world units/sec).")]
    [SerializeField] private float scissorsMinSpeed = 0.1f;

    [Tooltip("Extra speed when scissors are far behind.")]
    [SerializeField] private float scissorsCatchUpSpeed = 2.0f;

    [Tooltip("Distance threshold where catchup kicks in.")]
    [SerializeField] private float catchUpDistance = 0.1f;

    [Header("Close Tolerance")]
    [Tooltip("Max UV distance from end to start to close the polygon.")]
    [SerializeField] private float closeTolerance = 0.05f;

    [Header("Audio")]
    [Tooltip("Looping cut sound while scissors move.")]
    [SerializeField] private AudioClip cutLoopSFX;

    [Tooltip("Played when loop closes.")]
    [SerializeField] private AudioClip cutCompleteSFX;

    [Header("Events")]
    [Tooltip("Fires with UV polygon points when cut loop closes.")]
    public UnityEvent<List<Vector2>> OnCutComplete;

    // Inline input actions
    private InputAction _mousePosition;
    private InputAction _mouseClick;

    // State
    private bool _isEnabled;
    private bool _isDrawing;
    private List<Vector3> _pathWorldPoints = new List<Vector3>();
    private List<Vector2> _pathUVPoints = new List<Vector2>();
    private float _drawnArcLength;
    private float _scissorsArcLength;
    private int _scissorsLastCutIndex;
    private float _prevFrameDrawnArc;
    private LineRenderer _lineRenderer;

    // Pre-computed cumulative arc lengths for each point
    private List<float> _cumulativeArcLengths = new List<float>();

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        _mousePosition = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");
        _mouseClick = new InputAction("MouseClick", InputActionType.Button, "<Mouse>/leftButton");

        // Create LineRenderer for cut line visualization
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;
        _lineRenderer.startWidth = 0.002f;
        _lineRenderer.endWidth = 0.002f;
        _lineRenderer.numCornerVertices = 4;
        _lineRenderer.numCapVertices = 4;

        var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
        lineMat.color = cutLineColor;
        _lineRenderer.sharedMaterial = lineMat;

        if (scissorsVisual != null)
            scissorsVisual.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _mousePosition.Enable();
        _mouseClick.Enable();
    }

    private void OnDisable()
    {
        _mousePosition.Disable();
        _mouseClick.Disable();
    }

    private void Update()
    {
        if (!_isEnabled) return;

        if (_isDrawing)
        {
            UpdateDrawing();
            UpdateScissorsFollow();

            if (_mouseClick.WasReleasedThisFrame())
                FinishDrawing();
        }
        else
        {
            if (_mouseClick.WasPressedThisFrame())
                TryStartDrawing();
        }
    }

    // ─── Public API ───────────────────────────────────────────────

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (!enabled)
        {
            if (_isDrawing)
                CancelDrawing();

            if (scissorsVisual != null)
                scissorsVisual.gameObject.SetActive(false);
        }
    }

    // ─── Drawing ──────────────────────────────────────────────────

    private void TryStartDrawing()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, newspaperLayer))
            return;

        _isDrawing = true;
        _pathWorldPoints.Clear();
        _pathUVPoints.Clear();
        _cumulativeArcLengths.Clear();
        _drawnArcLength = 0f;
        _scissorsArcLength = 0f;
        _scissorsLastCutIndex = 0;
        _prevFrameDrawnArc = 0f;

        Vector3 worldPoint = hit.point + hit.normal * 0.001f;
        _pathWorldPoints.Add(worldPoint);
        _pathUVPoints.Add(hit.textureCoord);
        _cumulativeArcLengths.Add(0f);

        _lineRenderer.positionCount = 1;
        _lineRenderer.SetPosition(0, worldPoint);

        if (scissorsVisual != null)
        {
            scissorsVisual.position = worldPoint;
            scissorsVisual.gameObject.SetActive(true);
        }

        // Play cut loop SFX
        if (cutLoopSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(cutLoopSFX);
    }

    private void UpdateDrawing()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, newspaperLayer))
            return;

        Vector3 worldPoint = hit.point + hit.normal * 0.001f;
        Vector3 lastPoint = _pathWorldPoints[_pathWorldPoints.Count - 1];
        float dist = Vector3.Distance(worldPoint, lastPoint);

        if (dist < minPointDistance)
            return;

        _prevFrameDrawnArc = _drawnArcLength;
        _drawnArcLength += dist;
        _pathWorldPoints.Add(worldPoint);
        _pathUVPoints.Add(hit.textureCoord);
        _cumulativeArcLengths.Add(_drawnArcLength);

        _lineRenderer.positionCount = _pathWorldPoints.Count;
        _lineRenderer.SetPosition(_pathWorldPoints.Count - 1, worldPoint);

        // Auto-close if path too long
        if (_drawnArcLength >= maxPathLength)
            FinishDrawing();
    }

    private void UpdateScissorsFollow()
    {
        if (_pathWorldPoints.Count < 2) return;

        // Calculate player draw speed this frame
        float drawDelta = _drawnArcLength - _prevFrameDrawnArc;
        float playerSpeed = drawDelta / Mathf.Max(Time.deltaTime, 0.001f);

        // Advance scissors
        float scissorsSpeed = Mathf.Max(playerSpeed * scissorsSpeedMultiplier, scissorsMinSpeed);

        float gap = _drawnArcLength - _scissorsArcLength;
        if (gap > catchUpDistance)
            scissorsSpeed += scissorsCatchUpSpeed;

        _scissorsArcLength += scissorsSpeed * Time.deltaTime;
        _scissorsArcLength = Mathf.Min(_scissorsArcLength, _drawnArcLength);

        // Interpolate position along path
        Vector3 scissorsPos = InterpolateAlongPath(_scissorsArcLength, out int segmentIndex, out Vector3 direction);

        if (scissorsVisual != null)
        {
            scissorsVisual.position = scissorsPos;
            if (direction.sqrMagnitude > 0.0001f)
                scissorsVisual.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        // Stamp cut on surface up to scissors position
        if (surface != null && segmentIndex > _scissorsLastCutIndex)
        {
            surface.CutAlongPath(_pathUVPoints, _scissorsLastCutIndex, segmentIndex);
            _scissorsLastCutIndex = segmentIndex;
        }
    }

    private void FinishDrawing()
    {
        _isDrawing = false;

        if (_pathUVPoints.Count < 3)
        {
            ClearVisuals();
            return;
        }

        // Check if end is close enough to start to form a closed polygon
        Vector2 start = _pathUVPoints[0];
        Vector2 end = _pathUVPoints[_pathUVPoints.Count - 1];
        float closeDist = Vector2.Distance(start, end);

        if (closeDist <= closeTolerance)
        {
            // Close the polygon — add start point to end
            _pathUVPoints.Add(start);

            // Scissors finish remaining path
            if (surface != null)
                surface.CutAlongPath(_pathUVPoints, _scissorsLastCutIndex, _pathUVPoints.Count - 1);

            // Fill the cut area
            if (surface != null)
            {
                surface.FillCutPolygon(_pathUVPoints);
                surface.SpawnCutPiece(_pathUVPoints);
            }

            // Play complete SFX
            if (cutCompleteSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(cutCompleteSFX);

            Debug.Log($"[ScissorsCutController] Closed polygon with {_pathUVPoints.Count} points.");
            OnCutComplete?.Invoke(new List<Vector2>(_pathUVPoints));
        }
        else
        {
            // Open cut — cosmetic damage only, stamp remaining path
            if (surface != null)
                surface.CutAlongPath(_pathUVPoints, _scissorsLastCutIndex, _pathUVPoints.Count - 1);

            Debug.Log("[ScissorsCutController] Open cut — no polygon formed.");
        }

        ClearVisuals();
    }

    private void CancelDrawing()
    {
        _isDrawing = false;
        _pathWorldPoints.Clear();
        _pathUVPoints.Clear();
        _cumulativeArcLengths.Clear();
        ClearVisuals();
    }

    private void ClearVisuals()
    {
        _lineRenderer.positionCount = 0;

        if (scissorsVisual != null)
            scissorsVisual.gameObject.SetActive(false);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private Vector3 InterpolateAlongPath(float arcLength, out int segmentIndex, out Vector3 direction)
    {
        segmentIndex = 0;
        direction = Vector3.forward;

        if (_pathWorldPoints.Count < 2)
        {
            return _pathWorldPoints.Count > 0 ? _pathWorldPoints[0] : Vector3.zero;
        }

        // Find which segment the arc length falls in
        for (int i = 1; i < _cumulativeArcLengths.Count; i++)
        {
            if (arcLength <= _cumulativeArcLengths[i])
            {
                segmentIndex = i - 1;
                float segStart = _cumulativeArcLengths[i - 1];
                float segEnd = _cumulativeArcLengths[i];
                float segLen = segEnd - segStart;
                float t = segLen > 0f ? (arcLength - segStart) / segLen : 0f;

                direction = (_pathWorldPoints[i] - _pathWorldPoints[i - 1]).normalized;
                return Vector3.Lerp(_pathWorldPoints[i - 1], _pathWorldPoints[i], t);
            }
        }

        // Past the end — return last point
        segmentIndex = _pathWorldPoints.Count - 2;
        if (_pathWorldPoints.Count >= 2)
            direction = (_pathWorldPoints[_pathWorldPoints.Count - 1] - _pathWorldPoints[_pathWorldPoints.Count - 2]).normalized;

        return _pathWorldPoints[_pathWorldPoints.Count - 1];
    }
}
