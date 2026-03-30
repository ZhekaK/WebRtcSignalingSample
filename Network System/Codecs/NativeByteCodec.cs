using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System;
using System.Buffers;
using Unity.Collections;

namespace SgsDemonstrator
{
    [ContentCodec]
    public class NativeByteCodec : ContentCodec<NativeArray<byte>>
    {
        private readonly IContentCodec<int> _intCodec;
        public NativeByteCodec(IContentCodec<int> intCodec)
        {
            _intCodec = intCodec;
        }
        public override NativeArray<byte> Decode(ref SequenceReader<byte> reader)
        {
            var lenght = _intCodec.Decode(ref reader);
            NativeArray<byte> result = new(lenght, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            if (!reader.TryCopyTo(result))
                throw new InvalidOperationException("Failed to copy from SequenceReader.");

            reader.Advance(lenght);
            return result;
        }

        public override int Encode(IBufferWriter<byte> writer, NativeArray<byte> value)
        {
            var writtenSize = _intCodec.Encode(writer, value.Length);
            writer.Write(value);
            return writtenSize + value.Length;
        }

        public override int GetSize(NativeArray<byte> value)
        {
            return value.Length + sizeof(int);
        }
    }

    [ContentCodec]
    public class MemoryByteCodec : ContentCodec<Memory<byte>>
    {
        private readonly IContentCodec<int> _intCodec;
        public MemoryByteCodec(IContentCodec<int> intCodec)
        {
            _intCodec = intCodec;
        }
        public override Memory<byte> Decode(ref SequenceReader<byte> reader)
        {
            var lenght = _intCodec.Decode(ref reader);
            Memory<byte> result = new(new byte[lenght]);

            if (!reader.TryCopyTo(result.Span))
                throw new InvalidOperationException("Failed to copy from SequenceReader.");

            reader.Advance(lenght);
            return result;
        }

        public override int Encode(IBufferWriter<byte> writer, Memory<byte> value)
        {
            var writtenSize = _intCodec.Encode(writer, value.Length);
            writer.Write(value.Span);
            return writtenSize + value.Length;
        }

        public override int GetSize(Memory<byte> value)
        {
            return value.Length + sizeof(int);
        }
    }
}
