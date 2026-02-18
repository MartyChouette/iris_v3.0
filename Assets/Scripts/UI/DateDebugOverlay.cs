using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Scene-scoped singleton toggled by F1. Shows live debug state:
/// date phase, affection, NPC state, public/private items, cleanliness,
/// smell, and a scrolling reaction log.
/// </summary>
public class DateDebugOverlay : MonoBehaviour
{
    public static DateDebugOverlay Instance { get; private set; }

    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _debugText;

    private InputAction _toggleAction;
    private readonly List<string> _reactionLog = new List<string>();
    private const int MaxLogEntries = 20;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DateDebugOverlay] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _toggleAction = new InputAction("DebugToggle", InputActionType.Button, "<Keyboard>/f1");

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _toggleAction.Enable();
    }

    private void OnDisable()
    {
        _toggleAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_toggleAction.WasPressedThisFrame() && _overlayRoot != null)
            _overlayRoot.SetActive(!_overlayRoot.activeSelf);

        if (_overlayRoot == null || !_overlayRoot.activeSelf || _debugText == null) return;

        RebuildText();
    }

    /// <summary>Append a reaction to the scrolling log.</summary>
    public void LogReaction(string text)
    {
        _reactionLog.Add(text);
        if (_reactionLog.Count > MaxLogEntries)
            _reactionLog.RemoveAt(0);
    }

    private void RebuildText()
    {
        var sb = new System.Text.StringBuilder(512);

        // Phase + Affection
        var dsm = DateSessionManager.Instance;
        string phase = dsm != null ? dsm.CurrentDatePhase.ToString() : "N/A";
        float affection = dsm != null ? dsm.Affection : 0f;
        sb.AppendLine($"Phase: {phase}  |  Affection: {affection:F1}");

        // NPC State
        var npc = Object.FindAnyObjectByType<DateCharacterController>();
        string npcState = npc != null ? npc.CurrentState.ToString() : "N/A";
        sb.AppendLine($"NPC State: {npcState}");

        sb.AppendLine();

        // Public items
        sb.AppendLine("PUBLIC ITEMS:");
        foreach (var tag in ReactableTag.All)
        {
            if (!tag.IsPrivate && tag.IsActive)
                sb.AppendLine($"  {tag.gameObject.name} [{string.Join(",", tag.Tags)}]");
        }

        // Private items
        sb.AppendLine("PRIVATE ITEMS:");
        foreach (var tag in ReactableTag.All)
        {
            if (tag.IsPrivate)
                sb.AppendLine($"  {tag.gameObject.name} [{string.Join(",", tag.Tags)}]");
        }

        sb.AppendLine();

        // Per-area cleanliness
        var cm = CleaningManager.Instance;
        if (cm != null)
        {
            sb.AppendLine($"Kitchen Clean: {cm.GetAreaCleanPercent(ApartmentArea.Kitchen):P0}");
            sb.AppendLine($"Living Room Clean: {cm.GetAreaCleanPercent(ApartmentArea.LivingRoom):P0}");
        }

        // Smell
        sb.AppendLine($"Smell: {SmellTracker.TotalSmell:F2} (threshold: {SmellTracker.SmellThreshold:F1})");

        sb.AppendLine();

        // Reaction log
        sb.AppendLine("REACTION LOG:");
        for (int i = 0; i < _reactionLog.Count; i++)
            sb.AppendLine($"  {_reactionLog[i]}");

        _debugText.text = sb.ToString();
    }
}
