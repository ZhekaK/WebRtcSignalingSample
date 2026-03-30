using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class Texture2DExtensions
{
    /// <summary> Reinitialize the created Texture2D if necessary </summary>
    public static void Reinitialize(this Texture2D texture2D, Vector2Int resolution, TextureFormat format)
    {
        if (!texture2D)
        {
            Debug.LogError("<color=red>Cannot reinitialize null-Texture2D</color>");
            return;
        }

        if (!texture2D.NeedToReinitialize(resolution, format)) return;

        // Store original properties
        string originalName = texture2D.name;
        FilterMode originalFilterMode = texture2D.filterMode;
        TextureWrapMode originalWrapMode = texture2D.wrapMode;
        int originalAnisoLevel = texture2D.anisoLevel;

        // Reinitialize Texture2D
        texture2D.Reinitialize(resolution.x, resolution.y, format, false);
        texture2D.name = !string.IsNullOrEmpty(originalName) ? originalName : "New Texture2D";
        texture2D.filterMode = originalFilterMode;
        texture2D.wrapMode = originalWrapMode;
        texture2D.anisoLevel = originalAnisoLevel;

        // Apply changes
        texture2D.Apply();
    }

    /// <summary> Reinitialize the created Texture2D if necessary </summary>
    public static void Reinitialize(this Texture2D texture2D, Vector2Int resolution, GraphicsFormat format)
    {
        if (!texture2D)
        {
            Debug.LogError("<color=red>Cannot reinitialize null-Texture2D</color>");
            return;
        }

        if (!texture2D.NeedToReinitialize(resolution, GraphicsFormatUtility.GetTextureFormat(format))) return;

        // Save original properties
        string originalName = texture2D.name;
        FilterMode originalFilterMode = texture2D.filterMode;
        TextureWrapMode originalWrapMode = texture2D.wrapMode;
        int originalAnisoLevel = texture2D.anisoLevel;

        // Reinitialize created RenderTexture
        texture2D.Reinitialize(resolution.x, resolution.y, GraphicsFormatUtility.GetTextureFormat(format), false);
        texture2D.name = !string.IsNullOrEmpty(originalName) ? originalName : "New Texture2D";
        texture2D.filterMode = originalFilterMode;
        texture2D.wrapMode = originalWrapMode;
        texture2D.anisoLevel = originalAnisoLevel;
        texture2D.Apply();
    }

    private static bool NeedToReinitialize(this Texture2D texture2D, Vector2Int resolution, TextureFormat format) => texture2D && (texture2D.width != resolution.x || texture2D.height != resolution.y || texture2D.format != format);
}