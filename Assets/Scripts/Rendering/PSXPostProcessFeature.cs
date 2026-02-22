using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// URP ScriptableRendererFeature that handles PSX-style post-processing:
/// resolution downscale (pixelation) + color depth reduction + ordered dithering.
/// Add this to the URP Renderer asset's feature list.
/// </summary>
public class PSXPostProcessFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Resolution")]
        [Tooltip("Divide screen resolution by this value. Higher = chunkier pixels.")]
        [Range(1, 6)] public int resolutionDivisor = 3;

        [Header("Color")]
        [Tooltip("Color levels per channel (lower = more posterized).")]
        [Range(4, 256)] public float colorDepth = 32f;

        [Tooltip("Ordered dither strength.")]
        [Range(0, 1)] public float ditherIntensity = 0.5f;
    }

    public Settings settings = new Settings();

    [Header("Shader Reference")]
    [Tooltip("Drag PSXPost shader here. Shader.Find does not work in builds.")]
    [SerializeField] private Shader _postShader;

    private PSXPostProcessPass _pass;
    private Material _material;

    public override void Create()
    {
        var shader = _postShader != null ? _postShader : Shader.Find("Iris/Fullscreen/PSXPost");
        if (shader == null)
        {
            Debug.LogWarning("[PSXPostProcessFeature] Shader 'Iris/Fullscreen/PSXPost' not found. " +
                             "Assign it in the Renderer asset's feature settings.");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(shader);
        _pass = new PSXPostProcessPass(_material, settings);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null)
            return;

        _pass.UpdateSettings(settings);
        _pass.requiresIntermediateTexture = true;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    // ─────────────────────────────────────────────────────────────────
    // Render Pass (RenderGraph API for Unity 6 URP)
    // ─────────────────────────────────────────────────────────────────
    class PSXPostProcessPass : ScriptableRenderPass
    {
        private Material _material;
        private Settings _settings;

        private static readonly int ColorDepthID = Shader.PropertyToID("_ColorDepth");
        private static readonly int DitherIntensityID = Shader.PropertyToID("_DitherIntensity");
        private static readonly int DitherResolutionID = Shader.PropertyToID("_DitherResolution");

        public PSXPostProcessPass(Material material, Settings settings)
        {
            _material = material;
            _settings = settings;
            profilingSampler = new ProfilingSampler("PSX Post Process");
        }

        public void UpdateSettings(Settings settings)
        {
            _settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Cannot blit from the backbuffer directly
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var source = resourceData.activeColorTexture;
            var sourceDesc = renderGraph.GetTextureDesc(source);

            // Update material properties
            _material.SetFloat(ColorDepthID, _settings.colorDepth);
            _material.SetFloat(DitherIntensityID, _settings.ditherIntensity);

            int div = Mathf.Max(_settings.resolutionDivisor, 1);
            int lowW = Mathf.Max(1, sourceDesc.width / div);
            int lowH = Mathf.Max(1, sourceDesc.height / div);
            _material.SetVector(DitherResolutionID, new Vector4(lowW, lowH, 0f, 0f));

            // ── Step 1: Downscale camera → low-res (plain copy) ──
            var lowResDesc = new TextureDesc(
                Mathf.Max(1, sourceDesc.width / div),
                Mathf.Max(1, sourceDesc.height / div))
            {
                colorFormat = sourceDesc.colorFormat,
                filterMode = FilterMode.Point,
                name = "_PSXLowRes"
            };
            var lowRes = renderGraph.CreateTexture(lowResDesc);

            renderGraph.AddBlitPass(source, lowRes, Vector2.one, Vector2.zero,
                passName: "PSX Downscale");

            // ── Step 2: Apply shader + upscale low-res → destination ──
            var destDesc = new TextureDesc(sourceDesc.width, sourceDesc.height)
            {
                colorFormat = sourceDesc.colorFormat,
                filterMode = FilterMode.Point,
                name = "_PSXOutput"
            };
            var dest = renderGraph.CreateTexture(destDesc);

            var blitParams = new RenderGraphUtils.BlitMaterialParameters(
                lowRes, dest, _material, 0);
            renderGraph.AddBlitPass(blitParams, passName: "PSX Color Reduce");

            // Replace the active color texture with our processed output
            resourceData.cameraColor = dest;
        }
    }
}
