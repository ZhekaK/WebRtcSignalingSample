using System;
using System.Collections;
using UnityEngine;

[Serializable]
public abstract class BasePipeline : IDisposable
{
    public DisplayData DisplayData { get; private set; }

    /// <summary> Reference render texture, input data source </summary>
    public RenderTexture InputRenderTexture;
    /// <summary> Process render texture, used for processing </summary>
    protected RenderTexture ProcessRenderTexture;
    /// <summary> Reference render texture, output data source </summary>
    public RenderTexture OutputRenderTexture;

    protected ImageTakePipelineModule TakeModule { get; private set; }
    protected ImageDrawPipelineModule DrawModule { get; private set; }

    private Coroutine _lifecycleCoroutine;


    public BasePipeline(DisplayData displayData)
    {
        DisplayData = displayData;

        TakeModule = new(this);
        DrawModule = new(this);
    }

    /// <summary> Get pipeline process render texture </summary>
    public abstract RenderTexture GetProcessRenderTexture();

    /// <summary> Initialize and start current Pipeline </summary>
    public void Initialize()
    {
        Stop();
        Setup();
        Start();
    }

    protected void Start()
    {
        _lifecycleCoroutine = UtilityMonoBehaviourHooks.StartCoroutine(LifecycleCoroutine());
    }

    protected abstract void Setup();

    protected void Stop()
    {
        if (_lifecycleCoroutine == null) return;

        UtilityMonoBehaviourHooks.StopCoroutine(_lifecycleCoroutine);
        _lifecycleCoroutine = null;
    }

    private IEnumerator LifecycleCoroutine()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            TakeModule.OnTakeImage();
        }
    }

    /// <summary> Stop and dispose current Pipeline </summary>
    public virtual void Dispose()
    {
        Stop();

        if (ProcessRenderTexture)
        {
            ProcessRenderTexture.Release();
            UnityEngine.Object.Destroy(ProcessRenderTexture);
        }
    }
}