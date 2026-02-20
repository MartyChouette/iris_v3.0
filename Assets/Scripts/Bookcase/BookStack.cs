using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic reusable stack manager for flat-stacked books. Handles both
/// floor BookVolumes and CoffeeTableBooks. Books register at Start,
/// are removed when pulled out (triggering collapse), and added to the
/// top when returned.
/// </summary>
public class BookStack : MonoBehaviour
{
    [SerializeField] private Vector3 _stackBase;
    [SerializeField] private Quaternion _stackRotation = Quaternion.identity;

    private readonly List<StackEntry> _entries = new List<StackEntry>();
    private readonly Dictionary<Transform, Coroutine> _activeAnimations = new Dictionary<Transform, Coroutine>();

    private const float CollapseDuration = 0.3f;
    private const float JitterXZ = 0.003f;
    private const float JitterYaw = 2f;

    private struct StackEntry
    {
        public Transform book;
        public float thickness;
    }

    public void Register(Transform book, float thickness)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].book == book) return;

        _entries.Add(new StackEntry { book = book, thickness = thickness });
    }

    public void Remove(Transform book)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].book == book)
            {
                _entries.RemoveAt(i);
                break;
            }
        }
        Collapse();
    }

    /// <summary>
    /// Places a book on top of the stack. Outputs the target position and rotation.
    /// </summary>
    public void AddToTop(Transform book, float thickness, out Vector3 position, out Quaternion rotation)
    {
        // Remove if already present (shouldn't happen, but safety)
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].book == book)
                _entries.RemoveAt(i);

        _entries.Add(new StackEntry { book = book, thickness = thickness });
        int index = _entries.Count - 1;
        position = GetTargetPosition(index);
        rotation = GetTargetRotation(index);
    }

    /// <summary>
    /// Returns the target rotation for a book at the given index,
    /// with slight random jitter for a natural look.
    /// </summary>
    public Quaternion GetTargetRotation(int index)
    {
        // Deterministic jitter per index so it's stable
        Random.State saved = Random.state;
        Random.InitState(index * 7 + 31);
        float yaw = Random.Range(-JitterYaw, JitterYaw);
        Random.state = saved;

        return _stackRotation * Quaternion.Euler(0f, yaw, 0f);
    }

    private Vector3 GetTargetPosition(int index)
    {
        float yOffset = 0f;
        for (int i = 0; i < index; i++)
            yOffset += _entries[i].thickness;

        float halfThickness = _entries[index].thickness / 2f;

        // Deterministic jitter per index
        Random.State saved = Random.state;
        Random.InitState(index * 7 + 31);
        Random.Range(-JitterYaw, JitterYaw); // consume the yaw value
        float xJitter = Random.Range(-JitterXZ, JitterXZ);
        float zJitter = Random.Range(-JitterXZ, JitterXZ);
        Random.state = saved;

        return _stackBase + Vector3.up * (yOffset + halfThickness)
               + new Vector3(xJitter, 0f, zJitter);
    }

    private void Collapse()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.book == null) continue;

            Vector3 targetPos = GetTargetPosition(i);
            Quaternion targetRot = GetTargetRotation(i);

            // Cancel any existing animation on this book
            if (_activeAnimations.TryGetValue(entry.book, out var existing))
            {
                if (existing != null) StopCoroutine(existing);
                _activeAnimations.Remove(entry.book);
            }

            var routine = StartCoroutine(AnimateCollapse(entry.book, targetPos, targetRot));
            _activeAnimations[entry.book] = routine;
        }
    }

    private IEnumerator AnimateCollapse(Transform book, Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = book.position;
        Quaternion startRot = book.rotation;
        float elapsed = 0f;

        while (elapsed < CollapseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / CollapseDuration);
            book.position = Vector3.Lerp(startPos, targetPos, t);
            book.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        book.position = targetPos;
        book.rotation = targetRot;
        _activeAnimations.Remove(book);
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
