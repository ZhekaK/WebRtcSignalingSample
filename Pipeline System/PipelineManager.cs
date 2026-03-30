using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PipelineManager : MonoBehaviour
{
    public static PipelineManager Instance { get; private set; }

    [Header("- - Pipelines:")]
    [SerializeField] private List<BasePipeline> Pipelines = new();

    /// <summary> Get all created EVS Pipelines </summary>
    public List<BasePipeline> EvsPipelines => Pipelines.Where(pipeline => pipeline is EvsPipeline).ToList();


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject manager = new(nameof(PipelineManager));
        Instance = manager.AddComponent<PipelineManager>();
        DontDestroyOnLoad(manager);

        Instance.InitializePipelines();

        Instance.Subscribe();
    }

    private void Subscribe()
    {
        if (LayerDataReceiverModeController.Instance == null) return;
        LayerDataReceiverModeController.Instance.OnEvsStateChanged += LayerDataReceiverModeController_OnStateEvsChanged;
    }
    private void Unsubscribe()
    {
        if (LayerDataReceiverModeController.Instance == null) return;
        LayerDataReceiverModeController.Instance.OnEvsStateChanged -= LayerDataReceiverModeController_OnStateEvsChanged;
    }

    private void LayerDataReceiverModeController_OnStateEvsChanged(object sender, EvsStateChangedEventArgs e)
    {
        SetPipelinesState(EvsPipelines, e.CurrentStateEvs);
    }

    private void InitializePipelines()
    {
        foreach (DisplayData displayData in DisplaysManager.Instance.DisplaysDatas.Values)
            if (displayData.RenderingLayersDatas.TryGetValue(RenderLayer.EVS, out RenderLayerData renderLayerData))
                if (renderLayerData.Settings.ExistOnDisplay)
                    Pipelines.Add(new EvsPipeline(displayData));
    }

    /// <summary> Set Pipelines Enabled State </summary>
    public void SetPipelinesState(List<BasePipeline> pipelines, bool enabledState)
    {
        foreach (BasePipeline pipeline in pipelines)
        {
            if (enabledState)
                pipeline.Initialize();
            else
                pipeline.Dispose();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();

        foreach (BasePipeline pipeline in Pipelines)
            pipeline.Dispose();
    }

#if UNITY_EDITOR

    [Header("- - Debug:")]
    [SerializeField] private bool _enabledState;

    [InspectorButton]
    private void SetPipelineStateEditor() => SetPipelinesState(EvsPipelines, _enabledState);

#endif
}