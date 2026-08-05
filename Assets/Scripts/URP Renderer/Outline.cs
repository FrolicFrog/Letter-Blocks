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
        if (outlinePass.SetupMaterials())
        {
            renderer.EnqueuePass(outlinePass);
        }
    }

    class OutlinePass : ScriptableRenderPass
    {
        private OutlineSettings settings;
        private Material outlineShaderMaterial;
        private Material drawOutlineMaterial;
        private Material drawBlockerMaterial;

        private RenderTargetIdentifier cameraColorTarget;
        private int maskTextureID = Shader.PropertyToID("_OutlineMaskTexture");
        private int tempTargetID = Shader.PropertyToID("_TempCameraColor");

        private ShaderTagId[] shaderTags;

        public OutlinePass(OutlineSettings settings)
        {
            this.settings = settings;
            shaderTags = new ShaderTagId[]
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("Universal2D")
            };
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
                drawOutlineMaterial.SetColor("_BaseColor", Color.red);
                drawOutlineMaterial.SetInt("_ZWrite", 0);
                drawOutlineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }

            if (drawBlockerMaterial == null)
            {
                drawBlockerMaterial = CoreUtils.CreateEngineMaterial("Universal Render Pipeline/Unlit");
                drawBlockerMaterial.SetColor("_BaseColor", Color.green);
                drawBlockerMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
                drawBlockerMaterial.SetInt("_DstBlend", (int)BlendMode.Zero);
                drawBlockerMaterial.SetInt("_ZWrite", 0);
                drawBlockerMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }

            outlineShaderMaterial.SetColor("_OutlineColor", settings.outlineColor);
            outlineShaderMaterial.SetFloat("_OutlineThickness", settings.outlineThickness);

            return true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;

            // Restored 16-bit depth so Android Vulkan/GLES doesn't drop the mesh draw calls
            descriptor.depthBufferBits = 16;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;

            cmd.GetTemporaryRT(maskTextureID, descriptor, FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (outlineShaderMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Layered Outline Pass");

            cmd.SetRenderTarget(maskTextureID);

            // Now that depth is 16, we MUST clear it (true) so old data doesn't corrupt the mask
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            if (settings.outlineLayerMask != 0)
            {
                FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.all, settings.outlineLayerMask);
                DrawingSettings drawSettings = CreateDrawingSettings(shaderTags[0], ref renderingData, SortingCriteria.CommonOpaque);
                for (int i = 1; i < shaderTags.Length; i++) drawSettings.SetShaderPassName(i, shaderTags[i]);

                drawSettings.overrideMaterial = drawOutlineMaterial;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
            }

            if (settings.blockerLayerMask != 0)
            {
                FilteringSettings blockFilterSettings = new FilteringSettings(RenderQueueRange.all, settings.blockerLayerMask);
                DrawingSettings blockDrawSettings = CreateDrawingSettings(shaderTags[0], ref renderingData, SortingCriteria.CommonOpaque);
                for (int i = 1; i < shaderTags.Length; i++) blockDrawSettings.SetShaderPassName(i, shaderTags[i]);

                blockDrawSettings.overrideMaterial = drawBlockerMaterial;
                context.DrawRenderers(renderingData.cullResults, ref blockDrawSettings, ref blockFilterSettings);
            }

            cmd.SetGlobalTexture("_OutlineMaskTexture", maskTextureID);

            RenderTextureDescriptor cameraDesc = renderingData.cameraData.cameraTargetDescriptor;
            cameraDesc.depthBufferBits = 0;
            cmd.GetTemporaryRT(tempTargetID, cameraDesc, FilterMode.Bilinear);

            cmd.Blit(cameraColorTarget, tempTargetID, outlineShaderMaterial, 0);
            cmd.Blit(tempTargetID, cameraColorTarget);

            context.ExecuteCommandBuffer(cmd);
            cmd.ReleaseTemporaryRT(tempTargetID);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(maskTextureID);
        }
    }
}