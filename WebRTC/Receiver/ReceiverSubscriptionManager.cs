using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ReceiverSubscriptionScenario
{
    DefaultLayout = 0,
    OneSourcePerMonitor = 1,
    ThreeSourcesToCenterMonitor = 2,
    Custom = 3
}

public sealed class ReceiverSubscriptionManager
{
    private readonly ReceiverManager _manager;
    private MediaCatalogMessage _catalog;

    public ReceiverSubscriptionScenario ActiveScenario { get; private set; } = ReceiverSubscriptionScenario.DefaultLayout;
    public MediaSubscriptionRequest CurrentRequest { get; private set; }

    public ReceiverSubscriptionManager(ReceiverManager manager)
    {
        _manager = manager;
    }

    public void UpdateCatalog(MediaCatalogMessage catalog)
    {
        _catalog = catalog;
    }

    public MediaSubscriptionRequest UseDefaultLayout()
    {
        ActiveScenario = ReceiverSubscriptionScenario.DefaultLayout;
        CurrentRequest = new MediaSubscriptionRequest
        {
            clientName = _manager.ClientName,
            useDefaultLayout = true,
            allowEmptySelection = false,
            subscriptions = Array.Empty<MediaSubscriptionEntry>()
        };
        return Clone(CurrentRequest);
    }

    public MediaSubscriptionRequest UseCustomRequest(List<MediaSubscriptionEntry> neededSubscriptions)
    {
        ActiveScenario = ReceiverSubscriptionScenario.Custom;
        CurrentRequest = new MediaSubscriptionRequest
        {
            clientName = _manager.ClientName,
            useDefaultLayout = false,
            allowEmptySelection= neededSubscriptions.Count == 0,
            subscriptions = neededSubscriptions.ToArray()
        };
        return Clone(CurrentRequest);
    }

    public MediaSubscriptionRequest UseOneSourcePerMonitor()
    {
        var catalogSources = GetCatalogSourcesOrdered();
        if (catalogSources.Length == 0)
            return UseDefaultLayout();

        ActiveScenario = ReceiverSubscriptionScenario.OneSourcePerMonitor;

        var groupedSources = catalogSources
            .GroupBy(source => source.serverDisplayIndex)
            .OrderBy(group => group.Key)
            .ToArray();

        int monitorCount = Mathf.Max(1, ResolveOutputSlotCount());
        var subscriptions = new List<MediaSubscriptionEntry>(Mathf.Min(monitorCount, groupedSources.Length));

        for (int i = 0; i < groupedSources.Length && subscriptions.Count < monitorCount; i++)
        {
            MediaSourceDescriptor source = PickPreferredSource(groupedSources[i]);
            if (source == null)
                continue;

            subscriptions.Add(new MediaSubscriptionEntry
            {
                sourceId = source.sourceId,
                clientSlotIndex = subscriptions.Count,
                clientMonitorIndex = source.serverDisplayIndex,
                clientPanelIndex = 0,
                note = "one-source-per-monitor"
            });
        }

        CurrentRequest = BuildExplicitRequest(subscriptions, allowEmptySelection: subscriptions.Count == 0);
        return Clone(CurrentRequest);
    }

    public MediaSubscriptionRequest UseThreeSourcesToCenterMonitor()
    {
        var catalogSources = GetCatalogSourcesOrdered();
        if (catalogSources.Length == 0)
            return UseDefaultLayout();

        ActiveScenario = ReceiverSubscriptionScenario.ThreeSourcesToCenterMonitor;

        int centerMonitorIndex = ResolveCenterMonitorIndex();
        var centerSources = catalogSources
            .Where(source => source.serverDisplayIndex == centerMonitorIndex)
            .ToArray();

        if (centerSources.Length == 0)
            centerSources = catalogSources;

        var subscriptions = centerSources
            .Take(3)
            .Select((source, index) => new MediaSubscriptionEntry
            {
                sourceId = source.sourceId,
                clientSlotIndex = index,
                clientMonitorIndex = centerMonitorIndex,
                clientPanelIndex = index,
                note = "three-sources-center"
            })
            .ToList();

        CurrentRequest = BuildExplicitRequest(subscriptions, allowEmptySelection: subscriptions.Count == 0);
        return Clone(CurrentRequest);
    }

