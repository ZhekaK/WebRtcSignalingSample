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
            return;

        foreach (DisplayData displayData in displays.Values)
        {
            if (displayData.RenderingLayersDatas.TryGetValue(layer, out RenderLayerData renderLayerData) &&
                renderLayerData.Settings.ExistOnDisplay)
            {
                SourceRenderTextures.Add(renderLayerData.RT);
            }
        }

        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            _mediaServer?.RefreshAllClientSubscriptions();
            return;
        }

        _session?.SyncTracksWithSources();
    }

    public bool ChangeSourceResolution(int displayIndex, int width, int height)
    {
        return _session != null && _session.ChangeSourceResolution(displayIndex, width, height);
    }

    public bool RefreshSourceTrack(int displayIndex)
    {
        return _session != null && _session.RefreshSourceTrack(displayIndex);
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

        if (_session == null)
            return true;

        return _session.AddOrReplaceSourceTrack(targetIndex, sourceTexture);
    }

    public bool RemoveSourceTrack(int displayIndex, bool clearSourceSlot = true)
    {
        if (displayIndex < 0 || SourceRenderTextures == null || displayIndex >= SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        bool removed = _session == null || _session.RemoveSourceTrack(displayIndex);
        if (!removed)
            return false;

        if (clearSourceSlot)
            SourceRenderTextures[displayIndex] = null;

        TrimTrailingEmptySourceSlots();
        return true;
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
