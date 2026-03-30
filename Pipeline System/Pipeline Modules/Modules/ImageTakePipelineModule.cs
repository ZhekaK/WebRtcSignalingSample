using NetworkDTO.Types;
using System;
using UnityEngine;

public class ImageTakePipelineModule : IObservable<Image>
{
    private BasePipeline _pipeline;
    private readonly Observable<Image> _observableImage = new();
    private readonly Lazy<Texture2D> _texture2D = new(() => new(640, 360));


    public ImageTakePipelineModule(BasePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public IDisposable Subscribe(IObserver<Image> observer) => _observableImage.Subscribe(observer);

    /// <summary> Take the image from process render texture and send to next pipeline module</summary>
    public void OnTakeImage()
    {
        foreach (var observer in _observableImage.Observers)
            observer.OnNext(TakeImage());
    }

    private Image TakeImage()
    {
        try
        {
            RenderTexture processRenderTexture = _pipeline.GetProcessRenderTexture();
            _texture2D.Value.Reinitialize(new Vector2Int(processRenderTexture.width, processRenderTexture.height), processRenderTexture.graphicsFormat);
            processRenderTexture.CopyToTexture2D(_texture2D.Value);

            Memory<byte> buffer = _texture2D.Value.GetRawTextureData();
            var image = new Image(new NetworkDisplay(), _texture2D.Value.width, _texture2D.Value.height, buffer, _texture2D.Value.format, false);

            return image;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return new Image();
        }
    }
}