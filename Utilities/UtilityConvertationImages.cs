using NetworkDTO.Types;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class UtilityConvertationImages
{
    public static byte[] ConvertToPng(Image framesImage)
    {
        // 1. Получите сырые данные текстуры в виде массива байтов
        byte[] rawTextureData = framesImage.ImageBytes.ToArray();
        // 2. Получите графический формат и размеры текстуры
        GraphicsFormat format = GraphicsFormatUtility.GetGraphicsFormat(framesImage.format, false);
        uint width = (uint)framesImage.width;
        uint height = (uint)framesImage.height;
        // 3. Закодируйте массив в png
        byte[] pngBytes = ImageConversion.EncodeArrayToPNG(rawTextureData, format, width, height, 0);
        return pngBytes;
    }

    public static byte[] ConvertToPng(Texture2D framesImage)
    {
        // 1. Получите сырые данные текстуры в виде массива байтов
        byte[] rawTextureData = framesImage.GetRawTextureData();
        // 2. Получите графический формат и размеры текстуры
        GraphicsFormat format = GraphicsFormatUtility.GetGraphicsFormat(framesImage.format, false);
        uint width = (uint)framesImage.width;
        uint height = (uint)framesImage.height;
        // 3. Закодируйте массив в png
        byte[] pngBytes = ImageConversion.EncodeArrayToPNG(rawTextureData, format, width, height, 0);
        return pngBytes;
    }

    public static byte[] ConvertToJpeg(Image framesImage)
    {
        // 1. Получите сырые данные текстуры в виде массива байтов
        byte[] rawTextureData = framesImage.ImageBytes.ToArray();
        // 2. Получите графический формат и размеры текстуры
        GraphicsFormat format = GraphicsFormatUtility.GetGraphicsFormat(framesImage.format, false);
        uint width = (uint)framesImage.width;
        uint height = (uint)framesImage.height;
        // 3. Закодируйте массив в JPG
        int jpgQuality = 75;//jpg qualuty: 1 - lowest, 100 - hightest. 75 - default
        byte[] jpgBytes = ImageConversion.EncodeArrayToJPG(rawTextureData, format, width, height, 0, jpgQuality);
        return jpgBytes;
    }

    public static byte[] ConvertToJpeg(Texture2D framesImage, int jpgQuality)
    {
        // 1. Получите сырые данные текстуры в виде массива байтов
        byte[] rawTextureData = framesImage.GetRawTextureData();
        // 2. Получите графический формат и размеры текстуры
        GraphicsFormat format = GraphicsFormatUtility.GetGraphicsFormat(framesImage.format, false);
        uint width = (uint)framesImage.width;
        uint height = (uint)framesImage.height;
        // 3. Закодируйте массив в JPG
        byte[] jpgBytes = ImageConversion.EncodeArrayToJPG(rawTextureData, format, width, height, 0, jpgQuality);
        return jpgBytes;
    }

    public static byte[] ConvertToRaw(byte[] compressedImage)
    {
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);

        try
        {
            // Декодируем PNG
            if (!texture.LoadImage(compressedImage))
            {
                Debug.LogError("Не удалось декодировать PNG");
                return null;
            }

            // Получаем raw байты
            byte[] rawBytes = texture.GetRawTextureData();

            return rawBytes;
        }
        finally
        {
            // Уничтожаем временную текстуру
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public static Image ConvertToRaw(byte[] compressedImage, NetworkDisplay display)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(compressedImage))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            return default;
        }

        var data = new Image(display, 
            texture.width, 
            texture.height, 
            texture.GetRawTextureData(), 
            texture.format, 
            false);

        UnityEngine.Object.DestroyImmediate(texture);
        return data;
    }
}
