using System;
using UnityEngine;

namespace NetworkDTO.Types
{
    public readonly struct Image
    {
        public readonly NetworkDisplay Target;
        public readonly int width;
        public readonly int height;
        public readonly TextureFormat format;
        public readonly bool HasMipMap;
        public readonly Memory<byte> ImageBytes;
        public readonly bool IsRaw => width > 0 && height > 0;
        public readonly int BytesPerPixel
        {
            get
            {
                switch (format)
                {
                    case TextureFormat.RGBA32:
                    case TextureFormat.ARGB32:
                    case TextureFormat.BGRA32:
                        return 4;
                    case TextureFormat.RGB24:
                        return 3;
                    case TextureFormat.R8:
                        return 1;
                    case TextureFormat.R8_SIGNED:
                        return 1;
                    case TextureFormat.R16:
                        return 2;
                    case TextureFormat.RGBA64:
                        return 8;
                    default:
                        return 4;
                }
            }
        }

        public Image(NetworkDisplay target, int width, int height, Memory<byte> imageBytes, TextureFormat textureFormat, bool hasMipMap)
        {
            Target = target;
            this.width = width;
            this.height = height;
            ImageBytes = imageBytes;
            format = textureFormat;
            HasMipMap = hasMipMap;
        }
    }
}
