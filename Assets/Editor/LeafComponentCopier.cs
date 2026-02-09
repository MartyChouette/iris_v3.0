using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

/// <summary>
/// One-shot editor tool: copies all components from Daisy_Leaf_L to Daisy_Leaf_R
/// in the active scene, then re-wires references (connectedBody, PartId).
///
/// Menu: Window > Iris > Copy Leaf L → Leaf R
/// </summary>
public static class LeafComponentCopier
{
    [MenuItem("Window/Iris/Copy Leaf L → Leaf R")]
    public static void CopyLeafLToR()
    {
        // 1. Find both leaves in scene hierarchy
        var leafL = FindInScene("Daisy_Leaf_L");
        var leafR = FindInScene("Daisy_Leaf_R");

        if (leafL == null) { Debug.LogError("[LeafCopier] Could not find 'Daisy_Leaf_L' in scene."); return; }
        if (leafR == null) { Debug.LogError("[LeafCopier] Could not find 'Daisy_Leaf_R' in scene."); return; }

        // 2. Find attachment points for reference fixup
        var rAttach = FindInScene("R_Leaf_AttachmentPoint");
        Rigidbody rAttachRb = rAttach != null ? rAttach.GetComponent<Rigidbody>() : null;

        Undo.SetCurrentGroupName("Copy Leaf L → Leaf R");
        int undoGroup = Undo.GetCurrentGroup();

        // 3. Strip all existing MonoBehaviours on Leaf_R (keep Transform, MeshFilter, MeshRenderer, Collider, Rigidbody)
        var existingMBs = leafR.GetComponents<MonoBehaviour>();
        foreach (var mb in existingMBs)
        {
            if (mb != null)
                Undo.DestroyObjectImmediate(mb);
        }

        // 4. Copy each MonoBehaviour from Leaf_L → Leaf_R
        var sourceMBs = leafL.GetComponents<MonoBehaviour>();
        foreach (var src in sourceMBs)
        {
            if (src == null) continue;
            ComponentUtility.CopyComponent(src);
            ComponentUtility.PasteComponentAsNew(leafR);
        }

        // 5. Fix up references on the new components
        // XYTetherJoint: connectedBody → R_Leaf_AttachmentPoint's Rigidbody
        var tether = leafR.GetComponent<XYTetherJoint>();
        if (tether != null && rAttachRb != null)
        {
            var so = new SerializedObject(tether);
            var cb = so.FindProperty("connectedBody");
            if (cb != null)
            {
                cb.objectReferenceValue = rAttachRb;
                so.ApplyModifiedProperties();
                Debug.Log($"[LeafCopier] XYTetherJoint.connectedBody → {rAttach.name}");
            }
        }
        else if (tether != null && rAttachRb == null)
        {
            Debug.LogWarning("[LeafCopier] R_Leaf_AttachmentPoint not found or has no Rigidbody — connectedBody not wired.");
        }

        // FlowerPartRuntime: fix PartId to use Leaf_R's name
        var part = leafR.GetComponent<FlowerPartRuntime>();
        if (part != null)
        {
            var so = new SerializedObject(part);
            var pidProp = so.FindProperty("PartId");
            if (pidProp != null)
            {
                pidProp.stringValue = "Leaf_Daisy_Leaf_R";
                so.ApplyModifiedProperties();
                Debug.Log("[LeafCopier] FlowerPartRuntime.PartId → Leaf_Daisy_Leaf_R");
            }
        }

        // Ensure Leaf_R has a Rigidbody (should already from prefab, but just in case)
        if (leafR.GetComponent<Rigidbody>() == null)
        {
            var rb = Undo.AddComponent<Rigidbody>(leafR);
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Debug.Log("[LeafCopier] Added Rigidbody to Leaf_R");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(leafR);

        // Summary
        var finalMBs = leafR.GetComponents<MonoBehaviour>();
        Debug.Log($"[LeafCopier] Done! Copied {sourceMBs.Length} MonoBehaviours from '{leafL.name}' → '{leafR.name}'. " +
                  $"Leaf_R now has {finalMBs.Length} MonoBehaviours. Save the scene to persist.");
    }

    [MenuItem("Window/Iris/Copy Leaf L → Leaf R", true)]
    public static bool CopyLeafLToR_Validate()
    {
        return !Application.isPlaying;
    }

    [MenuItem("Window/Iris/Diagnose All Leaves")]
    public static void DiagnoseLeaves()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int leafCount = 0;

        foreach (var root in roots)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (!t.name.ToLowerInvariant().Contains("leaf")) continue;
                leafCount++;
                var go = t.gameObject;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== LEAF: '{go.name}' (active={go.activeInHierarchy}, layer={LayerMask.LayerToName(go.layer)}) ===");

                // Rigidbody
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) sb.AppendLine("  Rigidbody: MISSING");
                else sb.AppendLine($"  Rigidbody: mass={rb.mass}, kinematic={rb.isKinematic}, constraints={rb.constraints}");

