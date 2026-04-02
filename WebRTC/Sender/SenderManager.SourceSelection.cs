using System;
using System.Collections.Generic;
using TabletTypes;
using UnityEngine;

public partial class SenderManager
{
    private void LayerDataReceiverModeController_OnStateChanged(object sender, EvsStateChangedEventArgs e)
    {
        if (e.CurrentStateEvs)
            SetRenderTextures(RenderLayer.EVS);
        else
            SetRenderTextures(CastToNewEnum(e.CurrentLayer));
    }

    private RenderLayer CastToNewEnum(ImitatorVisibleLayer layer)
    {
        switch (layer)
        {
            case ImitatorVisibleLayer.Visible:
                return RenderLayer.Visible;
            case ImitatorVisibleLayer.LWIR:
                return RenderLayer.LWIR;
            case ImitatorVisibleLayer.SWIR:
                return RenderLayer.SWIR;
            case ImitatorVisibleLayer.Labels:
                return RenderLayer.Labels;
            default:
                return RenderLayer.Visible;
        }
    }

    public void SetRenderTextures(RenderLayer layer)
    {
        if (SourceRenderTextures == null)
            SourceRenderTextures = new List<RenderTexture>();
        else
            SourceRenderTextures.Clear();

        var displays = DisplaysManager.Instance?.DisplaysDatas;
        if (displays == null)
        {
            _relayHub?.RefreshAllClientSubscriptions();
            return;
        }

        foreach (DisplayData displayData in displays.Values)
        {
            if (displayData.RenderingLayersDatas.TryGetValue(layer, out RenderLayerData renderLayerData) &&
                renderLayerData.Settings.ExistOnDisplay)
            {
                SourceRenderTextures.Add(renderLayerData.RT);
            }
        }

        _relayHub?.RefreshAllClientSubscriptions();
    }

    public bool ChangeSourceResolution(int displayIndex, int width, int height)
    {
        if (displayIndex < 0 || SourceRenderTextures == null || displayIndex >= SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        RenderTexture sourceTexture = SourceRenderTextures[displayIndex];
        if (sourceTexture == null)
        {
            Debug.LogError($"No source texture configured for display index {displayIndex}.");
            return false;
        }

        if (sourceTexture.width == width && sourceTexture.height == height)
            return true;

        try
        {
            sourceTexture.Release();
            sourceTexture.width = width;
            sourceTexture.height = height;
            sourceTexture.Create();
            return RefreshSourceTrack(displayIndex);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }

    public bool RefreshSourceTrack(int displayIndex)
    {
        if (displayIndex < 0 || SourceRenderTextures == null || displayIndex >= SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        return _relayHub == null || _relayHub.RefreshAllClientSubscriptions();
    }

    public bool AddSourceTrack(RenderTexture sourceTexture, int displayIndex = -1)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Cannot add null source texture.");
            return false;
        }

        int targetIndex = ResolveTargetDisplayIndex(displayIndex);
        EnsureSourceCapacity(targetIndex + 1);
        SourceRenderTextures[targetIndex] = sourceTexture;
        return _relayHub == null || _relayHub.RefreshAllClientSubscriptions();
    }

    public bool RemoveSourceTrack(int displayIndex, bool clearSourceSlot = true)
    {
        if (displayIndex < 0 || SourceRenderTextures == null || displayIndex >= SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        if (clearSourceSlot)
            SourceRenderTextures[displayIndex] = null;

        TrimTrailingEmptySourceSlots();
        return _relayHub == null || _relayHub.RefreshAllClientSubscriptions();
    }

    private int ResolveTargetDisplayIndex(int preferredIndex)
    {
        if (preferredIndex >= 0)
            return preferredIndex;

        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
            return 0;

        for (int i = 0; i < SourceRenderTextures.Count; i++)
        {
            if (SourceRenderTextures[i] == null)
                return i;
        }

        return SourceRenderTextures.Count;
    }

    private void EnsureSourceCapacity(int requiredLength)
    {
        requiredLength = Mathf.Max(1, requiredLength);
        SourceRenderTextures ??= new List<RenderTexture>(requiredLength);

        while (SourceRenderTextures.Count < requiredLength)
            SourceRenderTextures.Add(null);
    }

    private void TrimTrailingEmptySourceSlots()
    {
        if (SourceRenderTextures == null)
            return;

        while (SourceRenderTextures.Count > 0 && SourceRenderTextures[SourceRenderTextures.Count - 1] == null)
            SourceRenderTextures.RemoveAt(SourceRenderTextures.Count - 1);
    }
}
