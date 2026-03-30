using EditorAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "Displays Config", menuName = "Scriptable Objects/Multilayer/Create Displays Config SO")]
public class DisplaysConfig : ScriptableObject
{
    [Serializable]
    public class DisplayConfig
    {
        [field: SerializeField, HideInInspector] public string Info { get; private set; }
        [field: SerializeField] public DisplayData.DataSettings DisplaySettings { get; private set; }
        [field: SerializeField] public List<RenderLayerData.DataSettings> RenderLayersSettings { get; private set; } = new();


        public DisplayConfig(DisplayData.DataSettings displaySettings, bool existOnDisplay = true, int width = 1920, int height = 1080, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            DisplaySettings = displaySettings;

            foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
                RenderLayersSettings.Add(new RenderLayerData.DataSettings(layer, existOnDisplay, width, height, format));
        }

        /// <summary> Sync the list (RenderLayerData.BaseParams) with enum (RenderLayer) values </summary>
        public void SyncRenderLayers()
        {
            var allLayers = Enum.GetValues(typeof(RenderLayer)).Cast<RenderLayer>().ToArray();

            // 1. Remove not valid enum (RenderLayers) values
            RenderLayersSettings.RemoveAll(parameters => !allLayers.Contains(parameters.RenderLayer));

            // 2. Remove duplicates
            HashSet<RenderLayer> uniqueLayers = new();
            for (int i = 0; i < RenderLayersSettings.Count; i++)
            {
                RenderLayer layer = RenderLayersSettings[i].RenderLayer;
                if (!uniqueLayers.Contains(layer))
                {
                    uniqueLayers.Add(layer);
                }
                else
                {
                    RenderLayersSettings.RemoveAt(i);
                    i--;
                }
            }

            // 3. Add not found enum (RenderLayers) values
            foreach (var layer in allLayers)
            {
                if (!RenderLayersSettings.Any(p => p.RenderLayer == layer))
                {
                    RenderLayersSettings.Add(new RenderLayerData.DataSettings(layer));
                }
            }

            // 4. Sort parameters for enum (RenderLayer)
            RenderLayersSettings.Sort((a, b) => a.RenderLayer.CompareTo(b.RenderLayer));
        }

        /// <summary> Update Info field </summary>
        public void UpdateInfoField()
        {
            DisplaySettings.UpdateInfoField();
            Info = DisplaySettings.Info;
            foreach (var renderLayerSettings in RenderLayersSettings)
                renderLayerSettings.UpdateInfoField();
        }
    }

    [field: SerializeField] public bool EmulateWindshields { get; private set; }
    [field: SerializeField] public List<DisplayConfig> Configs { get; private set; } = new();


#if UNITY_EDITOR

    private void OnValidate()
    {
        foreach (var config in Configs)
        {
            config.UpdateInfoField();
            config.SyncRenderLayers();
        }
    }

    [Header("- - Debug:")]
    [SerializeField, Range(1, 8)] private int _defaultDisplaysCount;

    [Button("Delete Current Configs and Create Defaults")]
    public void CreateDisplaysConfigsEditor()
    {
        Configs.Clear();
        for (int i = 0; i < _defaultDisplaysCount; i++)
        {
            Configs.Add(new DisplayConfig(new((TargetDisplay)i, 30, Vector3.zero)));
        }
    }

#endif
}