                // Colliders
                var colliders = go.GetComponents<Collider>();
                sb.AppendLine($"  Colliders: {colliders.Length}");
                foreach (var c in colliders)
                    sb.AppendLine($"    - {c.GetType().Name}: enabled={c.enabled}, isTrigger={c.isTrigger}");

                // XYTetherJoint
                var tethers = go.GetComponents<XYTetherJoint>();
                sb.AppendLine($"  XYTetherJoint count: {tethers.Length}");
                foreach (var tj in tethers)
                {
                    var so = new SerializedObject(tj);
                    var cb = so.FindProperty("connectedBody");
                    string cbName = cb?.objectReferenceValue != null ? cb.objectReferenceValue.name : "NULL";
                    var bf = so.FindProperty("breakForce");
                    var md = so.FindProperty("maxDistance");
                    var dl = so.FindProperty("debugLogs");
                    var cr = so.FindProperty("criteria");
                    var ejc = so.FindProperty("enableJointCollision");
                    sb.AppendLine($"    - enabled={tj.enabled}, connectedBody={cbName}, breakForce={bf?.floatValue}, maxDistance={md?.floatValue}, criteria={cr?.intValue}, enableJointCollision={ejc?.boolValue}, debugLogs={dl?.boolValue}");
                }

                // FlowerPartRuntime
                var parts = go.GetComponents<FlowerPartRuntime>();
                sb.AppendLine($"  FlowerPartRuntime count: {parts.Length}");
                foreach (var p in parts)
                {
                    var so = new SerializedObject(p);
                    var pid = so.FindProperty("PartId");
                    sb.AppendLine($"    - PartId={pid?.stringValue}, isAttached={p.isAttached}, kind={p.kind}");
                }

                // FlowerBreathing
                var breathing = go.GetComponents<FlowerBreathing>();
                sb.AppendLine($"  FlowerBreathing count: {breathing.Length}");
                foreach (var b in breathing)
                    sb.AppendLine($"    - enabled={b.enabled}");

                // GrabPull
                var grabs = go.GetComponents<GrabPull>();
                sb.AppendLine($"  GrabPull count: {grabs.Length}");
                foreach (var g in grabs)
                    sb.AppendLine($"    - enabled={g.enabled}");

                // All MonoBehaviours summary
                var allMBs = go.GetComponents<MonoBehaviour>();
                sb.AppendLine($"  Total MonoBehaviours: {allMBs.Length}");
                foreach (var mb in allMBs)
                {
                    if (mb == null)
                        sb.AppendLine("    - [MISSING SCRIPT]");
                    else
                        sb.AppendLine($"    - {mb.GetType().Name} (enabled={mb.enabled})");
                }

