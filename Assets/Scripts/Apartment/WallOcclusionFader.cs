using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Raycasts from the camera toward key targets (date NPC, held object, area focus).
/// Renderers hit that use the PSXLitDissolvable shader get their _DissolveAmount
/// driven up via MaterialPropertyBlock. Respects AlwaysFadedWall base dissolve floor.
/// </summary>
public class WallOcclusionFader : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────
    public static WallOcclusionFader Instance { get; private set; }

    [Header("Raycast Settings")]
    [Tooltip("Layer mask for walls that can be faded.")]
    [SerializeField] private LayerMask _wallLayer = ~0;

    [Tooltip("Max raycast distance from camera to targets.")]
    [SerializeField] private float _maxRayDistance = 50f;

    [Tooltip("Radius of the sphere cast (0 = line cast).")]
    [SerializeField] private float _rayRadius = 0.15f;

    [Header("Fade Timing")]
    [Tooltip("Seconds to fully dissolve a wall (fade in).")]
    [SerializeField] private float _fadeInDuration = 0.2f;

    [Tooltip("Seconds for a wall to reappear (fade out).")]
    [SerializeField] private float _fadeOutDuration = 0.4f;

    [Tooltip("Target dissolve amount when occluding (0-1).")]
    [SerializeField] private float _targetDissolve = 0.85f;

    // ── Internals ────────────────────────────────────────────────────
    private Camera _cam;
    private readonly Dictionary<Renderer, FadeState> _trackedRenderers = new();
    private readonly List<Renderer> _removeList = new();
    private readonly List<Renderer> _keyBuffer = new();
    private readonly HashSet<Renderer> _hitThisFrame = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

    private static readonly int DissolveID = Shader.PropertyToID("_DissolveAmount");

    private struct FadeState
    {
        public float current;
        public float floor;     // AlwaysFadedWall.BaseDissolve or 0
        public MaterialPropertyBlock mpb;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        // Clean up any driven dissolve values
        foreach (var kvp in _trackedRenderers)
        {
            if (kvp.Key != null)
            {
                kvp.Value.mpb.SetFloat(DissolveID, kvp.Value.floor);
                kvp.Key.SetPropertyBlock(kvp.Value.mpb);
            }
        }
        _trackedRenderers.Clear();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        _hitThisFrame.Clear();
        Vector3 camPos = _cam.transform.position;

        // ── Gather targets ──────────────────────────────────────────
        // 1. Date NPC
        var dateMgr = DateSessionManager.Instance;
        if (dateMgr != null && dateMgr.DateCharacter != null)
            CastToTarget(camPos, dateMgr.DateCharacter.transform.position);

        // 2. Held object
        var held = ObjectGrabber.HeldObject;
        if (held != null)
            CastToTarget(camPos, held.transform.position);

        // 3. Current area focus point (camera target position)
        var apt = ApartmentManager.Instance;
        if (apt != null)
        {
            // The area definitions store the camera position; the focus is roughly
            // forward from there. Use the area camera position as a proxy target.
            int idx = apt.CurrentAreaIndex;
            // Raycast toward a point slightly in front of camera along forward
            Vector3 focusPoint = camPos + _cam.transform.forward * 5f;
            CastToTarget(camPos, focusPoint);
        }

        // ── Drive dissolve on hit renderers ─────────────────────────
        float dt = Time.deltaTime;

        foreach (var rend in _hitThisFrame)
        {
            if (!_trackedRenderers.TryGetValue(rend, out var state))
            {
                state = new FadeState
                {
                    current = GetBaseDissolve(rend),
                    floor = GetBaseDissolve(rend),
                    mpb = new MaterialPropertyBlock()
                };
                rend.GetPropertyBlock(state.mpb);
            }

            float target = Mathf.Max(_targetDissolve, state.floor);
            float speed = _fadeInDuration > 0f ? 1f / _fadeInDuration : 100f;
            state.current = Mathf.MoveTowards(state.current, target, speed * dt);
            state.mpb.SetFloat(DissolveID, state.current);
            rend.SetPropertyBlock(state.mpb);
            _trackedRenderers[rend] = state;
        }

        // ── Fade out renderers that are no longer hit ───────────────
        _removeList.Clear();
        _keyBuffer.Clear();
        foreach (var kvp in _trackedRenderers)
            _keyBuffer.Add(kvp.Key);

        for (int i = 0; i < _keyBuffer.Count; i++)
        {
            var rend = _keyBuffer[i];
            if (_hitThisFrame.Contains(rend)) continue;

            if (rend == null) { _removeList.Add(rend); continue; }

            var state = _trackedRenderers[rend];
            float speed = _fadeOutDuration > 0f ? 1f / _fadeOutDuration : 100f;
            state.current = Mathf.MoveTowards(state.current, state.floor, speed * dt);
            state.mpb.SetFloat(DissolveID, state.current);
            rend.SetPropertyBlock(state.mpb);
            _trackedRenderers[rend] = state;

            if (Mathf.Approximately(state.current, state.floor))
                _removeList.Add(rend);
        }

        foreach (var rend in _removeList)
            _trackedRenderers.Remove(rend);
    }

    // ── Raycast helper ──────────────────────────────────────────────

    private void CastToTarget(Vector3 camPos, Vector3 targetPos)
    {
        Vector3 dir = targetPos - camPos;
        float dist = dir.magnitude;
        if (dist < 0.01f || dist > _maxRayDistance) return;

        int hitCount;
        if (_rayRadius > 0f)
            hitCount = Physics.SphereCastNonAlloc(camPos, _rayRadius, dir.normalized, _hitBuffer, dist, _wallLayer, QueryTriggerInteraction.Ignore);
        else
            hitCount = Physics.RaycastNonAlloc(camPos, dir.normalized, _hitBuffer, dist, _wallLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            var rend = _hitBuffer[i].collider.GetComponentInParent<Renderer>();
            if (rend == null) continue;
            if (!UsesDissolvableShader(rend)) continue;
            _hitThisFrame.Add(rend);
        }
    }

    private static bool UsesDissolvableShader(Renderer rend)
    {
        // Check shared material to avoid instantiation
        var mats = rend.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].shader.name == "Iris/PSXLitDissolvable")
                return true;
        }
        return false;
    }

    private static float GetBaseDissolve(Renderer rend)
    {
        var marker = rend.GetComponentInParent<AlwaysFadedWall>();
        return marker != null ? marker.BaseDissolve : 0f;
    }
}
