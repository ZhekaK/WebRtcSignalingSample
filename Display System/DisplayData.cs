using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DisplayData : IDisposable
{
    [Serializable]
    public class DataSettings
    {
        [field: SerializeField, HideInInspector] public string Info { get; private set; }
        [field: SerializeField] public TargetDisplay Display { get; private set; }
        [field: SerializeField, Range(1, 120)] public float FOV { get; private set; }
        [field: SerializeField] public Vector3 Rotation { get; private set; }


        public DataSettings(TargetDisplay display, float fov, Vector3 rotation)
        {
            Display = display;
            FOV = Mathf.Clamp(fov, 1, 120);
            Rotation = rotation;

            UpdateInfoField();
        }

        public void UpdateInfoField() => Info = $"{Display}";
    }

    [field: SerializeField] public DataSettings Settings { get; private set; } = new(TargetDisplay.Display1, 30f, Vector3.zero);

    private readonly Dictionary<RenderLayer, RenderLayerData> _renderingLayersDatas = new();
    public IReadOnlyDictionary<RenderLayer, RenderLayerData> RenderingLayersDatas => _renderingLayersDatas;


    public DisplayData(DisplaysConfig.DisplayConfig config)
    {
        Settings = new(config.DisplaySettings.Display, config.DisplaySettings.FOV, config.DisplaySettings.Rotation);

        foreach (RenderLayerData.DataSettings layerSettings in config.RenderLayersSettings)
        {
            if (_renderingLayersDatas.ContainsKey(layerSettings.RenderLayer))
            {
                Debug.LogError($"<color=red>On creating DisplayData - {Settings.Display}, RenderLayer - {layerSettings.RenderLayer} was duplicated! Check the {nameof(DisplaysConfig)} file!</color>");
                continue;
            }

            _renderingLayersDatas.Add(layerSettings.RenderLayer, new RenderLayerData(new(layerSettings.RenderLayer, layerSettings.ExistOnDisplay, layerSettings.RTWidth, layerSettings.RTHeight, layerSettings.RTFormat)));
        }
    }

    public DisplayData(DataSettings displaySettings, RenderLayerData.DataSettings layerSettings)
    {
        Settings = displaySettings;

        foreach (RenderLayer renderLayer in Enum.GetValues(typeof(RenderLayer)))
            _renderingLayersDatas.Add(renderLayer, new RenderLayerData(new(renderLayer, layerSettings.ExistOnDisplay, layerSettings.RTWidth, layerSettings.RTHeight, layerSettings.RTFormat)));
    }

    /// <summary> Dispose data resources </summary>
    public void Dispose()
    {
        foreach (RenderLayerData renderLayerData in _renderingLayersDatas.Values)
            renderLayerData.Dispose();
    }
}