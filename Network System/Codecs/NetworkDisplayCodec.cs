using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System.Buffers;
using NetworkDTO.Types;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class NetworkDisplayCodec : ContentCodec<NetworkDisplay>
    {
        private readonly IContentCodec<int> _intCodec;

        public NetworkDisplayCodec(IContentCodec<int> intCodec)
        {
            _intCodec = intCodec;
        }
        public override NetworkDisplay Decode(ref SequenceReader<byte> reader)
        {
            var displayId = _intCodec.Decode(ref reader);
            var basePort = (NetworkDisplay.PortType)_intCodec.Decode(ref reader);

            return new NetworkDisplay(displayId, basePort);
        }

        public override int Encode(IBufferWriter<byte> writer, NetworkDisplay value)
        {
            var writtenSize = _intCodec.Encode(writer, value.DisplayId);
            writtenSize += _intCodec.Encode(writer, (int)value.BasePort);

            return writtenSize;
        }

        public override int GetSize(NetworkDisplay value)
        {
            var size = _intCodec.GetSize(value.DisplayId);
            size += _intCodec.GetSize((int)value.BasePort);

            return size;
        }
    }
}
