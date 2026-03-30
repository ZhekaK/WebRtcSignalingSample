using FlexNet;
using NetworkDTO.Types;
using SgsDemonstrator;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class FrameClient : ReconnectableClient<(Image, List<RecognitionObject>, int, int)>
{
    private readonly ImagePngCodec _pngCodec = new(ContentCodecDIProvider.Default.GetContentCodec<int>(),
                                                   ContentCodecDIProvider.Default.GetContentCodec<Memory<byte>>(),
                                                   ContentCodecDIProvider.Default.GetContentCodec<bool>(),
                                                   ContentCodecDIProvider.Default.GetContentCodec<NetworkDisplay>());

    protected override async Task SendAsync((Image, List<RecognitionObject>, int, int) message)
    {
        await Client.AddContent(message.Item1, _pngCodec)
                    .AddContentEnumerable(message.Item2)
                    .AddContent(message.Item3)
                    .AddContent(message.Item4)
                    .SendAsync();
    }
}
