using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

class MultilayerOverlayPass : CustomPass
{
    [SerializeField, DisableEdit] private Material _overlayMaterial;
    private bool _isFirstExecute = true;


    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        Shader shader = Shader.Find("Hidden/HDRP/Multilayer Overlay Shader");
        if (shader != null)
            _overlayMaterial = CoreUtils.CreateEngineMaterial(shader);
        else
            Debug.LogError("<color=red>Overlay shader is not found! Overlay pass is not initialized!</color>");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (!_overlayMaterial) return;

        if (_isFirstExecute)
        {
            if (DisplaysManager.Instance.DisplaysDatas.TryGetValue((TargetDisplay)ctx.hdCamera.camera.targetDisplay, out DisplayData displayData))
            {
                foreach (RenderLayerData renderLayerData in displayData.RenderingLayersDatas.Values)
                {
                    SetRenderTextureInMaterial(renderLayerData);
                }

                _isFirstExecute = false;
            }
            else
            {
                Debug.LogError($"<color=red>Display Data - {(TargetDisplay)ctx.hdCamera.camera.targetDisplay} is not found! Overlay material for {(TargetDisplay)ctx.hdCamera.camera.targetDisplay} is not initialized with Display RTs!</color>");
            }
        }

        CoreUtils.DrawFullScreen(ctx.cmd, _overlayMaterial);
    }

    private void SetRenderTextureInMaterial(RenderLayerData renderLayerData)
    {
        string textureParameterName;
        switch (renderLayerData.Settings.RenderLayer)
        {
            case RenderLayer.Visible:
                textureParameterName = "_Visible_Texture";
                break;
            case RenderLayer.LWIR:
                textureParameterName = "_LWIR_Texture";
                break;
            case RenderLayer.Labels:
                textureParameterName = "_Labels_Texture";
                break;
            case RenderLayer.SWIR:
                textureParameterName = "_SWIR_Texture";
                break;
            case RenderLayer.EVS:
                textureParameterName = "_EVS_Texture";
                break;
            default:
                return;
        }

        _overlayMaterial.SetTexture(textureParameterName, renderLayerData.RT);
    }

    protected override void Cleanup()
    {
        if (_overlayMaterial)
            CoreUtils.Destroy(_overlayMaterial);
    }
}