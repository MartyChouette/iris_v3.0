using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton managing interactive fridge magnets.
/// Click + drag on FridgeMagnets layer to rearrange magnets on the fridge door.
/// </summary>
public class FridgeMagnetManager : MonoBehaviour
{
    public static FridgeMagnetManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The fridge door surface magnets snap to.")]
    [SerializeField] private FridgeMagnetSurface _surface;

    [Tooltip("Layer mask for magnet raycasting.")]
    [SerializeField] private LayerMask _magnetLayerMask;

    [Header("Settings")]
    [Tooltip("Maximum raycast distance for magnet picking.")]
    [SerializeField] private float _maxRayDistance = 10f;

    private InputAction _clickAction;
    private InputAction _pointerAction;

    private Transform _heldMagnet;
    private Camera _cachedCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[FridgeMagnetManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("MagnetClick", InputActionType.Button, "<Mouse>/leftButton");
        _pointerAction = new InputAction("MagnetPointer", InputActionType.Value, "<Mouse>/position");
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _pointerAction.Enable();
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _pointerAction.Disable();
    }

    private void OnDestroy()
    {
        _clickAction?.Dispose();
        _pointerAction?.Dispose();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_cachedCamera == null) _cachedCamera = Camera.main;
        if (_cachedCamera == null || _surface == null) return;

        Vector2 mousePos = _pointerAction.ReadValue<Vector2>();

        if (_clickAction.WasPressedThisFrame())
        {
            TryPickMagnet(mousePos);
        }

        if (_heldMagnet != null)
        {
            DragMagnet(mousePos);

            if (_clickAction.WasReleasedThisFrame())
            {
                ReleaseMagnet();
            }
        }
    }

    private void TryPickMagnet(Vector2 screenPos)
    {
        Ray ray = _cachedCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _magnetLayerMask))
        {
            _heldMagnet = hit.transform;
            Debug.Log($"[FridgeMagnetManager] Picked up: {_heldMagnet.name}");
        }
    }

    private void DragMagnet(Vector2 screenPos)
    {
        if (_heldMagnet == null) return;

        Ray ray = _cachedCamera.ScreenPointToRay(screenPos);

        // Project ray onto surface plane
        Plane surfacePlane = new Plane(_surface.transform.forward, _surface.transform.position);
        if (surfacePlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            _heldMagnet.position = _surface.ProjectOntoSurface(worldPoint);
        }
    }

    private void ReleaseMagnet()
    {
        if (_heldMagnet != null)
        {
            Debug.Log($"[FridgeMagnetManager] Released: {_heldMagnet.name}");
            _heldMagnet = null;
        }
    }

    /// <summary>Get current magnet positions for saving.</summary>
    public List<MagnetPositionRecord> GetMagnetPositions()
    {
        var records = new List<MagnetPositionRecord>();
        foreach (Transform child in transform)
        {
            var local = _surface != null
                ? _surface.transform.InverseTransformPoint(child.position)
                : child.localPosition;

            records.Add(new MagnetPositionRecord
            {
                magnetId = child.name,
                posX = local.x,
                posY = local.y
            });
        }
        return records;
    }

    /// <summary>Restore magnet positions from save data.</summary>
    public void RestoreMagnetPositions(List<MagnetPositionRecord> records)
    {
        if (records == null || _surface == null) return;

        foreach (var record in records)
        {
            foreach (Transform child in transform)
            {
                if (child.name == record.magnetId)
                {
                    Vector3 local = new Vector3(record.posX, record.posY, 0f);
                    child.position = _surface.transform.TransformPoint(local);
                    break;
                }
            }
        }
    }
}
