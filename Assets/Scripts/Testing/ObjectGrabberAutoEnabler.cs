using UnityEngine;

/// <summary>
/// Calls ObjectGrabber.SetEnabled(true) on Start.
/// Used in test scenes that lack ApartmentManager (which normally controls grabber state).
/// </summary>
[RequireComponent(typeof(ObjectGrabber))]
public class ObjectGrabberAutoEnabler : MonoBehaviour
{
    private void Start()
    {
        GetComponent<ObjectGrabber>().SetEnabled(true);
    }
}
