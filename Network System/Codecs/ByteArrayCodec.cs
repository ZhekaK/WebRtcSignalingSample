using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System;
using System.Buffers;

namespace NetworkDTO.Codecs
{
    [ContentCodec]
    public class ByteArrayCodec : ContentCodec<byte[]>
    {
        private readonly IContentCodec<int> _intCodec;

        public ByteArrayCodec(IContentCodec<int> intCodec)
        {
            _intCodec = intCodec;
        }

        public override int GetSize(byte[] value)
        {
            return value.Length + 4;
        }

        public override int Encode(IBufferWriter<byte> writer, byte[] value)
        {
            var writtenSize = _intCodec.Encode(writer, value.Length);
            writer.Write(value);
            return writtenSize + value.Length;
        }

        public override byte[] Decode(ref SequenceReader<byte> reader)
        {
            var lenght = _intCodec.Decode(ref reader);
            byte[] result = new byte[lenght];

            if (!reader.TryCopyTo(result.AsSpan()))
                throw new InvalidOperationException("Failed to copy from SequenceReader.");

            reader.Advance(lenght);
            return result;
        }
    }
}
