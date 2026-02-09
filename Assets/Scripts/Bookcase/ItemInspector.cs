using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Shared utility for close-up item inspection. Lerps any item to an
/// inspect anchor (camera child), shows name + description UI panel.
/// RMB/ESC to return item and dismiss.
/// </summary>
public class ItemInspector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform where inspected items are positioned (child of camera).")]
    [SerializeField] private Transform inspectAnchor;

    [Tooltip("BookcaseBrowseCamera — look disabled during inspection.")]
    [SerializeField] private BookcaseBrowseCamera browseCamera;

    [Header("UI")]
    [Tooltip("Root panel for item name + description overlay.")]
    [SerializeField] private GameObject descriptionPanel;

    [Tooltip("TMP_Text for item name.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("TMP_Text for item description.")]
    [SerializeField] private TMP_Text descriptionText;

    [Header("Settings")]
    [Tooltip("Duration of the lerp to/from inspect position.")]
    [SerializeField] private float lerpDuration = 0.3f;

    public bool IsInspecting { get; private set; }

    private Transform _inspectedItem;
    private Vector3 _returnPosition;
    private Quaternion _returnRotation;
    private Transform _returnParent;

    private void Awake()
    {
        if (descriptionPanel != null)
            descriptionPanel.SetActive(false);
    }

    /// <summary>
    /// Begin inspecting an item. Lerps it to the inspect anchor.
    /// </summary>
    public void InspectItem(Transform item, string itemName, string itemDescription)
    {
        if (IsInspecting || item == null) return;

        _inspectedItem = item;
        _returnPosition = item.position;
        _returnRotation = item.rotation;
        _returnParent = item.parent;

        IsInspecting = true;

        if (browseCamera != null)
            browseCamera.SetLookEnabled(false);

        if (descriptionPanel != null)
        {
            if (nameText != null) nameText.text = itemName;
            if (descriptionText != null) descriptionText.text = itemDescription;
            descriptionPanel.SetActive(true);
        }

        StartCoroutine(LerpToInspect());
    }

    /// <summary>
    /// End inspection. Lerps item back to original position.
    /// </summary>
    public void EndInspection()
    {
        if (!IsInspecting || _inspectedItem == null) return;
        StartCoroutine(LerpFromInspect());
    }

    private IEnumerator LerpToInspect()
    {
        Vector3 startPos = _inspectedItem.position;
        Quaternion startRot = _inspectedItem.rotation;
        float elapsed = 0f;

        _inspectedItem.SetParent(inspectAnchor, true);

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / lerpDuration);
            _inspectedItem.position = Vector3.Lerp(startPos, inspectAnchor.position, t);
            _inspectedItem.rotation = Quaternion.Slerp(startRot, inspectAnchor.rotation, t);
            yield return null;
        }

        _inspectedItem.position = inspectAnchor.position;
        _inspectedItem.rotation = inspectAnchor.rotation;
    }

    private IEnumerator LerpFromInspect()
    {
        if (descriptionPanel != null)
            descriptionPanel.SetActive(false);

        Vector3 startPos = _inspectedItem.position;
        Quaternion startRot = _inspectedItem.rotation;
        float elapsed = 0f;

        _inspectedItem.SetParent(_returnParent, true);

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / lerpDuration);
            _inspectedItem.position = Vector3.Lerp(startPos, _returnPosition, t);
            _inspectedItem.rotation = Quaternion.Slerp(startRot, _returnRotation, t);
            yield return null;
        }

        _inspectedItem.position = _returnPosition;
        _inspectedItem.rotation = _returnRotation;

        if (browseCamera != null)
            browseCamera.SetLookEnabled(true);

        IsInspecting = false;
        _inspectedItem = null;
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
