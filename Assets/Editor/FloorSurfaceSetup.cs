using UnityEditor;
using UnityEngine;

public static class FloorSurfaceSetup
{
    [MenuItem("Window/Iris/Add Floor Placement Surface")]
    public static void AddFloorSurface()
    {
        var go = new GameObject("FloorPlacementSurface");
        go.layer = 0; // Default — keeps solid physics collision
        go.transform.position = new Vector3(-1f, 0f, 0f);

        // Solid collider so items don't fall through
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.center = Vector3.zero;
        col.size = new Vector3(14f, 0.1f, 18f);

        // PlacementSurface — trigger child auto-created on layer 22 at runtime
        var surface = go.AddComponent<PlacementSurface>();
        var so = new SerializedObject(surface);
        var bounds = so.FindProperty("localBounds");
        bounds.FindPropertyRelative("m_Center").vector3Value = Vector3.zero;
        bounds.FindPropertyRelative("m_Extent").vector3Value = new Vector3(7f, 0.05f, 9f);
        so.FindProperty("normalAxis").enumValueIndex = 0; // Up
        so.FindProperty("surfaceLayerIndex").intValue = 22;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(go, "Add Floor Placement Surface");
        Selection.activeGameObject = go;

        Debug.Log("[FloorSurfaceSetup] Floor placement surface added at (-1, 0, 0), 14x18m. Adjust position/bounds as needed.");
    }
}
