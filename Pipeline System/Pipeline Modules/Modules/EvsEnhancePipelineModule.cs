using NetworkDTO.Types;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class EvsEnhancePipelineModule : AsyncObserver<Image>, IObservable<Image>
{
    BasePipeline _pipeline;

    [DllImport("EVS_dll.dll")] public static extern int EnhVision(int iChanNum, IntPtr pFrame_in, int iWidth_in, int iHeight_in, IntPtr pFrame_out, int iWidth_out, int iHeight_out);
    [DllImport("EVS_dll.dll")] public static extern void EnhVision_Reset(int iChanNum);

    private readonly Observable<Image> _observable = new();

    private readonly object _libraryLock = new();


    public EvsEnhancePipelineModule(BasePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary> Subscribe to a data source </summary>
    public IDisposable Subscribe(IObserver<Image> observer) => _observable.Subscribe(observer);

    /// <summary> Called when a new data object is received from the source. </summary>
    public override Task OnNextAsync(Image value)
    {
        if (value.format != TextureFormat.R8 && value.format != TextureFormat.R8_SIGNED)
        {
            foreach (var observer in _observable.Observers)
                observer.OnError(new NotImplementedException());
        }

        //EnhVision_Reset(2);
        try
        {
            lock (_libraryLock)
            {
                unsafe
                {
                    fixed (byte* p = value.ImageBytes.Span)
                    {
                        IntPtr ptr = (IntPtr)p;
                        EnhVision(Mathf.Clamp((int)_pipeline.DisplayData.Settings.Display, 0, 2), ptr, value.width, value.height, ptr, value.width, value.height);
                    }
                }
            }

            foreach (var observer in _observable.Observers)
                observer.OnNext(value);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }

        return Task.CompletedTask;
    }

    public override Task OnCompletedAsync()
    {
        throw new NotImplementedException();
    }

    public override Task OnErrorAsync(Exception exception)
    {
        Debug.LogException(exception);
        throw new NotImplementedException();
    }
}