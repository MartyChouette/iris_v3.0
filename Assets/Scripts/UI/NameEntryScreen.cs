using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Earthbound-style letter grid name entry. Arrow keys navigate the grid,
/// Enter/Space selects a character. Replaces the TMP_InputField approach.
/// </summary>
public class NameEntryScreen : MonoBehaviour
{
    public static NameEntryScreen Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TMP_Text _nameDisplay;
    [SerializeField] private TMP_Text _gridText;

    [Header("Settings")]
    [SerializeField] private int _maxNameLength = 8;

    // ── Grid definition ────────────────────────────────────────────
    // 7 rows: 6 character rows (9 cols each) + 1 command row (3 zones)
    private static readonly char[][] _charGrid = {
        new[] { 'A','B','C','D','E','F','G','H','I' },
        new[] { 'J','K','L','M','N','O','P','Q','R' },
        new[] { 'S','T','U','V','W','X','Y','Z',' ' },
        new[] { 'a','b','c','d','e','f','g','h','i' },
        new[] { 'j','k','l','m','n','o','p','q','r' },
        new[] { 's','t','u','v','w','x','y','z',' ' },
    };

    private const int GridCols = 9;
    private const int CharRows = 6;
    private const int TotalRows = 7; // 6 char rows + 1 command row
    private const int CmdBack = 0;
    private const int CmdSpace = 1;
    private const int CmdOK = 2;

    [Header("Colors")]
    [Tooltip("Color of the currently selected grid cell.")]
    [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.4f);
    [Tooltip("Color of unselected command buttons.")]
    [SerializeField] private Color _dimColor = new Color(0.53f, 0.53f, 0.53f);
    [Tooltip("Color of empty name slots.")]
    [SerializeField] private Color _nameSlotColor = new Color(0.67f, 0.67f, 0.67f);
    [Tooltip("Color of entered name characters.")]
    [SerializeField] private Color _nameCharColor = Color.white;

    // Cached hex strings (built in Awake from Color fields)
    private string _highlightHex;
    private string _dimHex;
    private string _slotHex;
    private string _charHex;

    // ── Input ──────────────────────────────────────────────────────
    private InputAction _navUp;
    private InputAction _navDown;
    private InputAction _navLeft;
    private InputAction _navRight;
    private InputAction _selectAction;

    // ── Runtime state ──────────────────────────────────────────────
    private int _cursorRow;
    private int _cursorCol;
    private char[] _nameChars;
    private int _nameLength;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cache hex color strings from SerializeField colors
        _highlightHex = "#" + ColorUtility.ToHtmlStringRGB(_highlightColor);
        _dimHex = "#" + ColorUtility.ToHtmlStringRGB(_dimColor);
        _slotHex = "#" + ColorUtility.ToHtmlStringRGB(_nameSlotColor);
        _charHex = "#" + ColorUtility.ToHtmlStringRGB(_nameCharColor);

        // Initialize name buffer with default
        _nameChars = new char[_maxNameLength];
        string defaultName = PlayerData.PlayerName;
        _nameLength = Mathf.Min(defaultName.Length, _maxNameLength);
        for (int i = 0; i < _nameLength; i++)
            _nameChars[i] = defaultName[i];

        // Inline input actions
        _navUp = new InputAction("GridUp", InputActionType.Button);
        _navUp.AddBinding("<Keyboard>/w");
        _navUp.AddBinding("<Keyboard>/upArrow");

        _navDown = new InputAction("GridDown", InputActionType.Button);
        _navDown.AddBinding("<Keyboard>/s");
        _navDown.AddBinding("<Keyboard>/downArrow");

        _navLeft = new InputAction("GridLeft", InputActionType.Button);
        _navLeft.AddBinding("<Keyboard>/a");
        _navLeft.AddBinding("<Keyboard>/leftArrow");

        _navRight = new InputAction("GridRight", InputActionType.Button);
        _navRight.AddBinding("<Keyboard>/d");
        _navRight.AddBinding("<Keyboard>/rightArrow");