                Debug.Log(sb.ToString(), go);
            }
        }

        if (leafCount == 0)
            Debug.LogWarning("[Diagnose] No GameObjects with 'leaf' in their name found in scene.");
        else
            Debug.Log($"[Diagnose] Checked {leafCount} leaf GameObjects. See above for details.");
    }

    [MenuItem("Window/Iris/Setup Sap System")]
    public static void SetupSapSystem()
    {
        Undo.SetCurrentGroupName("Setup Sap System");
        int undoGroup = Undo.GetCurrentGroup();
        int added = 0;

        // 1. Find the flower root (has FlowerSessionController)
        var session = Object.FindFirstObjectByType<FlowerSessionController>();
        if (session == null)
        {
            Debug.LogError("[SapSetup] No FlowerSessionController found in scene. Open a flower scene first.");
            return;
        }
        var flowerRoot = session.gameObject;

        // 2. FlowerSapController — must be on flower root (stem cut uses GetComponentInParent)
        var sapCtrl = flowerRoot.GetComponentInChildren<FlowerSapController>(true);
        if (sapCtrl == null)
        {
            sapCtrl = Object.FindFirstObjectByType<FlowerSapController>();
            if (sapCtrl != null)
            {
                Debug.LogWarning($"[SapSetup] FlowerSapController found on '{sapCtrl.name}' but NOT on flower root. Moving it to '{flowerRoot.name}'.");
                Undo.DestroyObjectImmediate(sapCtrl);
                sapCtrl = null;
            }
        }
        if (sapCtrl == null)
        {
            sapCtrl = Undo.AddComponent<FlowerSapController>(flowerRoot);
            Debug.Log($"[SapSetup] Added FlowerSapController to '{flowerRoot.name}'");
            added++;
        }
        else
        {
            Debug.Log($"[SapSetup] FlowerSapController already on '{sapCtrl.gameObject.name}' OK");
        }

        // 3. SapParticleController — singleton, can be anywhere (put on a dedicated GO)
        var particleCtrl = Object.FindFirstObjectByType<SapParticleController>();
        if (particleCtrl == null)
        {
            var go = new GameObject("SapParticleController");
            Undo.RegisterCreatedObjectUndo(go, "Create SapParticleController");
            particleCtrl = Undo.AddComponent<SapParticleController>(go);
            Debug.Log("[SapSetup] Created SapParticleController GameObject");
            added++;
        }
        else
        {
            Debug.Log($"[SapSetup] SapParticleController already on '{particleCtrl.gameObject.name}' OK");
        }

        // 4. SapDecalPool — singleton, can be anywhere (put on a dedicated GO)
        var decalPool = Object.FindFirstObjectByType<SapDecalPool>();
        if (decalPool == null)
        {
            var go = new GameObject("SapDecalPool");
            Undo.RegisterCreatedObjectUndo(go, "Create SapDecalPool");
            decalPool = Undo.AddComponent<SapDecalPool>(go);
            Debug.Log("[SapSetup] Created SapDecalPool GameObject");
            added++;
        }
        else
        {
            Debug.Log($"[SapSetup] SapDecalPool already on '{decalPool.gameObject.name}' OK");
        }

        // 5. Wire JointBreakFluidResponder on all leaves/petals to the sap controller
        var responders = Object.FindObjectsByType<JointBreakFluidResponder>(FindObjectsSortMode.None);
        int wired = 0;
        foreach (var r in responders)
        {
            var so = new SerializedObject(r);
            var prop = so.FindProperty("sapController");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = sapCtrl;
                so.ApplyModifiedProperties();
                wired++;
            }
        }
        if (wired > 0)
            Debug.Log($"[SapSetup] Wired FlowerSapController to {wired} JointBreakFluidResponders");

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[SapSetup] Done! Added {added} new component(s), wired {wired} responder(s). Save the scene to persist.");
    }

    private static GameObject FindInScene(string name)
    {
        // Search all root objects and their children
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t.name == name)
                    return t.gameObject;
            }
        }
        return null;
    }
}
