using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultilayerSceneManager : MonoBehaviour
{
    public static MultilayerSceneManager Instance { get; private set; }

    public event Action<RenderLayer> OnAddRenderLayer;

    private Dictionary<RenderLayer, MultilayerSetupStrategy> _setupStrategies = new();
    private Dictionary<RenderLayer, Scene> _isLoadedLayers = new();
    public IReadOnlyDictionary<RenderLayer, Scene> LoadedLayers => _isLoadedLayers;

    private HashSet<RenderLayer> _onLoadingLayers = new();
    private HashSet<RenderLayer> _onUnloadingLayers = new();


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager(List<MultilayerSetupStrategy> setupStrategies)
    {
        if (Instance) return;

        GameObject manager = new(nameof(MultilayerSceneManager));
        Instance = manager.AddComponent<MultilayerSceneManager>();
        DontDestroyOnLoad(manager);

        Instance.LoadStrategies(setupStrategies);
    }

    private void LoadStrategies(List<MultilayerSetupStrategy> setupStrategies)
    {
        foreach (MultilayerSetupStrategy strategy in setupStrategies)
        {
            RenderLayer renderLayer = strategy.RenderLayer;
            if (_setupStrategies.ContainsKey(renderLayer))
                Debug.LogError($"<color=red>Strategy for setup the RenderLayer - {renderLayer} is duplicated! Check folder (Resources/Multilayer System/Setup Strategies)!</color>");
            else
                _setupStrategies.Add(renderLayer, strategy);
        }

        SceneData.OnSceneDataLoaded += SceneDataLoaded;
        SceneData.OnSceneDataUnloaded += SceneDataUnloaded;
    }

    private void SceneDataLoaded(Scene scene, SceneData sceneData)
    {
        RenderLayer renderLayer = _onLoadingLayers.Count == 0 ? sceneData.RenderLayer : _onLoadingLayers.First();

        _onLoadingLayers.Remove(renderLayer);
        if (_isLoadedLayers.ContainsKey(renderLayer))
        {
            Debug.LogError($"<color=red>Scene with target RenderLayer - {renderLayer} is duplicated!</color>");
            return;
        }

        _isLoadedLayers.Add(renderLayer, scene);
        if (_setupStrategies.TryGetValue(renderLayer, out MultilayerSetupStrategy strategy))
        {
            sceneData.SetupScene(renderLayer, strategy);
            OnAddRenderLayer?.Invoke(renderLayer);
        }
        else
        {
            Debug.LogError($"<color=red>Strategy for setup the RenderLayer - {renderLayer} is not found! Check folder (Resources/Multilayer System/Setup Strategies)!</color>");
        }
    }

    private void SceneDataUnloaded(Scene scene, SceneData sceneData)
    {
        _onUnloadingLayers.Remove(sceneData.RenderLayer);
        _isLoadedLayers.Remove(sceneData.RenderLayer);
    }

    /// <summary> Load scene for target RenderLayer </summary>
    public void LoadLayer(RenderLayer renderLayer)
    {
        if (!IsLayerCanBeLoad(renderLayer)) return;

        AsyncOperation asyncLoad = AirportsManager.Instance.LoadSceneAsync(AirportsManager.Instance.CurrentSceneName, LoadSceneMode.Additive);
        if (asyncLoad != null)
            _onLoadingLayers.Add(renderLayer);
    }

    /// <summary> Unload scene with target RenderLayer </summary>
    public void UnloadLayer(RenderLayer renderLayer)
    {
        if (!IsLayerCanBeUnload(renderLayer)) return;

        if (_isLoadedLayers.TryGetValue(renderLayer, out Scene scene))
        {
            AsyncOperation asyncUnload = AirportsManager.Instance.UnloadSceneAsync(scene);
            if (asyncUnload != null)
                _onUnloadingLayers.Add(renderLayer);
        }
    }

    private void OnDestroy()
    {
        SceneData.OnSceneDataLoaded -= SceneDataLoaded;
        SceneData.OnSceneDataUnloaded -= SceneDataUnloaded;
    }

    #region OTHER METHODS

    private bool IsLayerCanBeLoad(RenderLayer renderLayer)
    {
        bool isLoadableScene = AirportsManager.Instance.CurrentSceneName != AirportsManager.Instance.AirportsData.MenuSceneName;
        bool isLayerNotLoadet = !_isLoadedLayers.ContainsKey(renderLayer);
        bool isLayerNotLoading = !_onLoadingLayers.Contains(renderLayer);
        bool isLoadableLayer = renderLayer == RenderLayer.Visible || renderLayer == RenderLayer.LWIR || renderLayer == RenderLayer.Labels || renderLayer == RenderLayer.SWIR;

        return isLoadableScene && isLayerNotLoadet && isLayerNotLoading && isLoadableLayer;
    }

    private bool IsLayerCanBeUnload(RenderLayer renderLayer)
    {
        bool isSceneNotLast = SceneManager.loadedSceneCount > 1;
        bool isRenderLayerLoad = _isLoadedLayers.TryGetValue(renderLayer, out Scene scene);
        bool isRenderLayerSceneValid = scene != null;
        bool isRenderLayerNotUnloading = !_onUnloadingLayers.Contains(renderLayer);

        return isSceneNotLast && isRenderLayerLoad && isRenderLayerSceneValid && isRenderLayerNotUnloading;
    }

    /// <summary> Get visibility layer from GameObject RenderingLayerMask </summary>
    public static RenderLayer GetObjectLayer(GameObject gameObject)
    {
        if (!gameObject)
        {
            Debug.LogError("<color=red>GameObject is null or missed!</color>");
            return RenderLayer.Visible;
        }

        if (!Enum.TryParse(LayerMask.LayerToName(gameObject.layer), true, out RenderLayer renderLayer))
        {
            Debug.LogError($"<color=red>GameObject {gameObject.name} layer cant cast to visibility layers!</color>", gameObject);
            return RenderLayer.Visible;
        }

        return renderLayer;
    }

    #endregion

#if UNITY_EDITOR

    [Header("- - Debug:")]
    [SerializeField] private RenderLayer _debugLayer;

    [InspectorButton("Load Layer")]
    private void LoadLayerEditor() => LoadLayer(_debugLayer);

    [InspectorButton("Unload Layer")]
    private void UnloadLayerEditor() => UnloadLayer(_debugLayer);

#endif
}