        _selectAction = new InputAction("GridSelect", InputActionType.Button);
        _selectAction.AddBinding("<Keyboard>/enter");
        _selectAction.AddBinding("<Keyboard>/space");
    }

    private void OnEnable()
    {
        _navUp.Enable();
        _navDown.Enable();
        _navLeft.Enable();
        _navRight.Enable();
        _selectAction.Enable();
    }

    private void OnDisable()
    {
        _navUp.Disable();
        _navDown.Disable();
        _navLeft.Disable();
        _navRight.Disable();
        _selectAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void Update()
    {
        bool changed = false;

        if (_navUp.WasPressedThisFrame())
        {
            _cursorRow = (_cursorRow - 1 + TotalRows) % TotalRows;
            ClampCursorCol();
            changed = true;
        }
        else if (_navDown.WasPressedThisFrame())
        {
            _cursorRow = (_cursorRow + 1) % TotalRows;
            ClampCursorCol();
            changed = true;
        }

        if (_navLeft.WasPressedThisFrame())
        {
            if (_cursorRow < CharRows)
            {
                _cursorCol = (_cursorCol - 1 + GridCols) % GridCols;
            }
            else
            {
                // Command row: 3 zones
                int cmd = GetCommandIndex();
                cmd = (cmd - 1 + 3) % 3;
                _cursorCol = cmd * 3;
            }
            changed = true;
        }
        else if (_navRight.WasPressedThisFrame())
        {
            if (_cursorRow < CharRows)
            {
                _cursorCol = (_cursorCol + 1) % GridCols;
            }
            else
            {
                int cmd = GetCommandIndex();
                cmd = (cmd + 1) % 3;
                _cursorCol = cmd * 3;
            }
            changed = true;
        }

        if (_selectAction.WasPressedThisFrame())
        {
            HandleSelect();
            changed = true;
        }

        if (changed)
            RefreshDisplay();
    }

    // ── Selection ──────────────────────────────────────────────────

    private void HandleSelect()
    {
        if (_cursorRow < CharRows)
        {
            // Character selected
            char c = _charGrid[_cursorRow][_cursorCol];
            if (_nameLength < _maxNameLength)
            {
                _nameChars[_nameLength] = c;
                _nameLength++;
            }
        }
        else
        {
            // Command row
            int cmd = GetCommandIndex();
            switch (cmd)
            {
                case CmdBack:
                    if (_nameLength > 0)
                        _nameLength--;
                    break;
                case CmdSpace:
                    if (_nameLength < _maxNameLength)
                    {
                        _nameChars[_nameLength] = ' ';
                        _nameLength++;
                    }
                    break;
                case CmdOK:
                    OnConfirm();
                    break;
            }
        }
    }

    private int GetCommandIndex()
    {
        // Map column to command zone: 0-2=Back, 3-5=Space, 6-8=OK
        if (_cursorCol < 3) return CmdBack;
        if (_cursorCol < 6) return CmdSpace;
        return CmdOK;
    }

    private void ClampCursorCol()
    {
        if (_cursorRow == CharRows) // entering command row
        {
            // Snap to nearest command zone center
            if (_cursorCol < 3) _cursorCol = 0;
            else if (_cursorCol < 6) _cursorCol = 3;
            else _cursorCol = 6;
        }
        // Char rows always have GridCols columns, no clamping needed
    }

    // ── Display ────────────────────────────────────────────────────

    private void RefreshDisplay()
    {
        RefreshNameDisplay();
        RefreshGridDisplay();
    }

    private void RefreshNameDisplay()
    {
        if (_nameDisplay == null) return;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _maxNameLength; i++)
        {
            if (i < _nameLength)
            {
                char c = _nameChars[i];
                string display = c == ' ' ? "_" : c.ToString();
                sb.Append($"<color={_charHex}>{display}</color> ");
            }
            else if (i == _nameLength)
            {
                // Cursor position — blinking underscore
                float blink = Mathf.PingPong(Time.time * 3f, 1f);
                string cursorChar = blink > 0.5f ? "_" : " ";
                sb.Append($"<color={_charHex}>{cursorChar}</color> ");
            }
            else
            {
                sb.Append($"<color={_slotHex}>.</color> ");
            }
        }
        _nameDisplay.text = sb.ToString();
    }

    private void RefreshGridDisplay()
    {
        if (_gridText == null) return;

        var sb = new System.Text.StringBuilder();

        // Character rows
        for (int r = 0; r < CharRows; r++)
        {
            for (int c = 0; c < GridCols; c++)
            {
                char ch = _charGrid[r][c];
                string display = ch == ' ' ? "  " : ch.ToString();
                bool selected = (r == _cursorRow && c == _cursorCol);

                if (selected)
                    sb.Append($"<color={_highlightHex}>[{display}]</color>");
                else
                    sb.Append($" {display} ");

                if (c < GridCols - 1)
                    sb.Append(' ');
            }
            sb.Append('\n');
        }

        sb.Append('\n');

        // Command row
        int cmdSelected = (_cursorRow >= CharRows) ? GetCommandIndex() : -1;

        string[] cmdLabels = { "BACK", "SPACE", "OK" };
        for (int i = 0; i < 3; i++)
        {
            if (i == cmdSelected)
                sb.Append($"<color={_highlightHex}>[ {cmdLabels[i]} ]</color>");
            else
                sb.Append($"<color={_dimHex}>  {cmdLabels[i]}  </color>");

            if (i < 2)
                sb.Append("    ");
        }

        _gridText.text = sb.ToString();
    }

    // ── Confirm ────────────────────────────────────────────────────

    private void OnConfirm()
    {
        string name = new string(_nameChars, 0, _nameLength).Trim();
        if (string.IsNullOrEmpty(name))
            name = "Nema";

        PlayerData.PlayerName = name;
        Debug.Log($"[NameEntryScreen] Player name set to: {name}");

        // Disable input so it doesn't conflict with apartment systems
        _navUp.Disable();
        _navDown.Disable();
        _navLeft.Disable();
        _navRight.Disable();
        _selectAction.Disable();

        // Hide entire screen (disables OnEnable re-enabling inputs too)
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);

        // Begin the first day - fires newspaper
        DayManager.Instance?.BeginDay1();
    }
}
