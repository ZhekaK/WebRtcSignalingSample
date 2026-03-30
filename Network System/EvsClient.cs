using FlexNet;
using NetworkDTO.Types;
using SgsDemonstrator;
using System;
using System.Threading.Tasks;

public class EvsClient : ReconnectableClient<Image>
{
    private readonly EvsImageJpegCodec _jpegCodec = new(ContentCodecDIProvider.Default.GetContentCodec<int>(),
                                                        ContentCodecDIProvider.Default.GetContentCodec<Memory<byte>>(),
                                                        ContentCodecDIProvider.Default.GetContentCodec<bool>(),
                                                        ContentCodecDIProvider.Default.GetContentCodec<NetworkDisplay>());

    protected override async Task SendAsync(Image message)
    {
        await Client.AddContent(message, _jpegCodec)
                    .SendAsync();
    }
}