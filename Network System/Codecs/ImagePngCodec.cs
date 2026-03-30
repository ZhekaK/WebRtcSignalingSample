using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using NetworkDTO.Types;
using System;
using System.Buffers;
using UnityEngine;

namespace SgsDemonstrator
{
    public class ImagePngCodec : ContentCodec<Image>
    {
        private readonly IContentCodec<int> _intCodec;
        private readonly IContentCodec<Memory<byte>> _bytesCodec;
        private readonly IContentCodec<bool> _boolCodec;
        private readonly IContentCodec<NetworkDisplay> _ndCodec;
        public ImagePngCodec(IContentCodec<int> intCodec, IContentCodec<Memory<byte>> bytesCodec, IContentCodec<bool> boolCodec, IContentCodec<NetworkDisplay> ndCodec)
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
            writtenSize += _intCodec.Encode(writer, -1); //width
            writtenSize += _intCodec.Encode(writer, -1); //height
            writtenSize += _intCodec.Encode(writer, (int)value.format);
            writtenSize += _boolCodec.Encode(writer, value.HasMipMap);

            byte[] bytes = UtilityConvertationImages.ConvertToPng(value);
            writtenSize += _bytesCodec.Encode(writer, bytes);

            return writtenSize;
        }

        public override int GetSize(Image value)
        {
            var size = _ndCodec.GetSize(value.Target);
            size += _intCodec.GetSize(value.width); //width
            size += _intCodec.GetSize(value.height); //height
            size += _intCodec.GetSize((int)value.format);
            size += _boolCodec.GetSize(value.HasMipMap);

            byte[] bytes = UtilityConvertationImages.ConvertToPng(value);
            size += _bytesCodec.GetSize(bytes);

            return size;
        }
    }
}
