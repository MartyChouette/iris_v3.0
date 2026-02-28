using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(TidyScorer))]
public class TidyScorerEditor : Editor
{
    private BoxBoundsHandle _kitchenHandle;
    private BoxBoundsHandle _livingRoomHandle;
    private BoxBoundsHandle _entranceHandle;

    private static readonly Color KitchenColor = new Color(1f, 0.6f, 0.2f, 0.9f);
    private static readonly Color LivingRoomColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    private static readonly Color EntranceColor = new Color(0.3f, 0.9f, 0.4f, 0.9f);

    private void OnEnable()
    {
        _kitchenHandle = new BoxBoundsHandle { handleColor = KitchenColor, wireframeColor = KitchenColor };
        _livingRoomHandle = new BoxBoundsHandle { handleColor = LivingRoomColor, wireframeColor = LivingRoomColor };
        _entranceHandle = new BoxBoundsHandle { handleColor = EntranceColor, wireframeColor = EntranceColor };
    }

    private void OnSceneGUI()
    {
        var scorer = (TidyScorer)target;

        EditorGUI.BeginChangeCheck();

        DrawBoundsHandle(scorer, _kitchenHandle, scorer.KitchenBounds, KitchenColor, out Bounds newKitchen);
        DrawBoundsHandle(scorer, _livingRoomHandle, scorer.LivingRoomBounds, LivingRoomColor, out Bounds newLiving);
        DrawBoundsHandle(scorer, _entranceHandle, scorer.EntranceBounds, EntranceColor, out Bounds newEntrance);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(scorer, "Edit TidyScorer Area Bounds");
            scorer.KitchenBounds = newKitchen;
            scorer.LivingRoomBounds = newLiving;
            scorer.EntranceBounds = newEntrance;
            EditorUtility.SetDirty(scorer);
        }
    }

    private static void DrawBoundsHandle(TidyScorer scorer, BoxBoundsHandle handle, Bounds bounds, Color color, out Bounds result)
    {
        handle.center = bounds.center;
        handle.size = bounds.size;

        using (new Handles.DrawingScope(color))
        {
            handle.DrawHandle();
        }

        result = new Bounds(handle.center, handle.size);
    }
}
