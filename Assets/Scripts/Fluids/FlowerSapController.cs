/**
 * @file FlowerSapController.cs
 * @brief FlowerSapController script.
 * @details
 * Routes sap emission calls to SapParticleController (lightweight, GPU-accelerated).
 *
 * @ingroup fluids
 */

using UnityEngine;

public class FlowerSapController : MonoBehaviour
{
    public static FlowerSapController Instance;

    [System.Serializable]
    public class SapBurstProfile
    {
        public float speed = 10f;
        public float duration = 0.2f;
        public float angleJitter = 5f;
    }

    [Header("Stem Cut Settings")]
    public float stemEndOffset = 0.02f;
    public SapBurstProfile stemTopBurst = new SapBurstProfile { speed = 18f, duration = 0.25f, angleJitter = 8f };
    public SapBurstProfile stemBottomBurst = new SapBurstProfile { speed = 12f, duration = 0.20f, angleJitter = 6f };

    [Header("Leaf / Petal Tear Settings")]
    public SapBurstProfile leafTearBurst = new SapBurstProfile { speed = 8f, duration = 0.18f, angleJitter = 12f };
    public SapBurstProfile petalTearBurst = new SapBurstProfile { speed = 6f, duration = 0.15f, angleJitter = 15f };

    [Header("Global Gore / Intensity")]
    [Min(0f)] public float sapIntensity = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal, FlowerStemRuntime stem)
    {
        if (sapIntensity <= 0f) { Debug.Log("[FlowerSapController] EmitStemCut skipped: sapIntensity=0"); return; }

        if (SapParticleController.Instance != null)
        {
            Debug.Log($"[FlowerSapController] EmitStemCut at {planePoint}");
            SapParticleController.Instance.EmitStemCut(planePoint, planeNormal, stem);
        }
        else
        {
            Debug.LogWarning("[FlowerSapController] EmitStemCut: SapParticleController.Instance is NULL");
        }
    }

    public void EmitLeafTear(Vector3 pos, Vector3 normal, Transform followTarget = null)
    {
        if (sapIntensity <= 0f) { Debug.Log("[FlowerSapController] EmitLeafTear skipped: sapIntensity=0"); return; }

        if (SapParticleController.Instance != null)
        {
            Debug.Log($"[FlowerSapController] EmitLeafTear at {pos}");
            SapParticleController.Instance.EmitLeafTear(pos, normal, followTarget);
        }
        else
        {
            Debug.LogWarning("[FlowerSapController] EmitLeafTear: SapParticleController.Instance is NULL");
        }
    }

    public void EmitPetalTear(Vector3 pos, Vector3 normal, Transform followTarget = null)
    {
        if (sapIntensity <= 0f) { Debug.Log("[FlowerSapController] EmitPetalTear skipped: sapIntensity=0"); return; }

        if (SapParticleController.Instance != null)
        {
            Debug.Log($"[FlowerSapController] EmitPetalTear at {pos}");
            SapParticleController.Instance.EmitPetalTear(pos, normal, followTarget);
        }
        else
        {
            Debug.LogWarning("[FlowerSapController] EmitPetalTear: SapParticleController.Instance is NULL");
        }
    }
}
