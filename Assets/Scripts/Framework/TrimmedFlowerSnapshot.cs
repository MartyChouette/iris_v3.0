using UnityEngine;

/// <summary>
/// Static utility that clones the visible (attached) parts of a trimmed flower
/// into a lightweight visual-only GameObject. No Rigidbody, Collider, or Joint
/// components — pure mesh renderers for apartment decoration.
/// </summary>
public static class TrimmedFlowerSnapshot
{
    /// <summary>
    /// Clone all attached flower parts from the brain into a new root GO.
    /// The result is re-centered so the lowest mesh point sits at Y=0.
    /// Materials are instanced so wilting tint can be applied independently.
    /// </summary>
    public static GameObject Capture(FlowerGameBrain brain)
    {
        if (brain == null)
        {
            Debug.LogWarning("[TrimmedFlowerSnapshot] Brain is null — returning empty GO.");
            return new GameObject("TrimmedFlower_Empty");
        }

        var root = new GameObject("TrimmedFlower");

        // Clone attached parts
        foreach (var part in brain.parts)
        {
            if (part == null || !part.isAttached) continue;
            CloneRenderers(part.transform, root.transform, brain.transform);
        }

        // Clone stem visuals
        if (brain.stem != null && brain.stem.StemAnchor != null)
        {
            // The stem hierarchy is usually the parent of StemAnchor
            Transform stemRoot = brain.stem.StemAnchor.parent != null
                ? brain.stem.StemAnchor.parent
                : brain.stem.StemAnchor;
            CloneRenderers(stemRoot, root.transform, brain.transform);
        }

        // Also clone any renderers directly on the brain root
        CloneRenderers(brain.transform, root.transform, brain.transform, recursive: false);

        // Re-center: shift root so lowest point is at Y=0
        RecenterToGround(root);

        return root;
    }

    private static void CloneRenderers(Transform source, Transform destRoot,
                                        Transform worldRef, bool recursive = true)
    {
        if (source == null) return;

        var renderers = recursive
            ? source.GetComponentsInChildren<MeshRenderer>()
            : source.GetComponents<MeshRenderer>();

        foreach (var rend in renderers)
        {
            var mf = rend.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var clone = new GameObject(rend.gameObject.name);
            clone.transform.SetParent(destRoot, false);

            // Preserve world-space position/rotation relative to brain root
            clone.transform.position = rend.transform.position;
            clone.transform.rotation = rend.transform.rotation;
            clone.transform.localScale = rend.transform.lossyScale;

            // Copy mesh
            var cloneMF = clone.AddComponent<MeshFilter>();
            cloneMF.sharedMesh = mf.sharedMesh;

            // Clone materials so wilting can tint independently
            var cloneRend = clone.AddComponent<MeshRenderer>();
            var srcMats = rend.sharedMaterials;
            var cloneMats = new Material[srcMats.Length];
            for (int i = 0; i < srcMats.Length; i++)
            {
                cloneMats[i] = srcMats[i] != null
                    ? new Material(srcMats[i])
                    : null;
            }
            cloneMats = cloneMats; // suppress unused warning
            cloneRend.sharedMaterials = cloneMats;
        }

        // Also handle SkinnedMeshRenderers (baked to static mesh)
        var skinnedRenderers = recursive
            ? source.GetComponentsInChildren<SkinnedMeshRenderer>()
            : source.GetComponents<SkinnedMeshRenderer>();

        foreach (var smr in skinnedRenderers)
        {
            if (smr.sharedMesh == null) continue;

            // Bake the current pose into a static mesh
            var bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);

            var clone = new GameObject(smr.gameObject.name);
            clone.transform.SetParent(destRoot, false);
            clone.transform.position = smr.transform.position;
            clone.transform.rotation = smr.transform.rotation;
            clone.transform.localScale = smr.transform.lossyScale;

            var cloneMF = clone.AddComponent<MeshFilter>();
            cloneMF.sharedMesh = bakedMesh;

            var cloneRend = clone.AddComponent<MeshRenderer>();
            var srcMats = smr.sharedMaterials;
            var cloneMats = new Material[srcMats.Length];
            for (int i = 0; i < srcMats.Length; i++)
            {
                cloneMats[i] = srcMats[i] != null
                    ? new Material(srcMats[i])
                    : null;
            }
            cloneRend.sharedMaterials = cloneMats;
        }
    }

    private static void RecenterToGround(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // Find the lowest Y across all renderer bounds
        float minY = float.MaxValue;
        foreach (var r in renderers)
        {
            float boundsMinY = r.bounds.min.y;
            if (boundsMinY < minY) minY = boundsMinY;
        }

        // Offset all children down so lowest point is at Y=0
        if (!float.IsInfinity(minY) && Mathf.Abs(minY) > 0.001f)
        {
            foreach (Transform child in root.transform)
            {
                child.position -= new Vector3(0f, minY, 0f);
            }
        }

        // Also center XZ around the average
        float sumX = 0f, sumZ = 0f;
        foreach (var r in renderers)
        {
            sumX += r.bounds.center.x;
            sumZ += r.bounds.center.z;
        }
        float avgX = sumX / renderers.Length;
        float avgZ = sumZ / renderers.Length;

        foreach (Transform child in root.transform)
        {
            child.position -= new Vector3(avgX, 0f, avgZ);
        }
    }
}
