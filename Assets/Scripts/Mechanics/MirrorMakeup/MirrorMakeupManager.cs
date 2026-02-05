using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton for the mirror makeup prototype.
/// Freeform tool selection — no rigid FSM. Handles input and delegates
/// painting to <see cref="FaceCanvas"/>.
/// </summary>
[DisallowMultipleComponent]
public class MirrorMakeupManager : MonoBehaviour
{
    public static MirrorMakeupManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The face painting surface.")]
    [SerializeField] private FaceCanvas _faceCanvas;

    [Tooltip("Head rotation controller.")]
    [SerializeField] private HeadController _headController;

    [Tooltip("Main camera for raycasts.")]
    [SerializeField] private Camera _mainCamera;

    [Tooltip("HUD display.")]
    [SerializeField] private MirrorMakeupHUD _hud;

    [Header("Tools")]
    [Tooltip("All available makeup tools.")]
    [SerializeField] private MakeupToolDefinition[] _tools;

    [SerializeField] private int _selectedToolIndex = -1;

    [Header("Painting")]
    [Tooltip("Layer mask for raycasting onto the face quad.")]
    [SerializeField] private LayerMask _faceLayer;

    [Header("Audio")]
    public AudioClip paintSFX;
    public AudioClip stickerSFX;
    public AudioClip smearSFX;

    // Input
    private InputAction _clickAction;
    private InputAction _mousePosition;

    // Painting state
    private Vector2 _previousUV;
    private bool _wasPaintingLastFrame;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>Currently selected tool, or null if in inspect mode.</summary>
    public MakeupToolDefinition ActiveTool =>
        (_selectedToolIndex >= 0 && _selectedToolIndex < _tools.Length) ? _tools[_selectedToolIndex] : null;

    /// <summary>All available tools.</summary>
    public MakeupToolDefinition[] Tools => _tools;

    /// <summary>Index of the selected tool (-1 = inspect).</summary>
    public int SelectedToolIndex => _selectedToolIndex;

    /// <summary>True while actively painting on the face.</summary>
    public bool IsPainting { get; private set; }

    /// <summary>The face canvas reference.</summary>
    public FaceCanvas Canvas => _faceCanvas;

    /// <summary>The head controller reference.</summary>
    public HeadController Head => _headController;

    /// <summary>Select a tool by index. Called by HUD buttons.</summary>
    public void SelectTool(int index)
    {
        _selectedToolIndex = index;
        Debug.Log($"[MirrorMakeupManager] Selected tool: {(ActiveTool != null ? ActiveTool.toolName : "None")}");
    }

    /// <summary>Deselect tool and return to inspect mode.</summary>
    public void DeselectTool()
    {
        _selectedToolIndex = -1;
        Debug.Log("[MirrorMakeupManager] Deselected tool (inspect mode)");
    }

    // ── Singleton lifecycle ─────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("MakeupClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePosition = new InputAction("MakeupPointer", InputActionType.Value, "<Mouse>/position");

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        _clickAction.Enable();
        _mousePosition.Enable();
    }

    void OnDisable()
    {
        _clickAction.Disable();
        _mousePosition.Disable();
    }

    // ── Update ──────────────────────────────────────────────────────

    void Update()
    {
        if (_mainCamera == null || _faceCanvas == null) return;

        var tool = ActiveTool;
        if (tool == null)
        {
            IsPainting = false;
            _wasPaintingLastFrame = false;
            return;
        }

        Vector2 pointer = _mousePosition.ReadValue<Vector2>();

        // StarSticker: stamp on click
        if (tool.toolType == MakeupToolDefinition.ToolType.StarSticker)
        {
            IsPainting = false;
            if (_clickAction.WasPressedThisFrame())
            {
                if (TryGetFaceUV(pointer, out Vector2 uv))
                {
                    _faceCanvas.StampStar(uv, tool.starSize, tool.starColor);
                    if (AudioManager.Instance != null && stickerSFX != null)
                        AudioManager.Instance.PlaySFX(stickerSFX);
                }
            }
            _wasPaintingLastFrame = false;
            return;
        }

        // Continuous painting tools (Foundation, Lipstick, Eyeliner)
        if (_clickAction.IsPressed())
        {
            if (TryGetFaceUV(pointer, out Vector2 uv))
            {
                IsPainting = true;

                if (_wasPaintingLastFrame)
                {
                    Vector2 dragDelta = uv - _previousUV;
                    float dragSpeed = dragDelta.magnitude;

                    // Smear check (lipstick)
                    if (tool.canSmear && dragSpeed > tool.smearSpeedThreshold)
                    {
                        float smearRadius = tool.brushRadius * tool.smearWidthMultiplier;
                        float smearOpacity = tool.opacity * tool.smearOpacityFalloff;

                        _faceCanvas.Paint(uv, tool.brushColor, smearRadius, smearOpacity, tool.softEdge);
                        _faceCanvas.Smear(uv, dragDelta.normalized, smearRadius);

                        if (AudioManager.Instance != null && smearSFX != null)
                            AudioManager.Instance.PlaySFX(smearSFX);
                    }
                    else
                    {
                        _faceCanvas.Paint(uv, tool.brushColor, tool.brushRadius, tool.opacity, tool.softEdge);

                        if (AudioManager.Instance != null && paintSFX != null)
                            AudioManager.Instance.PlaySFX(paintSFX);
                    }
                }
                else
                {
                    // First frame of drag
                    _faceCanvas.Paint(uv, tool.brushColor, tool.brushRadius, tool.opacity, tool.softEdge);
                }

                _previousUV = uv;
                _wasPaintingLastFrame = true;
            }
            else
            {
                IsPainting = false;
                _wasPaintingLastFrame = false;
            }
        }
        else
        {
            IsPainting = false;
            _wasPaintingLastFrame = false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool TryGetFaceUV(Vector2 screenPos, out Vector2 uv)
    {
        uv = Vector2.zero;
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _faceLayer))
        {
            uv = hit.textureCoord;
            return true;
        }
        return false;
    }
}
