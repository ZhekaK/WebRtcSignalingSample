using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using NetworkDTO.Types;
using System;
using System.Buffers;
using UnityEngine;

namespace SgsDemonstrator
{
    [ContentCodec]
    public class EvsImageCodec : ContentCodec<Image>
    {
        private readonly IContentCodec<int> _intCodec;
        private readonly IContentCodec<Memory<byte>> _bytesCodec;
        private readonly IContentCodec<bool> _boolCodec;
        private readonly IContentCodec<NetworkDisplay> _ndCodec;
        public EvsImageCodec(IContentCodec<int> intCodec, IContentCodec<Memory<byte>> bytesCodec, IContentCodec<bool> boolCodec, IContentCodec<NetworkDisplay> ndCodec)
        {
            _intCodec = intCodec;
            _bytesCodec = bytesCodec;
            _ndCodec = ndCodec;
            _boolCodec = boolCodec;
        }
        public override Image Decode(ref SequenceReader<byte> reader)
        {
            var nd = _ndCodec.Decode(ref reader);
            var width = _intCodec.Decode(ref reader);
            var height = _intCodec.Decode(ref reader);
            var format = (TextureFormat)_intCodec.Decode(ref reader);
            var hasMipMap = _boolCodec.Decode(ref reader);
            var image = _bytesCodec.Decode(ref reader);

            return new(nd, width, height, image, format, hasMipMap);
        }

        public override int Encode(IBufferWriter<byte> writer, Image value)
        {
            var writtenSize = _ndCodec.Encode(writer, value.Target);
            writtenSize += _intCodec.Encode(writer, value.width);
            writtenSize += _intCodec.Encode(writer, value.height);
            writtenSize += _intCodec.Encode(writer, (int)value.format);
            writtenSize += _boolCodec.Encode(writer, value.HasMipMap);
            writtenSize += _bytesCodec.Encode(writer, value.ImageBytes);

            return writtenSize;
        }

        public override int GetSize(Image value)
        {
            var size = _ndCodec.GetSize(value.Target);
            size += _intCodec.GetSize(value.width);
            size += _intCodec.GetSize(value.height);
            size += _intCodec.GetSize((int)value.format);
            size += _boolCodec.GetSize(value.HasMipMap);
            size += _bytesCodec.GetSize(value.ImageBytes);

            return size;
        }
    }
}
