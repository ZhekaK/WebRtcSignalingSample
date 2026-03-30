using FlexNet.ContentCodecs;
using System.Buffers;

namespace NetworkDTO.Codecs
{
    public class JpegForNeuralnetCodec : ContentCodec<byte[]>
    {
        public override int GetSize(byte[] value)
        {
            return value.Length;
        }

        public override int Encode(IBufferWriter<byte> writer, byte[] value)
        {
            writer.Write(value);
            return value.Length;
        }

        public override byte[] Decode(ref SequenceReader<byte> reader)
        {
            return reader.Sequence.ToArray();
        }
    }
}
