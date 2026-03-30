using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using FlexNet.Server;
using System.Buffers;
using NetworkDTO.Types;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class NeuroResponseHeaderCodec : ContentCodec<NeuroResponseHeader>
    {

        private readonly IContentCodec<int> _intHandler;

        public NeuroResponseHeaderCodec(IContentCodec<int> intContentHandler)
        {
            _intHandler = intContentHandler;

        }

        public override NeuroResponseHeader Decode(ref SequenceReader<byte> reader)
        {
            return new() { ResponseCode = (ResponseCode)_intHandler.Decode(ref reader) };
        }

        public override int Encode(IBufferWriter<byte> writer, NeuroResponseHeader value)
        {
            return _intHandler.Encode(writer, (int)value.ResponseCode);
        }

        public override int GetSize(NeuroResponseHeader value)
        {
            return sizeof(int);
        }
    }
}
