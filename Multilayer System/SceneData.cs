using Multilayer.Setup.Module;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneData : MonoBehaviour
{
    public static event Action<Scene, SceneData> OnSceneDataLoaded;
    public static event Action<Scene, SceneData> OnSceneDataUnloaded;

    [field: SerializeField, DisableEdit] public RenderLayer RenderLayer { get; private set; } = RenderLayer.Visible;

    [Header("- - Setup Modules:")]
    [SerializeField] private List<BaseSetupModule> _setupModules = new();


    private void Start()
    {
        OnSceneDataLoaded?.Invoke(gameObject.scene, this);
    }

    /// <summary> Setup scene in target layer </summary>
    public void SetupScene(RenderLayer layer, MultilayerSetupStrategy strat)
    {
        RenderLayer = layer;
        UnityEngine.Debug.Log($"<color=cyan>Setup {strat.RenderLayer} Scene:</color>");
        Stopwatch watcher = Stopwatch.StartNew();

        // Use setup modules:
        if (TryGetModule(out CamerasSetupModule camerasModule))
        {
            watcher.Restart();
            camerasModule.SetupRenderCameras(RenderLayer, strat.ClearColorMode, strat.BackgroundColor, strat.PassInjectionPoint, strat.OverrideShader, strat.OverrideShaderPassType, strat.RenderQueueType);
            watcher.Stop();
            UnityEngine.Debug.Log($"{nameof(CamerasSetupModule)} completed the setup in: <color=cyan>{watcher.ElapsedMilliseconds}ms and {watcher.ElapsedTicks}ticks</color>");
        }

        if (TryGetModule(out LightingSetupModule lightModule))
        {
            watcher.Restart();
            lightModule.SetupLighting(RenderLayer, strat.IsLightingEnabled, strat.LightingShadows, strat.LightingCullingLayersNames);
            watcher.Stop();
            UnityEngine.Debug.Log($"{nameof(LightingSetupModule)} completed the setup in: <color=cyan>{watcher.ElapsedMilliseconds}ms and {watcher.ElapsedTicks}ticks</color>");
        }

        if (TryGetModule(out VolumesSetupModule volumeModule))
        {
            watcher.Restart();
            volumeModule.SetupVolumes(RenderLayer, strat.UseGeneralVolume, strat.UseSkyVolume, strat.UseCloudsVolume, strat.UseFogVolume, strat.UsePostProcessVolume, strat.UseCustomVolume,
                strat.GeneralVolume, strat.SkyVolume, strat.CloudsVolume, strat.FogVolume, strat.PostProcessVolume, strat.CustomVolume);
            watcher.Stop();
            UnityEngine.Debug.Log($"{nameof(VolumesSetupModule)} completed the setup in: <color=cyan>{watcher.ElapsedMilliseconds}ms and {watcher.ElapsedTicks}ticks</color>");
        }

        if (TryGetModule(out GeometrySetupModule geometryModule))
        {
            watcher.Restart();
            geometryModule.SetupGeometry(RenderLayer, strat.IsGeometryEnabled, strat.GeometryShadowCastingMode, strat.GeometryReceiveShadows);
            watcher.Stop();
            UnityEngine.Debug.Log($"{nameof(GeometrySetupModule)} completed the setup in: <color=cyan>{watcher.ElapsedMilliseconds}ms and {watcher.ElapsedTicks}ticks</color>");
        }
    }

    private bool TryGetModule<T>(out T module) where T : BaseSetupModule
    {
        module = _setupModules.OfType<T>().FirstOrDefault();
        return module != null;
    }

    /// <summary> Dispose scene data resources </summary>
    public void DisposeScene()
    {
        // Dispose setup modules:
        foreach (BaseSetupModule module in _setupModules)
            module.Dispose();
    }

    private void OnDestroy()
    {
        DisposeScene();
        OnSceneDataUnloaded?.Invoke(gameObject.scene, this);
    }
}