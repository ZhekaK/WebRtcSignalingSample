using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System.Buffers;
using NetworkDTO.Types;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class FrameCodec : ContentCodec<FrameData>
    {
        private readonly IContentCodec<FrameCorner> _pixelCodec;
        public FrameCodec(IContentCodec<FrameCorner> pixelCodec)
        {
            _pixelCodec = pixelCodec;
        }

        public override FrameData Decode(ref SequenceReader<byte> reader)
        {
            var pix1 = _pixelCodec.Decode(ref reader);
            var pix2 = _pixelCodec.Decode(ref reader);
            var pix3 = _pixelCodec.Decode(ref reader);
            var pix4 = _pixelCodec.Decode(ref reader);
            return new(pix1, pix2, pix3, pix4);
        }

        public override int Encode(IBufferWriter<byte> writer, FrameData value)
        {
            var writtenSize = _pixelCodec.Encode(writer, value.Corner1);
            writtenSize += _pixelCodec.Encode(writer, value.Corner2);
            writtenSize += _pixelCodec.Encode(writer, value.Corner3);
            writtenSize += _pixelCodec.Encode(writer, value.Corner4);
            return writtenSize;
        }

        public override int GetSize(FrameData value)
        {
            return sizeof(int) * 2 * 4;
        }
    }
}
