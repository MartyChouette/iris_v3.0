using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tab button helper for the settings panel. Handles active/inactive visual state
/// and routes clicks to SettingsPanel.SwitchTab(index).
/// </summary>
[RequireComponent(typeof(Button))]
public class SettingsTabButton : MonoBehaviour
{
    [SerializeField] private int _tabIndex;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _label;

    [Header("Colors")]
    [SerializeField] private Color _activeColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    [SerializeField] private Color _inactiveColor = new Color(0.15f, 0.15f, 0.18f, 0.8f);
    [SerializeField] private Color _activeLabelColor = Color.white;
    [SerializeField] private Color _inactiveLabelColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private SettingsPanel _panel;
    private Button _button;

    public int TabIndex => _tabIndex;

    public void Initialize(SettingsPanel panel, int index)
    {
        _panel = panel;
        _tabIndex = index;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (_panel != null)
            _panel.SwitchTab(_tabIndex);
    }

    public void SetActive(bool active)
    {
        if (_background != null)
            _background.color = active ? _activeColor : _inactiveColor;
        if (_label != null)
            _label.color = active ? _activeLabelColor : _inactiveLabelColor;
    }
}
