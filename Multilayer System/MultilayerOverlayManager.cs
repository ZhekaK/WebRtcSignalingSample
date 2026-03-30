using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class MultilayerOverlayManager : MonoBehaviour
{
    public static MultilayerOverlayManager Instance { get; private set; }

    public bool UseMultilayerView { get; private set; }
    public RenderLayer OverlayRenderLayer { get; private set; }

    private readonly List<Camera> _overlayCameras = new();
    private readonly List<CustomPassVolume> _overlayPassVolumes = new();
    private readonly List<MultilayerOverlayPass> _overlayPasses = new();

    private const string USE_MULTILAYER_VIEW_PROPERTY = "_Use_Multilayer_View";
    private const string OVERLAY_RENDER_LAYER_PROPERTY = "_Overlay_Render_Layer";


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject manager = new($"{nameof(MultilayerOverlayManager)}");
        Instance = manager.AddComponent<MultilayerOverlayManager>();
        DontDestroyOnLoad(manager);

        Instance.InitializeOverlays();
    }

    private void InitializeOverlays()
    {
        foreach (DisplayData displayData in DisplaysManager.Instance.DisplaysDatas.Values)
            InitializeOverlay(displayData);

        SetOverlayMode(UseMultilayerView, OverlayRenderLayer);
    }

    private void InitializeOverlay(DisplayData displayData)
    {
        // Overlay Camera Prefab
        GameObject overlayCameraPrefab = Resources.Load<GameObject>("Multilayer System/Overlay Camera");
        if (!overlayCameraPrefab)
        {
            Debug.LogError($"<color=red>Overlay Camera Prefab is not found!</color>");
            return;
        }

        // Overlay Camera Object
        GameObject overlayCameraObject = Instantiate(overlayCameraPrefab, transform);
        if (overlayCameraObject.TryGetComponent<Camera>(out Camera overlayCamera))
        {
            overlayCamera.name = $"Overlay Camera - {displayData.Settings.Display}";
            overlayCamera.targetDisplay = (int)displayData.Settings.Display;
            overlayCamera.depth = 0;

            _overlayCameras.Add(overlayCamera);
        }
        else
        {
            Debug.LogError($"<color=red>Camera on object - {overlayCameraObject.name} is not found!</color>");
        }

        // Overlay Pass Volume
        CustomPassVolume passVolume = overlayCameraObject.GetComponentInChildren<CustomPassVolume>();
        if (passVolume)
        {
            MultilayerOverlayPass overlayPass = passVolume.AddPassOfType(typeof(MultilayerOverlayPass)) as MultilayerOverlayPass;
            overlayPass.targetColorBuffer = CustomPass.TargetBuffer.None;
            overlayPass.targetDepthBuffer = CustomPass.TargetBuffer.None;
            overlayPass.name = $"Overlay Pass - {displayData.Settings.Display}";

            _overlayPassVolumes.Add(passVolume);
            _overlayPasses.Add(overlayPass);
        }
        else
        {
            Debug.LogError($"<color=red>{nameof(CustomPassVolume)} on object - {overlayCameraObject.name} is not found!</color>");
        }
    }

    /// <summary> Set overlay layer visibility mode </summary>
    /// <param name="overlayMode"> [-1]: All Layers | [0]: Visible | [1]: LWIR | [2]: Labels | [3]: SWIR | [4]: Evs </param>
    public void SetOverlayMode(bool useMultilayerView, RenderLayer overlayRenderLayer)
    {
        UseMultilayerView = useMultilayerView;
        OverlayRenderLayer = overlayRenderLayer;

        Shader.SetGlobalInteger(USE_MULTILAYER_VIEW_PROPERTY, useMultilayerView ? 1 : 0);
        Shader.SetGlobalFloat(OVERLAY_RENDER_LAYER_PROPERTY, (int)overlayRenderLayer);
    }
}