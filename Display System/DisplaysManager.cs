using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static DisplaysConfig;

public class DisplaysManager : MonoBehaviour
{
    public static DisplaysManager Instance { get; private set; }

    public event Action<TargetDisplay, RenderLayer, int, int, GraphicsFormat> OnChangeRenderLayerSettings;

    [field: SerializeField] public DisplaysConfig DisplaysConfig { get; private set; }
    
    private Dictionary<TargetDisplay, DisplayData> _displaysDatas = new();
    public IReadOnlyDictionary<TargetDisplay, DisplayData> DisplaysDatas => _displaysDatas;


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager(DisplaysConfig displaysConfig)
    {
        if (Instance) return;

        GameObject manager = new(nameof(DisplaysManager));
        Instance = manager.AddComponent<DisplaysManager>();
        DontDestroyOnLoad(manager);

        if (displaysConfig)
        {
            Instance.DisplaysConfig = displaysConfig;
            Instance.InitializeDisplaysDatas();
        }
        else
        {
            Debug.LogError($"<color=red>{nameof(DisplaysConfig)} in (Resources) is not found!</color>");
        }
    }

    private void InitializeDisplaysDatas()
    {
        for(int i = 0; i < DisplaysConfig.Configs.Count; i++)
            _displaysDatas.Add(DisplaysConfig.Configs[i].DisplaySettings.Display, new DisplayData(DisplaysConfig.Configs[i]));

        if (_displaysDatas.Count > 1)
            for (int i = 1; i < Display.displays.Length; i++)
                Display.displays[i].Activate();
    }

    /// <summary> Change target display render layer resolution </summary>
    public void SetRenderLayerParameters(TargetDisplay display, RenderLayer layer, int width, int height, GraphicsFormat format)
    {
        if (DisplaysDatas.TryGetValue(display, out DisplayData displayData))
        {
            if (displayData.RenderingLayersDatas.TryGetValue(layer, out RenderLayerData renderingLayerData))
            {
                renderingLayerData.SetDataSettings(width, height, format);
                OnChangeRenderLayerSettings?.Invoke(display, layer, width, height, format);
            }
        }
    }

    /// <summary> Change target display all render layers resolutions </summary>
    public void SetDisplayParameters(TargetDisplay display, int width, int height, GraphicsFormat format)
    {
        foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
            SetRenderLayerParameters(display, layer, width, height, format);
    }

    /// <summary> Change all displays all render layers resolutions </summary>
    public void SetAllDisplaysParameters(int width, int height, GraphicsFormat format)
    {
        foreach (TargetDisplay display in _displaysDatas.Keys)
            SetDisplayParameters(display, width, height, format);
    }

    private void OnDestroy()
    {
        foreach (DisplayData displayData in DisplaysDatas.Values)
            displayData.Dispose();
    }
}