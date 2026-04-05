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

        // Clone stem visuals — check spline generator's mesh filter first,
        // then fall back to the StemAnchor hierarchy
        if (brain.stem != null)
        {
            bool stemCaptured = false;

            // Try the spline generator's explicit mesh filter
            if (brain.stem.splineGenerator != null && brain.stem.splineGenerator.stemMeshFilter != null)
            {
                CloneRenderers(brain.stem.splineGenerator.stemMeshFilter.transform, root.transform, brain.transform, recursive: false);
                stemCaptured = true;
            }

            // Also capture the stem hierarchy (may have additional renderers)
            if (brain.stem.StemAnchor != null)
            {
                Transform stemRoot = brain.stem.StemAnchor.parent != null
                    ? brain.stem.StemAnchor.parent
                    : brain.stem.StemAnchor;
                CloneRenderers(stemRoot, root.transform, brain.transform);
                stemCaptured = true;
            }

            // Last resort: capture from the stem component's own transform
            if (!stemCaptured)
                CloneRenderers(brain.stem.transform, root.transform, brain.transform);
        }

        // Also clone any renderers directly on the brain root
        CloneRenderers(brain.transform, root.transform, brain.transform, recursive: false);

        // Strip particle systems and fluid components so they don't produce artifacts
        StripParticles(root);

        // Auto-upright: if the flower is upside down (stem tip above anchor),
        // flip the entire snapshot 180 degrees around the X axis
        AutoUpright(root, brain);

        // Re-center: shift root so lowest point is at Y=0
        RecenterToGround(root);

        int rendererCount = root.GetComponentsInChildren<Renderer>().Length;
        Debug.Log($"[TrimmedFlowerSnapshot] Captured {rendererCount} renderer(s) from flower brain.");

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

            // Store position/rotation RELATIVE to brain root so scene offset doesn't matter
            clone.transform.localPosition = worldRef.InverseTransformPoint(rend.transform.position);
            clone.transform.localRotation = Quaternion.Inverse(worldRef.rotation) * rend.transform.rotation;
            clone.transform.localScale = rend.transform.lossyScale;

            // Deep-copy mesh so it survives flower scene unload
            var cloneMF = clone.AddComponent<MeshFilter>();
            cloneMF.sharedMesh = DeepCopyMesh(mf.sharedMesh);

            // Deep-copy materials so wilting can tint independently
            var cloneRend = clone.AddComponent<MeshRenderer>();
            cloneRend.sharedMaterials = DeepCopyMaterials(rend.sharedMaterials);
        }

        // Also handle SkinnedMeshRenderers (baked to static mesh)
        var skinnedRenderers = recursive
            ? source.GetComponentsInChildren<SkinnedMeshRenderer>()
            : source.GetComponents<SkinnedMeshRenderer>();

        foreach (var smr in skinnedRenderers)
        {
            if (smr.sharedMesh == null) continue;

            // Bake the current pose into a new static mesh (already a deep copy)
            var bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);

            var clone = new GameObject(smr.gameObject.name);
            clone.transform.SetParent(destRoot, false);
            clone.transform.localPosition = worldRef.InverseTransformPoint(smr.transform.position);
            clone.transform.localRotation = Quaternion.Inverse(worldRef.rotation) * smr.transform.rotation;
            clone.transform.localScale = smr.transform.lossyScale;

            var cloneMF = clone.AddComponent<MeshFilter>();
            cloneMF.sharedMesh = bakedMesh;

            var cloneRend = clone.AddComponent<MeshRenderer>();
            cloneRend.sharedMaterials = DeepCopyMaterials(smr.sharedMaterials);
        }
    }

    /// <summary>
    /// Destroy all particle systems and fluid-related components on the snapshot clone
    /// so no live emitters transfer to the apartment living plant.
    /// </summary>
    private static void StripParticles(GameObject root)
    {
        // Destroy ParticleSystems first (and their renderers)
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Object.DestroyImmediate(ps);
        }

        // Destroy ParticleSystemRenderers left behind
        foreach (var psr in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            Object.DestroyImmediate(psr);

        // Destroy known fluid components by name (avoid hard dependency on types that may not exist)
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (typeName == "SapParticleController" || typeName == "FluidSquirter"
                || typeName == "SapDecalPool")
            {
                Object.DestroyImmediate(mb);
            }
        }
    }

    /// <summary>
    /// Detect if the snapshot is upside down by checking if the bulk of the
    /// mesh is below the center. If so, flip 180 degrees on X.
    /// Also handles flowers that hang downward in the trimming scene.
    /// </summary>
    private static void AutoUpright(GameObject root, FlowerGameBrain brain)
    {
        // Strategy: check if brain's stem goes upward or downward.
        // In the flower scene, if StemAnchor is ABOVE StemTip, the flower hangs down
        // and the snapshot (in brain-local space) will be inverted.
        if (brain.stem != null && brain.stem.StemAnchor != null && brain.stem.StemTip != null)
        {
            // Compare in brain-local space (same space as our snapshot)
            Vector3 anchorLocal = brain.transform.InverseTransformPoint(brain.stem.StemAnchor.position);
            Vector3 tipLocal = brain.transform.InverseTransformPoint(brain.stem.StemTip.position);

            // If the stem tip is above the anchor in local space, the stem points up — correct orientation
            // If the anchor is above the tip, the flower hangs down — needs flip
            if (anchorLocal.y > tipLocal.y + 0.01f)
            {
                // Flip all children 180 on X
                foreach (Transform child in root.transform)
                {
                    child.localPosition = new Vector3(child.localPosition.x, -child.localPosition.y, child.localPosition.z);
                    child.localRotation = Quaternion.Euler(180f, 0f, 0f) * child.localRotation;
                }
                Debug.Log("[TrimmedFlowerSnapshot] Auto-flipped upside-down flower.");
            }
        }
        else
        {
            // No stem reference — check bounds center vs. renderers
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            float minY = float.MaxValue, maxY = float.MinValue;
            float totalWeightAbove = 0f, totalWeightBelow = 0f;
            foreach (var r in renderers)
            {
                if (r.bounds.min.y < minY) minY = r.bounds.min.y;
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            }
            float midY = (minY + maxY) * 0.5f;
            foreach (var r in renderers)
            {
                float vol = r.bounds.size.x * r.bounds.size.y * r.bounds.size.z;
                if (r.bounds.center.y > midY)
                    totalWeightAbove += vol;
                else
                    totalWeightBelow += vol;
            }

            // If most mesh volume is below center, it's likely upside down
            // (flower head should be at top, which is the bulkier part)
            if (totalWeightBelow > totalWeightAbove * 1.5f)
            {
                foreach (Transform child in root.transform)
                {
                    child.localPosition = new Vector3(child.localPosition.x, -child.localPosition.y, child.localPosition.z);
                    child.localRotation = Quaternion.Euler(180f, 0f, 0f) * child.localRotation;
                }
                Debug.Log("[TrimmedFlowerSnapshot] Auto-flipped (heuristic: bulk below center).");
            }
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

    /// <summary>
    /// Deep-copy a mesh so it has zero references to the source scene.
    /// The copy is a standalone runtime asset that survives scene unloads.
    /// </summary>
    private static Mesh DeepCopyMesh(Mesh source)
    {
        var copy = new Mesh();
        copy.name = source.name + "_snap";
        copy.vertices = source.vertices;
        copy.normals = source.normals;
        copy.tangents = source.tangents;
        copy.uv = source.uv;
        copy.uv2 = source.uv2;
        copy.colors32 = source.colors32;
        copy.boneWeights = source.boneWeights;
        copy.bindposes = source.bindposes;

        copy.subMeshCount = source.subMeshCount;
        for (int i = 0; i < source.subMeshCount; i++)
            copy.SetTriangles(source.GetTriangles(i), i);

        copy.RecalculateBounds();
        return copy;
    }

    /// <summary>
    /// Deep-copy a material array. Each material is a new instance with
    /// all properties copied from the source — no shared references.
    /// </summary>
    private static Material[] DeepCopyMaterials(Material[] source)
    {
        var copy = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
            copy[i] = source[i] != null ? new Material(source[i]) : null;
        return copy;
    }
}
