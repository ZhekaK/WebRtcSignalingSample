using EditorAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[CreateAssetMenu(fileName = "New Multilayer Setup Strategy", menuName = "Scriptable Objects/Multilayer/Create Multilayer Setup Strategy SO")]
public class MultilayerSetupStrategy : ScriptableObject
{
    [field: Title("Identifier:")]
    [field: SerializeField] public RenderLayer RenderLayer { get; private set; }

    [field: Space(20)]

    [field: Title("Camera Settings:")]
    [field: Tooltip("Render cameras skybox clear color mode")]
    [field: SerializeField] public HDAdditionalCameraData.ClearColorMode ClearColorMode { get; private set; } = HDAdditionalCameraData.ClearColorMode.None;

    [field: Tooltip("Render cameras skybox background color when clear mode is color")]
    [field: SerializeField] public Color BackgroundColor { get; private set; } = Color.black;

    [field: Tooltip("Render cameras draw renderers custom pass injection point")]
    [field: SerializeField] public CustomPassInjectionPoint PassInjectionPoint { get; private set; } = CustomPassInjectionPoint.BeforePostProcess;

    [field: Tooltip("Render cameras draw renderers custom pass override shader")]
    [field: SerializeField] public Shader OverrideShader { get; private set; }

    [field: Tooltip("Render cameras draw renderers custom pass override shader pass type (Lit - Forward, Unlit - ForwardOnly)")]
    [field: SerializeField] public string OverrideShaderPassType { get; private set; } = "Forward";

    [field: Tooltip("Render cameras draw renderers custom pass render queue type")]
    [field: SerializeField] public DrawRenderersCustomPass.RenderQueueType RenderQueueType { get; private set; } = DrawRenderersCustomPass.RenderQueueType.All;


    [field: Space(20)]

    [field: Title("Lighting Settings:")]
    [field: Tooltip("Enables or disables lighting (sun/moon) for this layer")]
    [field: SerializeField] public bool IsLightingEnabled { get; private set; } = false;

    [field: Tooltip("Shadow type for the main directional light")]
    [field: SerializeField] public LightShadows LightingShadows { get; private set; } = LightShadows.None;

    [field: Tooltip("Layers that the light should illuminate. Objects on these layers will receive light")]
    [field: SerializeField] public List<RenderLayer> LightingCullingLayers { get; private set; } = new();
    public string[] LightingCullingLayersNames => LightingCullingLayers.Select(layer => layer.ToString()).ToArray();

    [field: Space(20)]

    [field: Title("Volume Settings")]
    [field: Tooltip("Enable the general volume for this layer")]
    [field: SerializeField] public bool UseGeneralVolume { get; private set; } = false;

    [field: Tooltip("General volume profile")]
    [field: SerializeField] public VolumeProfile GeneralVolume { get; private set; }

    [field: Tooltip("Enable the sky volume for this layer")]
    [field: SerializeField] public bool UseSkyVolume { get; private set; } = false;

    [field: Tooltip("Sky volume profile")]
    [field: SerializeField] public VolumeProfile SkyVolume { get; private set; }

    [field: Tooltip("Enable the clouds volume for this layer")]
    [field: SerializeField] public bool UseCloudsVolume { get; private set; } = false;

    [field: Tooltip("Clouds volume profile")]
    [field: SerializeField] public VolumeProfile CloudsVolume { get; private set; }

    [field: Tooltip("Enable the fog volume for this layer")]
    [field: SerializeField] public bool UseFogVolume { get; private set; } = false;

    [field: Tooltip("Fog volume profile")]
    [field: SerializeField] public VolumeProfile FogVolume { get; private set; }

    [field: Tooltip("Enable the post‑processing volume for this layer")]
    [field: SerializeField] public bool UsePostProcessVolume { get; private set; } = false;

    [field: Tooltip("Post Process volume profile")]
    [field: SerializeField] public VolumeProfile PostProcessVolume { get; private set; }

    [field: Tooltip("Enable the custom volume for this layer")]
    [field: SerializeField] public bool UseCustomVolume { get; private set; } = false;

    [field: Tooltip("Custom volume profile")]
    [field: SerializeField] public VolumeProfile CustomVolume { get; private set; }

    [field: Space(20)]

    [field: Title("Geometry Settings:")]
    [field: Tooltip("If enabled, geometry objects will be visible for this layer")]
    [field: SerializeField] public bool IsGeometryEnabled { get; private set; } = false;

    [field: Tooltip("Determines how geometry casts shadows")]
    [field: SerializeField] public ShadowCastingMode GeometryShadowCastingMode { get; private set; } = ShadowCastingMode.Off;

    [field: Tooltip("If enabled, geometry can receive shadows from other objects")]
    [field: SerializeField] public bool GeometryReceiveShadows { get; private set; } = false;
}