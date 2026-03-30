using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System.Buffers;
using NetworkDTO.Types;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class PixelCodec : ContentCodec<FrameCorner>
    {
        private readonly IContentCodec<int> _intCodec;
        public PixelCodec(IContentCodec<int> intCodec)
        {
            _intCodec = intCodec;
        }

        public override FrameCorner Decode(ref SequenceReader<byte> reader)
        {
            var x = _intCodec.Decode(ref reader);
            var y = _intCodec.Decode(ref reader);
            return new FrameCorner(x, y);
        }

        public override int Encode(IBufferWriter<byte> writer, FrameCorner value)
        {
            var writtenSize = _intCodec.Encode(writer, value.X);
            writtenSize += _intCodec.Encode(writer, value.Y);
            return writtenSize;
        }

        public override int GetSize(FrameCorner value)
        {
            return sizeof(int) * 2;
        }
    }
}
