using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LayeredOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public LayerMask outlineLayerMask;
        public Color outlineColor = Color.black;
        [Range(1f, 10f)] public float outlineThickness = 2f;

        [Space(10)]
        public LayerMask blockerLayerMask;
    }

    public OutlineSettings settings = new OutlineSettings();
    private OutlinePass outlinePass;

    public override void Create()
    {
        outlinePass = new OutlinePass(settings);
        outlinePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Only enqueue if we successfully created materials AND we actually have layers selected
        if (outlinePass.SetupMaterials() && (settings.outlineLayerMask != 0 || settings.blockerLayerMask != 0))
        {
            renderer.EnqueuePass(outlinePass);
        }
    }

    class OutlinePass : ScriptableRenderPass
    {
        private readonly OutlineSettings settings;
        private Material outlineShaderMaterial;
        private Material drawOutlineMaterial;
        private Material drawBlockerMaterial;

        private RenderTargetIdentifier cameraColorTarget;

        // Cached Property IDs (Extremely fast for mobile compared to string lookups)
        private static readonly int MaskTextureID = Shader.PropertyToID("_OutlineMaskTexture");
        private static readonly int TempTargetID = Shader.PropertyToID("_TempCameraColor");
        private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int SrcBlendID = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendID = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteID = Shader.PropertyToID("_ZWrite");

        // Static readonly arrays prevent Garbage Collection allocations during gameplay
        private static readonly ShaderTagId[] ShaderTags = {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("LightweightForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("Universal2D")
        };

        private ProfilingSampler profilingSampler = new ProfilingSampler("Android Optimized Outline");

        public OutlinePass(OutlineSettings settings)
        {
            this.settings = settings;
        }

        public bool SetupMaterials()
        {
            if (outlineShaderMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/SimpleOutlineShader");
                if (shader != null) outlineShaderMaterial = new Material(shader);
                else return false;
            }

            if (drawOutlineMaterial == null)
            {
                drawOutlineMaterial = CoreUtils.CreateEngineMaterial("Universal Render Pipeline/Unlit");
                drawOutlineMaterial.SetColor(BaseColorID, Color.red);
            }

            if (drawBlockerMaterial == null)
            {
                drawBlockerMaterial = CoreUtils.CreateEngineMaterial("Universal Render Pipeline/Unlit");
                drawBlockerMaterial.SetColor(BaseColorID, Color.green);
                drawBlockerMaterial.SetInt(SrcBlendID, (int)BlendMode.One);
                drawBlockerMaterial.SetInt(DstBlendID, (int)BlendMode.Zero);
                drawBlockerMaterial.SetInt(ZWriteID, 1);
            }

            outlineShaderMaterial.SetColor(OutlineColorID, settings.outlineColor);
            outlineShaderMaterial.SetFloat(OutlineThicknessID, settings.outlineThickness);

            return true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;

            // 16-bit depth is strictly required for Android mesh rendering
            descriptor.depthBufferBits = 16;
            descriptor.msaaSamples = 1;

            // OPTIMIZATION: Use Point filtering. We are only checking solid Red/Green pixels.
            // Point filtering is computationally cheaper on mobile and prevents edge bleeding.
            cmd.GetTemporaryRT(MaskTextureID, descriptor, FilterMode.Point);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (outlineShaderMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.SetRenderTarget(MaskTextureID);
                // Clear both Color and Depth. This is crucial for Tile-Based GPUs on Android.
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Draw Target Mask
                if (settings.outlineLayerMask != 0)
                {
                    FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all, settings.outlineLayerMask);
                    DrawingSettings drawSettings = CreateDrawingSettings(ShaderTags[0], ref renderingData, SortingCriteria.CommonOpaque);
                    for (int i = 1; i < ShaderTags.Length; i++) drawSettings.SetShaderPassName(i, ShaderTags[i]);

                    drawSettings.overrideMaterial = drawOutlineMaterial;
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
                }

                // Draw Blocker Mask
                if (settings.blockerLayerMask != 0)
                {
                    FilteringSettings blockFilterSettings = new FilteringSettings(RenderQueueRange.all, settings.blockerLayerMask);
                    DrawingSettings blockDrawSettings = CreateDrawingSettings(ShaderTags[0], ref renderingData, SortingCriteria.CommonOpaque);
                    for (int i = 1; i < ShaderTags.Length; i++) blockDrawSettings.SetShaderPassName(i, ShaderTags[i]);

                    blockDrawSettings.overrideMaterial = drawBlockerMaterial;
                    context.DrawRenderers(renderingData.cullResults, ref blockDrawSettings, ref blockFilterSettings);
                }

                cmd.SetGlobalTexture(MaskTextureID, MaskTextureID);

                // Post-Process Blit
                RenderTextureDescriptor cameraDesc = renderingData.cameraData.cameraTargetDescriptor;
                cameraDesc.depthBufferBits = 0;
                cmd.GetTemporaryRT(TempTargetID, cameraDesc, FilterMode.Bilinear); // Screen blit can remain Bilinear

                cmd.Blit(cameraColorTarget, TempTargetID, outlineShaderMaterial, 0);
                cmd.Blit(TempTargetID, cameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);

            cmd.ReleaseTemporaryRT(TempTargetID);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(MaskTextureID);
        }
    }
}