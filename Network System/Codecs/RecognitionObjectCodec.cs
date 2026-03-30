using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System.Buffers;
using NetworkDTO.Types;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class RecognitionObjectCodec : ContentCodec<RecognitionObject>
    {
        private readonly IContentCodec<int> _intCodec;
        private readonly IContentCodec<float> _floatCodec;
        private readonly IContentCodec<FrameData> _frameCodec;
        public RecognitionObjectCodec(IContentCodec<int> intCodec, IContentCodec<float> floatCodec, IContentCodec<FrameData> frameCodec)
        {
            _intCodec = intCodec;
            _floatCodec = floatCodec;
            _frameCodec = frameCodec;
        }

        public override RecognitionObject Decode(ref SequenceReader<byte> reader)
        {
            var objClass = (RecognitionClass)_intCodec.Decode(ref reader);
            var probability = _floatCodec.Decode(ref reader);
            var frame = _frameCodec.Decode(ref reader);

            return new(objClass, probability, frame);
        }

        public override int Encode(IBufferWriter<byte> writer, RecognitionObject value)
        {
            var writtenBytes = _intCodec.Encode(writer, (int)value.RecognitionClass);
            writtenBytes += _floatCodec.Encode(writer, value.Probability);
            writtenBytes += _frameCodec.Encode(writer, value.FrameData);

            return writtenBytes;
        }

        public override int GetSize(RecognitionObject value)
        {
            var size = _intCodec.GetSize((int)value.RecognitionClass);
            size += _floatCodec.GetSize(value.Probability);
            size += _frameCodec.GetSize(value.FrameData);

            return size;
        }
    }
}
