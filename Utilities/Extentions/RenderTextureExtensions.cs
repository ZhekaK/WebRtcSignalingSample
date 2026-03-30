using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class RenderTextureExtensions
{
    /// <summary> Reinitialize the created RenderTexture if necessary </summary>
    public static void Reinitialize(this RenderTexture renderTexture, int width, int height, GraphicsFormat format)
    {
        if (!renderTexture)
        {
            Debug.LogError("<color=red>Cannot reinitialize null-RenderTexture</color>");
            return;
        }

        if (!renderTexture.NeedToReinitialize(width, height, format)) return;

        // Save original properties
        string originalName = renderTexture.name;
        FilterMode originalFilterMode = renderTexture.filterMode;
        TextureWrapMode originalWrapMode = renderTexture.wrapMode;
        int originalAnisoLevel = renderTexture.anisoLevel;
        renderTexture.Release();

        // Reinitialize created RenderTexture
        renderTexture.width = width;
        renderTexture.height = height;
        renderTexture.graphicsFormat = format;
        renderTexture.name = !string.IsNullOrEmpty(originalName) ? originalName : "New Render Texture";
        renderTexture.filterMode = originalFilterMode;
        renderTexture.wrapMode = originalWrapMode;
        renderTexture.anisoLevel = originalAnisoLevel;
        renderTexture.Create();
    }

    private static bool NeedToReinitialize(this RenderTexture renderTexture, int width, int height, GraphicsFormat format) => renderTexture && (renderTexture.width != width || renderTexture.height != height || renderTexture.graphicsFormat != format);

    /// <summary> Copy image from RenderTexture to Texture2D </summary>
    public static void CopyToTexture2D(this RenderTexture renderTexture, Texture2D texture2D)
    {
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;
    }

    /// <summary> Copies the contents of the RenderTexture to the specified Texture2D. The Texture2D must have the same dimensions as the RenderTexture </summary>
    public static void CopyToTexture2D(this RenderTexture renderTexture, Texture2D texture2D, bool applyChanges = true, bool makeNoLongerReadable = false)
    {
        if (renderTexture == null || texture2D == null)
            Debug.LogWarning($"<color=red>CopyToTexture2D method is aborted, because (RenderTexture) or (Texture2D) is null reference!</color>");

        if (renderTexture.NeedToReinitialize(texture2D.width, texture2D.height, texture2D.graphicsFormat))
            Debug.LogWarning($"<color=red>CopyToTexture2D method is aborted, because (RenderTexture) and (Texture2D) do not match dimensions or format!</color>");

        RenderTexture previousActive = RenderTexture.active;
        try
        {
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            if (applyChanges)
            {
                texture2D.Apply(updateMipmaps: false, makeNoLongerReadable);
            }
        }
        finally
        {
            RenderTexture.active = previousActive;
        }
    }
}