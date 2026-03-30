using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[Serializable]
public class RenderLayerData : IDisposable
{
    [Serializable]
    public class DataSettings
    {
        [field: SerializeField, HideInInspector] public string Info { get; private set; }
        [field: SerializeField, DisableEdit] public RenderLayer RenderLayer { get; private set; }
        [field: SerializeField] public bool ExistOnDisplay { get; private set; }
        [field: SerializeField, Min(360)] public int RTWidth { get; private set; }
        [field: SerializeField, Min(360)] public int RTHeight { get; private set; }
        [field: SerializeField] public GraphicsFormat RTFormat { get; private set; }


        public DataSettings(RenderLayer renderLayer = RenderLayer.Visible, bool existOnDisplay = true, int width = 1920, int height = 1080, GraphicsFormat format = GraphicsFormat.R8G8B8A8_SRGB)
        {
            RenderLayer = renderLayer;
            ExistOnDisplay = existOnDisplay;
            RTWidth = Mathf.Clamp(width, 360, 8192);
            RTHeight = Mathf.Clamp(height, 360, 8192);
            RTFormat = format;

            UpdateInfoField();
        }

        public void UpdateInfoField() => Info = $"{RenderLayer} | {(ExistOnDisplay ? "OnDisplay" : "Disabled")} | {RTWidth}x{RTHeight} | {RTFormat}";
    }

    [field: SerializeField] public DataSettings Settings { get; private set; } = new();
    [field: SerializeField] public RenderTexture RT { get; private set; }


    public RenderLayerData(DataSettings settings)
    {
        Settings = settings;

        RT = new(settings.RTWidth, settings.RTHeight, 0, settings.RTFormat)
        {
            name = $"{settings.RenderLayer} RT",
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = true
        };
        RT.Create();
    }

    /// <summary> Set render layer data settings </summary>
    public void SetDataSettings(int width, int height, GraphicsFormat format)
    {
        Settings = new(Settings.RenderLayer, Settings.ExistOnDisplay, width, height, format);
        RT.Reinitialize(Settings.RTWidth, Settings.RTHeight, Settings.RTFormat);
    }

    /// <summary> Dispose data resources </summary>
    public void Dispose()
    {
        if (RT)
        {
            RT.Release();
            UnityEngine.Object.Destroy(RT);
            RT = null;
        }
    }
}