using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[Serializable]
public class EvsPipeline : BasePipeline
{
    public EvsEnhancePipelineModule EvsEnchanceModule { get; private set; }

    private IDisposable _enhancerUnsubscriber;
    private IDisposable _drawerUnsubscriber;

    public EvsPipeline(DisplayData displayData) : base(displayData)
    {
        EvsEnchanceModule = new(this);
    }

    protected override void Setup()
    {
        if (DisplayData.RenderingLayersDatas.TryGetValue(RenderLayer.Visible, out var inputLayerData))
            InputRenderTexture = inputLayerData.RT;

        if (DisplayData.RenderingLayersDatas.TryGetValue(RenderLayer.EVS, out var outputLayerData))
            OutputRenderTexture = outputLayerData.RT;

        ProcessRenderTexture = new(outputLayerData.Settings.RTWidth, outputLayerData.Settings.RTHeight, 0, GraphicsFormat.R8_UNorm)
        {
            name = $"Process Render Texture",
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = true
        };
        ProcessRenderTexture.Create();

        _enhancerUnsubscriber = TakeModule.Subscribe(EvsEnchanceModule);
        _drawerUnsubscriber = EvsEnchanceModule.Subscribe(DrawModule);
    }

    public override RenderTexture GetProcessRenderTexture()
    {
        ProcessRenderTexture.Reinitialize(OutputRenderTexture.width, OutputRenderTexture.height, GraphicsFormat.R8_UNorm);
        Graphics.Blit(InputRenderTexture, ProcessRenderTexture);
        return ProcessRenderTexture;
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _enhancerUnsubscriber?.Dispose();
        _drawerUnsubscriber?.Dispose();
    }
}