    public MediaSubscriptionRequest UseManualSelection(IEnumerable<string> sourceIds)
    {
        ActiveScenario = ReceiverSubscriptionScenario.Custom;
        CurrentRequest = BuildRequestFromSourceIds(sourceIds, "manual-selection");
        return Clone(CurrentRequest);
    }
    public MediaSubscriptionRequest BuildRequestForActiveScenario()
    {
        return ActiveScenario switch
        {
            ReceiverSubscriptionScenario.DefaultLayout => UseDefaultLayout(),
            ReceiverSubscriptionScenario.OneSourcePerMonitor => UseOneSourcePerMonitor(),
            ReceiverSubscriptionScenario.ThreeSourcesToCenterMonitor => UseThreeSourcesToCenterMonitor(),
            ReceiverSubscriptionScenario.Custom => Clone(CurrentRequest),
            _ => UseDefaultLayout()
        };
    }

    private int ResolveOutputSlotCount()
    {
        if (_manager.OutputImages != null && _manager.OutputImages.Length > 0)
            return _manager.OutputImages.Length;

        if (_catalog?.sources != null && _catalog.sources.Length > 0)
            return Mathf.Max(1, _catalog.sources.Max(source => source.serverDisplayIndex) + 1);

        return 1;
    }

    private int ResolveCenterMonitorIndex()
    {
        int slotCount = ResolveOutputSlotCount();
        return slotCount >= 3 ? 1 : 0;
    }

    private MediaSubscriptionRequest BuildRequestFromSourceIds(IEnumerable<string> sourceIds, string note)
    {
        var ids = sourceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        if (ids.Length == 0)
            return BuildExplicitRequest(Array.Empty<MediaSubscriptionEntry>(), allowEmptySelection: true);

        var subscriptions = new List<MediaSubscriptionEntry>(ids.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            MediaSourceDescriptor descriptor = FindCatalogSource(ids[i]);
            subscriptions.Add(new MediaSubscriptionEntry
            {
                sourceId = ids[i],
                clientSlotIndex = i,
                clientMonitorIndex = descriptor?.serverDisplayIndex ?? 0,
                clientPanelIndex = i,
                note = note
            });
        }

        return BuildExplicitRequest(subscriptions, allowEmptySelection: false);
    }

    private MediaSubscriptionRequest BuildExplicitRequest(IEnumerable<MediaSubscriptionEntry> entries, bool allowEmptySelection)
    {
        return new MediaSubscriptionRequest
        {
            clientName = _manager.ClientName,
            useDefaultLayout = false,
            allowEmptySelection = allowEmptySelection,
            subscriptions = entries?
                .Where(entry => entry != null)
                .ToArray() ?? Array.Empty<MediaSubscriptionEntry>()
        };
    }

    private MediaSourceDescriptor[] GetCatalogSourcesOrdered()
    {
        if (_catalog?.sources == null)
            return Array.Empty<MediaSourceDescriptor>();

        return _catalog.sources
            .Where(source => source != null && !string.IsNullOrWhiteSpace(source.sourceId))
            .OrderBy(source => source.serverDisplayIndex)
            .ThenByDescending(source => source.isDefaultLayer)
            .ThenBy(source => source.sourceName)
            .ThenBy(source => source.sourceId)
            .ToArray();
    }

    private MediaSourceDescriptor FindCatalogSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || _catalog?.sources == null)
            return null;

        return _catalog.sources.FirstOrDefault(source =>
            source != null &&
            string.Equals(source.sourceId, sourceId, StringComparison.Ordinal));
    }

    private static MediaSourceDescriptor PickPreferredSource(IEnumerable<MediaSourceDescriptor> sources)
    {
        return sources?
            .OrderByDescending(source => source.isDefaultLayer)
            .ThenBy(source => source.sourceName)
            .ThenBy(source => source.sourceId)
            .FirstOrDefault();
    }

    private static MediaSubscriptionRequest Clone(MediaSubscriptionRequest request)
    {
        if (request == null)
            return null;

        return new MediaSubscriptionRequest
        {
            clientName = request.clientName,
            useDefaultLayout = request.useDefaultLayout,
            allowEmptySelection = request.allowEmptySelection,
            subscriptions = request.subscriptions == null
                ? Array.Empty<MediaSubscriptionEntry>()
                : request.subscriptions
                    .Where(entry => entry != null)
                    .Select(entry => new MediaSubscriptionEntry
                    {
                        sourceId = entry.sourceId,
                        clientSlotIndex = entry.clientSlotIndex,
                        clientMonitorIndex = entry.clientMonitorIndex,
                        clientPanelIndex = entry.clientPanelIndex,
                        note = entry.note
                    })
                    .ToArray()
        };
    }
}
