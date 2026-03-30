using NetworkDTO.Types;
using System;
using UnityEngine;

public class ImageDrawPipelineModule : IObserver<Image>
{
    private BasePipeline _pipeline;
    private readonly Lazy<Texture2D> _texture2D = new(() => new(640, 360));


    public ImageDrawPipelineModule(BasePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary> Draws an EVS image to the display </summary>
    private void Draw(Image image)
    {
        _texture2D.Value.Reinitialize(new Vector2Int(image.width, image.height), image.format);

        unsafe
        {
            fixed (byte* p = image.ImageBytes.Span)
            {
                IntPtr ptr = (IntPtr)p;
                _texture2D.Value.LoadRawTextureData(ptr, image.ImageBytes.Length);
                _texture2D.Value.Apply();
            }
        }

        Graphics.Blit(_texture2D.Value, _pipeline.OutputRenderTexture);
    }

    public void OnCompleted()
    {
        throw new NotImplementedException();
    }

    public void OnError(Exception error)
    {
        Debug.LogError(error);
    }

    public void OnNext(Image value)
    {
        UtilityMainThreadDispatcher.Enqueue(() => Draw(value));
    }
}
