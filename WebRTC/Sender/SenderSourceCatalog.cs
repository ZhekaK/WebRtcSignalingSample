using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

internal sealed class SenderSourceCatalog
{
    internal readonly struct ResolvedSource
    {
        public readonly string SourceId;
        public readonly int ClientSlotIndex;
        public readonly int ClientMonitorIndex;
        public readonly int ClientPanelIndex;
        public readonly int ServerDisplayIndex;
        public readonly RenderLayer RenderLayer;
        public readonly string SourceName;
        public readonly RenderTexture SourceTexture;

        public ResolvedSource(
            string sourceId,
            int clientSlotIndex,
            int clientMonitorIndex,
            int clientPanelIndex,
            int serverDisplayIndex,
            RenderLayer renderLayer,
            string sourceName,
            RenderTexture sourceTexture)
        {
            SourceId = sourceId ?? string.Empty;
            ClientSlotIndex = clientSlotIndex;
            ClientMonitorIndex = clientMonitorIndex;
            ClientPanelIndex = clientPanelIndex;
            ServerDisplayIndex = serverDisplayIndex;
            RenderLayer = renderLayer;
            SourceName = sourceName ?? string.Empty;
            SourceTexture = sourceTexture;
        }
    }

    private sealed class CatalogEntry
    {
        public MediaSourceDescriptor Descriptor;
        public RenderLayer RenderLayer;
        public RenderTexture Texture;
    }

    private long _catalogRevision;

    public MediaCatalogMessage BuildCatalogMessage()
    {
        List<CatalogEntry> snapshot = BuildSnapshot();
        return new MediaCatalogMessage
        {
            revision = Interlocked.Increment(ref _catalogRevision),
            sources = snapshot.Select(static entry => entry.Descriptor).ToArray()
        };
    }

    public SenderAvailableRenderTextureInfo[] BuildAvailableRenderTextureSnapshot()
    {
        return BuildSnapshot()
            .Select(entry => new SenderAvailableRenderTextureInfo
            {
                SourceId = entry.Descriptor.sourceId,
                SourceName = entry.Descriptor.sourceName,
                ServerDisplayIndex = entry.Descriptor.serverDisplayIndex,
                RenderLayer = entry.RenderLayer.ToString(),
                Width = entry.Descriptor.width,
                Height = entry.Descriptor.height,
                IsDefaultLayer = entry.Descriptor.isDefaultLayer,
                Texture = entry.Texture,
            })
            .ToArray();
    }

    public MediaSubscriptionRequest BuildDefaultRequest()
    {
        var snapshot = BuildSnapshot();
        var requestEntries = new List<MediaSubscriptionEntry>();
        int nextSlotIndex = 0;

        foreach (var group in snapshot.GroupBy(entry => entry.Descriptor.serverDisplayIndex).OrderBy(group => group.Key))
        {
            int panelIndex = 0;

            foreach (var entry in group.Take(2))
            {
                requestEntries.Add(new MediaSubscriptionEntry
                {
                    sourceId = entry.Descriptor.sourceId,
                    clientSlotIndex = nextSlotIndex,
                    clientMonitorIndex = group.Key,
                    clientPanelIndex = panelIndex,
                    note = "default"
                });

                nextSlotIndex++;
                panelIndex++;
            }
        }

        return new MediaSubscriptionRequest
        {
            clientName = string.Empty,
            useDefaultLayout = false,
            allowEmptySelection = false,
            subscriptions = requestEntries.ToArray()
        };
    }

    public ResolvedSource[] Resolve(MediaSubscriptionRequest request)
    {
        var snapshot = BuildSnapshot();
        var entryById = snapshot.ToDictionary(entry => entry.Descriptor.sourceId, StringComparer.Ordinal);
        MediaSubscriptionRequest effectiveRequest = request;

        if (effectiveRequest == null || effectiveRequest.useDefaultLayout)
            effectiveRequest = BuildDefaultRequest();
        else if ((effectiveRequest.subscriptions == null || effectiveRequest.subscriptions.Length == 0) && !effectiveRequest.allowEmptySelection)
            effectiveRequest = BuildDefaultRequest();

        if (effectiveRequest?.allowEmptySelection == true && (effectiveRequest.subscriptions == null || effectiveRequest.subscriptions.Length == 0))
            return Array.Empty<ResolvedSource>();

        var resolvedBySlot = new Dictionary<int, ResolvedSource>();
        int fallbackSlotIndex = 0;

        foreach (var subscription in effectiveRequest?.subscriptions ?? Array.Empty<MediaSubscriptionEntry>())
        {
            if (subscription == null || string.IsNullOrWhiteSpace(subscription.sourceId))
                continue;

            if (!entryById.TryGetValue(subscription.sourceId, out CatalogEntry entry) || entry.Texture == null)
            {
                Debug.LogWarning($"[SenderCatalog] Requested source '{subscription.sourceId}' is not available.");
                continue;
            }

            int clientSlotIndex = subscription.clientSlotIndex >= 0
                ? subscription.clientSlotIndex
                : fallbackSlotIndex;

            fallbackSlotIndex = Mathf.Max(fallbackSlotIndex, clientSlotIndex + 1);

            resolvedBySlot[clientSlotIndex] = new ResolvedSource(
                entry.Descriptor.sourceId,
                clientSlotIndex,
                subscription.clientMonitorIndex >= 0 ? subscription.clientMonitorIndex : clientSlotIndex,
                subscription.clientPanelIndex >= 0 ? subscription.clientPanelIndex : 0,
                entry.Descriptor.serverDisplayIndex,
                entry.RenderLayer,
                entry.Descriptor.sourceName,
                entry.Texture);
        }

        return resolvedBySlot
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .ToArray();
    }

    private static List<CatalogEntry> BuildSnapshot()
    {
        var results = new List<CatalogEntry>();
        var displays = DisplaysManager.Instance?.DisplaysDatas;
        if (displays == null)
            return results;

        foreach (var displayPair in displays.OrderBy(pair => (int)pair.Key))
        {
            int displayIndex = (int)displayPair.Key;
            var renderingLayers = displayPair.Value?.RenderingLayersDatas;
            if (renderingLayers == null)
                continue;

            int defaultLayerCount = 0;

            foreach (RenderLayer renderLayer in Enum.GetValues(typeof(RenderLayer)))
            {
                if (!renderingLayers.TryGetValue(renderLayer, out RenderLayerData layerData))
                    continue;

                if (layerData?.Settings == null || !layerData.Settings.ExistOnDisplay || layerData.RT == null)
                    continue;

                bool isDefaultLayer = defaultLayerCount < 2;
                if (isDefaultLayer)
                    defaultLayerCount++;

                results.Add(new CatalogEntry
                {
                    Descriptor = new MediaSourceDescriptor
                    {
                        sourceId = BuildSourceId(displayIndex, renderLayer),
                        serverDisplayIndex = displayIndex,
                        renderLayer = renderLayer.ToString(),
                        sourceName = $"Display {displayIndex + 1} / {renderLayer}",
                        width = layerData.RT.width,
                        height = layerData.RT.height,
                        isDefaultLayer = isDefaultLayer
                    },
                    RenderLayer = renderLayer,
                    Texture = layerData.RT
                });
            }
        }

        return results;
    }

    private static string BuildSourceId(int displayIndex, RenderLayer renderLayer)
    {
        return $"display-{displayIndex + 1}-{renderLayer.ToString().ToLowerInvariant()}";
    }
}
