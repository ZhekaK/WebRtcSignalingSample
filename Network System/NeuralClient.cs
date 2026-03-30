using FlexNet.Interfaces;
using FlexNet.Server;
using NetworkDTO.Codecs;
using NetworkDTO.Types;
using SgsDemonstrator;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NeuralClient : ReconnectableClient<Image>
{
    private readonly JpegForNeuralnetCodec _codec = new();
    private readonly NetworkManager _networkManager;
    public NeuralClient(NetworkManager networkManager)
    {
        /* 
         * TODO: A class should not be a link between two managers. 
         * It should be given information, not look for it. 
         * And it should not trigger events of other classes. 
         */
        _networkManager = networkManager;
    }

    private async Task<IReceiveResult> RequestAsync(byte[] jpeg, CancellationToken token)
    {
        await Client.AddContent(jpeg, _codec)
                    .SendAsync(token);
        return await Client.ReceiveAsync(token);
    }

    protected override async Task SendAsync(Image message)
    {
#warning вынести !FrameManager.Instance.UseNeuroFrames на ступень выше, из места где вызывается этот метод
        if (Client == null || !IsConnected /*|| !FrameManager.Instance.UseNeuroFrames*/)
        {
            UtilityMainThreadDispatcher.Enqueue(() => _networkManager.OnEmptyFrames?.Invoke());
            return;
        }

        var token = _cancellationTokenSource.Token;

        var jpeg = UtilityConvertationImages.ConvertToJpeg(message);

        using var receiveResult = await RequestAsync(jpeg, token);

        receiveResult.GetContent(out NeuroResponseHeader code);

        switch (code.ResponseCode)
        {
            case ResponseCode.Ok:
                receiveResult.GetContentEnumerable<RecognitionObject>(out var contents);
                UtilityMainThreadDispatcher.Enqueue(() => _networkManager.OnFramesRecieved?.Invoke(contents.ToList()));
                break;

            default:
                UtilityMainThreadDispatcher.Enqueue(() => _networkManager.OnEmptyFrames?.Invoke());
                break;
        }
    }
}