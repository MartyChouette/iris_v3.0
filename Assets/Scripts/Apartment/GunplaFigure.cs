using UnityEngine;

/// <summary>
/// Poseable Gunpla model figure built from a hierarchy of cubes with HingeJoints.
/// On pickup: all child Rigidbodies go kinematic so the figure moves as one unit.
/// On place: child Rigidbodies restored to non-kinematic with HingeJoint limits.
/// Click a limb segment while NOT holding it to cycle that joint through preset angles.
/// </summary>
public class GunplaFigure : MonoBehaviour
{
    [Tooltip("Preset angles (degrees) to cycle through when clicking a joint.")]
    [SerializeField] private float[] _presetAngles = { 0f, -45f, -90f };

    [Tooltip("Spring force used to drive joints to target angles.")]
    [SerializeField] private float _jointSpringForce = 50f;

    [Tooltip("Damper on the joint spring.")]
    [SerializeField] private float _jointDamper = 5f;

    private HingeJoint[] _joints;
    private Rigidbody[] _childBodies;
    private int[] _angleIndices; // per-joint index into _presetAngles
    private bool _isHeld;

    private void Awake()
    {
        _joints = GetComponentsInChildren<HingeJoint>();
        _childBodies = GetComponentsInChildren<Rigidbody>();
        _angleIndices = new int[_joints.Length];
    }

    /// <summary>
    /// Called by PlaceableObject.OnPickedUp (via SendMessage or manual wiring).
    /// Sets all child Rigidbodies kinematic so the figure moves as one piece.
    /// </summary>
    public void OnFigurePickedUp()
    {
        _isHeld = true;
        foreach (var rb in _childBodies)
        {
            if (rb.gameObject == gameObject) continue; // root handled by ObjectGrabber
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// Called by PlaceableObject.OnPlaced.
    /// Restores child Rigidbodies to non-kinematic with HingeJoint limits.
    /// </summary>
    public void OnFigurePlaced()
    {
        _isHeld = false;
        foreach (var rb in _childBodies)
        {
            if (rb.gameObject == gameObject) continue;
            rb.isKinematic = false;
        }
    }

    /// <summary>
    /// Cycle a specific joint to the next preset angle.
    /// Called when the player clicks on a limb segment.
    /// </summary>
    public void CycleJoint(HingeJoint joint)
    {
        if (_isHeld || joint == null) return;

        int idx = System.Array.IndexOf(_joints, joint);
        if (idx < 0) return;

        _angleIndices[idx] = (_angleIndices[idx] + 1) % _presetAngles.Length;
        float target = _presetAngles[_angleIndices[idx]];

        var spring = joint.spring;
        spring.spring = _jointSpringForce;
        spring.damper = _jointDamper;
        spring.targetPosition = target;
        joint.spring = spring;
        joint.useSpring = true;
    }

    /// <summary>
    /// Try cycling a joint on the clicked child collider. Returns true if handled.
    /// </summary>
    public bool TryClickLimb(Collider clickedCollider)
    {
        if (_isHeld) return false;

        var joint = clickedCollider.GetComponent<HingeJoint>();
        if (joint == null)
            joint = clickedCollider.GetComponentInParent<HingeJoint>();

        if (joint == null) return false;

        CycleJoint(joint);
        return true;
    }
}
