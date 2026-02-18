using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class BookInteractionManager : MonoBehaviour, IStationManager
{
    public enum State
    {
        Browsing,
        Pulling,
        Reading,
        PuttingBack,
        DrawerOpening,
        DrawerOpen,
        DrawerClosing,
        Inspecting,
        MovingCoffeeBook
    }

    public static BookInteractionManager Instance { get; private set; }

    public bool IsAtIdleState => CurrentState == State.Browsing;

    [Header("References")]
    [Tooltip("Transform where the book is positioned for reading (child of camera).")]
    [SerializeField] private Transform readingAnchor;

    [Tooltip("Camera component used for screen-to-world raycasting.")]
    [SerializeField] private UnityEngine.Camera mainCamera;

    [Tooltip("BookcaseBrowseCamera for enabling/disabling look during reading.")]
    [SerializeField] private BookcaseBrowseCamera browseCamera;

    [Header("Sub-Systems")]
    [Tooltip("ItemInspector for double-click close-up viewing.")]
    [SerializeField] private ItemInspector itemInspector;

    [Tooltip("EnvironmentMoodController for perfume lighting changes.")]
    [SerializeField] private EnvironmentMoodController moodController;

    [Header("UI")]
    [Tooltip("Root panel for the title hint shown on hover.")]
    [SerializeField] private GameObject titleHintPanel;

    [Tooltip("TMP_Text for displaying the hovered item's name.")]
    [SerializeField] private TMP_Text titleHintText;

    [Header("Raycast")]
    [Tooltip("Layer mask for the Books layer.")]
    [SerializeField] private LayerMask booksLayerMask;

    [Tooltip("Layer mask for the Drawers layer.")]
    [SerializeField] private LayerMask drawersLayerMask;

    [Tooltip("Layer mask for the Perfumes layer.")]
    [SerializeField] private LayerMask perfumesLayerMask;

    [Tooltip("Layer mask for the Trinkets layer.")]
    [SerializeField] private LayerMask trinketsLayerMask;

    [Tooltip("Layer mask for the CoffeeTableBooks layer.")]
    [SerializeField] private LayerMask coffeeTableBooksLayerMask;

    [Tooltip("Maximum raycast distance from camera.")]
    [SerializeField] private float maxRayDistance = 10f;

    [Header("Audio")]
    [Tooltip("SFX played when pulling a book out (optional).")]
    [SerializeField] private AudioClip pullOutSFX;

    [Tooltip("SFX played when putting a book back (optional).")]
    [SerializeField] private AudioClip putBackSFX;

    [Tooltip("SFX played when hovering over an interactable item.")]
    [SerializeField] private AudioClip hoverSFX;

    [Tooltip("SFX played when clicking/selecting an item.")]
    [SerializeField] private AudioClip selectSFX;

    public State CurrentState { get; private set; } = State.Browsing;

    private InputAction _mousePositionAction;
    private InputAction _clickAction;
    private InputAction _cancelAction;

    // Book state
    private BookVolume _hoveredBook;
    private BookVolume _activeBook;

    // Drawer state
    private DrawerController _hoveredDrawer;
    private DrawerController _activeDrawer;

    // Perfume state
    private PerfumeBottle _hoveredPerfume;

    // Trinket state
    private TrinketVolume _hoveredTrinket;

    // Coffee table book state
    private CoffeeTableBook _hoveredCoffeeBook;
    private CoffeeTableBook _activeCoffeeBook;

    // Double-click detection
    private float _lastClickTime;
    private GameObject _lastClickedObject;
    private const float DoubleClickThreshold = 0.3f;

    // Combined layer mask for browsing raycast
    private LayerMask _combinedLayerMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BookInteractionManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value,
            "<Mouse>/position");

        _clickAction = new InputAction("Click", InputActionType.Button,
            "<Mouse>/leftButton");

        _cancelAction = new InputAction("Cancel", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Mouse>/rightButton");

        if (titleHintPanel != null)
            titleHintPanel.SetActive(false);
    }

    private void Start()
    {
        _combinedLayerMask = booksLayerMask | drawersLayerMask | perfumesLayerMask
                           | trinketsLayerMask | coffeeTableBooksLayerMask;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        _mousePositionAction.Enable();
        _clickAction.Enable();
        _cancelAction.Enable();
    }

    private void OnDisable()
    {
        _mousePositionAction.Disable();
        _clickAction.Disable();
        _cancelAction.Disable();
    }

    private void Update()
    {
        if (DayPhaseManager.Instance == null || !DayPhaseManager.Instance.IsInteractionPhase)
            return;

        if (ObjectGrabber.IsHoldingObject) return;

        switch (CurrentState)
        {
            case State.Browsing:
                UpdateBrowsing();
                break;
            case State.Pulling:
                UpdatePulling();
                break;
            case State.Reading:
                UpdateReading();
                break;
            case State.PuttingBack:
                UpdatePuttingBack();
                break;
            case State.DrawerOpening:
                UpdateDrawerOpening();
                break;
            case State.DrawerOpen:
                UpdateDrawerOpen();
                break;
            case State.DrawerClosing:
                UpdateDrawerClosing();
                break;
            case State.Inspecting:
                UpdateInspecting();
                break;
            case State.MovingCoffeeBook:
                UpdateMovingCoffeeBook();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing — multi-layer raycast with hover and click routing
    // ──────────────────────────────────────────────────────────────

    private void UpdateBrowsing()
    {
        if (mainCamera == null) return;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit = default;
        bool didHit = Physics.Raycast(ray, out hit, maxRayDistance, _combinedLayerMask);

        if (didHit)
        {
            int hitLayer = hit.collider.gameObject.layer;

            if (IsInLayerMask(hitLayer, booksLayerMask))
                HandleBookHover(hit);
            else if (IsInLayerMask(hitLayer, drawersLayerMask))
                HandleDrawerHover(hit);
            else if (IsInLayerMask(hitLayer, perfumesLayerMask))
                HandlePerfumeHover(hit);
            else if (IsInLayerMask(hitLayer, trinketsLayerMask))
                HandleTrinketHover(hit);
            else if (IsInLayerMask(hitLayer, coffeeTableBooksLayerMask))
                HandleCoffeeBookHover(hit);
            else
                ClearAllHovers();
        }
        else
        {
            ClearAllHovers();
        }

        // Click routing
        if (_clickAction.WasPressedThisFrame())
        {
            HandleBrowsingClick(hit);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    // ── Book hover ──

    private void HandleBookHover(RaycastHit hit)
    {
        ClearNonBookHovers();

        var book = hit.collider.GetComponent<BookVolume>();
        if (book != null && book != _hoveredBook)
        {
            ClearBookHover();
            _hoveredBook = book;
            _hoveredBook.OnHoverEnter();
            ShowHint(_hoveredBook.Definition != null ? _hoveredBook.Definition.title : "Book");
            if (hoverSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hoverSFX);
        }
    }

    // ── Drawer hover ──

    private void HandleDrawerHover(RaycastHit hit)
    {
        ClearNonDrawerHovers();

        var drawer = hit.collider.GetComponent<DrawerController>();
        if (drawer != null && drawer != _hoveredDrawer)
        {
            ClearDrawerHover();
            _hoveredDrawer = drawer;
            _hoveredDrawer.OnHoverEnter();
            ShowHint("Drawer");
            if (hoverSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hoverSFX);
        }
    }

    // ── Perfume hover ──

    private void HandlePerfumeHover(RaycastHit hit)
    {
        ClearNonPerfumeHovers();

        var perfume = hit.collider.GetComponent<PerfumeBottle>();
        if (perfume != null && perfume != _hoveredPerfume)
        {
            ClearPerfumeHover();
            _hoveredPerfume = perfume;
            _hoveredPerfume.OnHoverEnter();
            ShowHint(_hoveredPerfume.Definition != null ? _hoveredPerfume.Definition.perfumeName : "Perfume");
            if (hoverSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hoverSFX);
        }
    }

    // ── Trinket hover ──

    private void HandleTrinketHover(RaycastHit hit)
    {
        ClearNonTrinketHovers();

        var trinket = hit.collider.GetComponent<TrinketVolume>();
        if (trinket != null && trinket != _hoveredTrinket)
        {
            ClearTrinketHover();
            _hoveredTrinket = trinket;
            _hoveredTrinket.OnHoverEnter();
            ShowHint(_hoveredTrinket.Definition != null ? _hoveredTrinket.Definition.trinketName : "Trinket");
            if (hoverSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hoverSFX);
        }
    }

    // ── Coffee book hover ──

    private void HandleCoffeeBookHover(RaycastHit hit)
    {
        ClearNonCoffeeBookHovers();

        var coffeeBook = hit.collider.GetComponent<CoffeeTableBook>();
        if (coffeeBook != null && coffeeBook != _hoveredCoffeeBook)
        {
            ClearCoffeeBookHover();
            _hoveredCoffeeBook = coffeeBook;
            _hoveredCoffeeBook.OnHoverEnter();
            ShowHint(_hoveredCoffeeBook.Definition != null ? _hoveredCoffeeBook.Definition.title : "Coffee Table Book");
            if (hoverSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hoverSFX);
        }
    }

    // ── Click routing during Browsing ──

    private void HandleBrowsingClick(RaycastHit hit)
    {
        // Double-click detection
        GameObject clickedObj = hit.collider != null ? hit.collider.gameObject : null;
        bool isDoubleClick = false;

        if (clickedObj != null && clickedObj == _lastClickedObject
            && (Time.unscaledTime - _lastClickTime) < DoubleClickThreshold)
        {
            isDoubleClick = true;
            _lastClickedObject = null;
        }
        else
        {
            _lastClickTime = Time.unscaledTime;
            _lastClickedObject = clickedObj;
        }

        // Book click
        if (_hoveredBook != null)
        {
            if (isDoubleClick) return; // books use Reading, not inspection
            if (selectSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(selectSFX);
            _activeBook = _hoveredBook;
            ClearBookHover();
            BeginPullOut();
            return;
        }

        // Drawer click
        if (_hoveredDrawer != null)
        {
            if (selectSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(selectSFX);
            _activeDrawer = _hoveredDrawer;
            ClearDrawerHover();
            BeginDrawerOpen();
            return;
        }

        // Perfume click — single click sprays immediately
        if (_hoveredPerfume != null)
        {
            if (isDoubleClick && itemInspector != null)
            {
                var def = _hoveredPerfume.Definition;
                itemInspector.InspectItem(_hoveredPerfume.transform,
                    def != null ? def.perfumeName : "Perfume",
                    def != null ? def.description : "");
                ClearPerfumeHover();
                CurrentState = State.Inspecting;
                return;
            }
            _hoveredPerfume.SprayOnce();
            return;
        }

        // Trinket click
        if (_hoveredTrinket != null)
        {
            if (isDoubleClick && itemInspector != null)
            {
                var def = _hoveredTrinket.Definition;
                itemInspector.InspectItem(_hoveredTrinket.transform,
                    def != null ? def.trinketName : "Trinket",
                    def != null ? def.description : "");
                ClearTrinketHover();
                CurrentState = State.Inspecting;
                return;
            }
            _hoveredTrinket.TogglePlacement();
            return;
        }

        // Coffee table book click
        if (_hoveredCoffeeBook != null)
        {
            if (isDoubleClick && itemInspector != null)
            {
                var def = _hoveredCoffeeBook.Definition;
                itemInspector.InspectItem(_hoveredCoffeeBook.transform,
                    def != null ? def.title : "Coffee Table Book",
                    def != null ? def.description : "");
                ClearCoffeeBookHover();
                CurrentState = State.Inspecting;
                return;
            }
            _activeCoffeeBook = _hoveredCoffeeBook;
            ClearCoffeeBookHover();
            BeginMoveCoffeeBook();
            return;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Hover clearing helpers
    // ──────────────────────────────────────────────────────────────

    private void ClearAllHovers()
    {
        ClearBookHover();
        ClearDrawerHover();
        ClearPerfumeHover();
        ClearTrinketHover();
        ClearCoffeeBookHover();
        HideHint();
    }

    private void ClearNonBookHovers()
    {
        ClearDrawerHover();
        ClearPerfumeHover();
        ClearTrinketHover();
        ClearCoffeeBookHover();
    }

    private void ClearNonDrawerHovers()
    {
        ClearBookHover();
        ClearPerfumeHover();
        ClearTrinketHover();
        ClearCoffeeBookHover();
    }

    private void ClearNonPerfumeHovers()
    {
        ClearBookHover();
        ClearDrawerHover();
        ClearTrinketHover();
        ClearCoffeeBookHover();
    }

    private void ClearNonTrinketHovers()
    {
        ClearBookHover();
        ClearDrawerHover();
        ClearPerfumeHover();
        ClearCoffeeBookHover();
    }

    private void ClearNonCoffeeBookHovers()
    {
        ClearBookHover();
        ClearDrawerHover();
        ClearPerfumeHover();
        ClearTrinketHover();
    }

    private void ClearBookHover()
    {
        if (_hoveredBook != null)
        {
            _hoveredBook.OnHoverExit();
            _hoveredBook = null;
        }
    }

    private void ClearDrawerHover()
    {
        if (_hoveredDrawer != null)
        {
            _hoveredDrawer.OnHoverExit();
            _hoveredDrawer = null;
        }
    }

    private void ClearPerfumeHover()
    {
        if (_hoveredPerfume != null)
        {
            _hoveredPerfume.OnHoverExit();
            _hoveredPerfume = null;
        }
    }

    private void ClearTrinketHover()
    {
        if (_hoveredTrinket != null)
        {
            _hoveredTrinket.OnHoverExit();
            _hoveredTrinket = null;
        }
    }

    private void ClearCoffeeBookHover()
    {
        if (_hoveredCoffeeBook != null)
        {
            _hoveredCoffeeBook.OnHoverExit();
            _hoveredCoffeeBook = null;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // UI hint
    // ──────────────────────────────────────────────────────────────

    private void ShowHint(string text)
    {
        if (titleHintPanel == null || titleHintText == null) return;
        titleHintText.text = text;
        titleHintPanel.SetActive(true);
    }

    private void HideHint()
    {
        if (titleHintPanel != null)
            titleHintPanel.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────
    // Pull Out (existing book behavior)
    // ──────────────────────────────────────────────────────────────

    private void BeginPullOut()
    {
        CurrentState = State.Pulling;
        HideHint();

        if (pullOutSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(pullOutSFX);

        _activeBook.PullOut(readingAnchor);
    }

    private void UpdatePulling()
    {
        if (_activeBook != null && _activeBook.CurrentState == BookVolume.State.Reading)
            CurrentState = State.Reading;
    }

    // ──────────────────────────────────────────────────────────────
    // Reading — click or cancel to put back
    // ──────────────────────────────────────────────────────────────

    private void UpdateReading()
    {
        if (_clickAction.WasPressedThisFrame() || _cancelAction.WasPressedThisFrame())
            BeginPutBack();
    }

    private void BeginPutBack()
    {
        CurrentState = State.PuttingBack;

        if (putBackSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(putBackSFX);

        _activeBook.PutBack();
    }

    private void UpdatePuttingBack()
    {
        if (_activeBook != null && _activeBook.CurrentState == BookVolume.State.OnShelf)
        {
            _activeBook = null;
            CurrentState = State.Browsing;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Drawer
    // ──────────────────────────────────────────────────────────────

    private void BeginDrawerOpen()
    {
        CurrentState = State.DrawerOpening;
        HideHint();
        _activeDrawer.Open();
    }

    private void UpdateDrawerOpening()
    {
        if (_activeDrawer != null && _activeDrawer.CurrentState == DrawerController.State.Open)
            CurrentState = State.DrawerOpen;
    }

    private void UpdateDrawerOpen()
    {
        if (mainCamera == null) return;

        // Cancel closes drawer
        if (_cancelAction.WasPressedThisFrame())
        {
            BeginDrawerClose();
            return;
        }

        // Raycast for trinkets inside drawer or on display
        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        LayerMask drawerBrowseMask = trinketsLayerMask | drawersLayerMask;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, drawerBrowseMask))
        {
            int hitLayer = hit.collider.gameObject.layer;

            if (IsInLayerMask(hitLayer, trinketsLayerMask))
            {
                HandleTrinketHover(hit);
            }
            else if (IsInLayerMask(hitLayer, drawersLayerMask))
            {
                ClearTrinketHover();

                // Re-click on the active drawer closes it
                var drawer = hit.collider.GetComponent<DrawerController>();
                if (drawer != null && drawer != _hoveredDrawer)
                {
                    ClearDrawerHover();
                    _hoveredDrawer = drawer;
                    _hoveredDrawer.OnHoverEnter();
                    ShowHint("Drawer");
                }
            }
            else
            {
                ClearTrinketHover();
                ClearDrawerHover();
                HideHint();
            }
        }
        else
        {
            ClearTrinketHover();
            ClearDrawerHover();
            HideHint();
        }

        if (_clickAction.WasPressedThisFrame())
        {
            if (_hoveredTrinket != null)
            {
                _hoveredTrinket.TogglePlacement();
                return;
            }

            // Click on the same active drawer to close it
            if (_hoveredDrawer != null && _hoveredDrawer == _activeDrawer)
            {
                ClearDrawerHover();
                BeginDrawerClose();
                return;
            }
        }
    }

    private void BeginDrawerClose()
    {
        CurrentState = State.DrawerClosing;
        _activeDrawer.Close();
    }

    private void UpdateDrawerClosing()
    {
        if (_activeDrawer != null && _activeDrawer.CurrentState == DrawerController.State.Closed)
        {
            _activeDrawer = null;
            CurrentState = State.Browsing;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Inspecting (shared — ItemInspector handles lerp/UI)
    // ──────────────────────────────────────────────────────────────

    private void UpdateInspecting()
    {
        if (itemInspector == null || !itemInspector.IsInspecting)
        {
            CurrentState = State.Browsing;
            return;
        }

        if (_cancelAction.WasPressedThisFrame())
        {
            itemInspector.EndInspection();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Coffee Table Book
    // ──────────────────────────────────────────────────────────────

    private void BeginMoveCoffeeBook()
    {
        CurrentState = State.MovingCoffeeBook;
        HideHint();
        _activeCoffeeBook.TogglePlacement();
    }

    private void UpdateMovingCoffeeBook()
    {
        if (_activeCoffeeBook == null || !_activeCoffeeBook.IsMoving)
        {
            _activeCoffeeBook = null;
            CurrentState = State.Browsing;
        }
    }
}
