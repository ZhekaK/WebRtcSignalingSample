using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class SenderSourceActivationTable
{
    internal enum ActivationChangeType
    {
        Activated,
        Deactivated
    }

    internal readonly struct ActivationChange
    {
        public readonly ActivationChangeType ChangeType;
        public readonly string SourceId;
        public readonly int ServerDisplayIndex;
        public readonly RenderLayer RenderLayer;
        public readonly string SourceName;
        public readonly int ReferenceCount;

        public ActivationChange(
            ActivationChangeType changeType,
            string sourceId,
            int serverDisplayIndex,
            RenderLayer renderLayer,
            string sourceName,
            int referenceCount)
        {
            ChangeType = changeType;
            SourceId = sourceId ?? string.Empty;
            ServerDisplayIndex = serverDisplayIndex;
            RenderLayer = renderLayer;
            SourceName = sourceName ?? string.Empty;
            ReferenceCount = referenceCount;
        }
    }

    private sealed class SourceState
    {
        public string SourceId = string.Empty;
        public int ServerDisplayIndex = -1;
        public RenderLayer RenderLayer = RenderLayer.Visible;
        public string SourceName = string.Empty;
        public readonly HashSet<string> SessionIds = new(StringComparer.Ordinal);

        public int ReferenceCount => SessionIds.Count;
    }

    private readonly Dictionary<string, SourceState> _sourceStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _sessionSources = new(StringComparer.Ordinal);

    public ActivationChange[] ApplySessionSelection(string sessionId, IReadOnlyList<SenderSourceCatalog.ResolvedSource> selectedSources)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Array.Empty<ActivationChange>();

        var nextSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceById = new Dictionary<string, SenderSourceCatalog.ResolvedSource>(StringComparer.Ordinal);

        foreach (var source in selectedSources ?? Array.Empty<SenderSourceCatalog.ResolvedSource>())
        {
            if (string.IsNullOrWhiteSpace(source.SourceId))
                continue;

            nextSourceIds.Add(source.SourceId);
            sourceById[source.SourceId] = source;
        }

        if (!_sessionSources.TryGetValue(sessionId, out var previousSourceIds))
            previousSourceIds = new HashSet<string>(StringComparer.Ordinal);

        var transitions = new List<ActivationChange>();

        foreach (string sourceId in nextSourceIds)
        {
            if (!_sourceStates.TryGetValue(sourceId, out var state))
            {
                var source = sourceById[sourceId];
                state = new SourceState
                {
                    SourceId = source.SourceId,
                    ServerDisplayIndex = source.ServerDisplayIndex,
                    RenderLayer = source.RenderLayer,
                    SourceName = source.SourceName
                };
                _sourceStates[sourceId] = state;
            }

            bool wasInactive = state.ReferenceCount == 0;
            state.SessionIds.Add(sessionId);

            if (wasInactive && state.ReferenceCount == 1)
            {
                transitions.Add(new ActivationChange(
                    ActivationChangeType.Activated,
                    state.SourceId,
                    state.ServerDisplayIndex,
                    state.RenderLayer,
                    state.SourceName,
                    state.ReferenceCount));
            }
        }

        foreach (string sourceId in previousSourceIds)
        {
            if (nextSourceIds.Contains(sourceId))
                continue;

            if (!_sourceStates.TryGetValue(sourceId, out var state))
                continue;

            state.SessionIds.Remove(sessionId);
            if (state.ReferenceCount == 0)
            {
                transitions.Add(new ActivationChange(
                    ActivationChangeType.Deactivated,
                    state.SourceId,
                    state.ServerDisplayIndex,
                    state.RenderLayer,
                    state.SourceName,
                    state.ReferenceCount));
            }
        }

        _sessionSources[sessionId] = nextSourceIds;
        return transitions.ToArray();
    }

    public ActivationChange[] RemoveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessionSources.TryGetValue(sessionId, out _))
            return Array.Empty<ActivationChange>();

        var transitions = ApplySessionSelection(sessionId, Array.Empty<SenderSourceCatalog.ResolvedSource>());
        _sessionSources.Remove(sessionId);
        return transitions;
    }

    public string[] BuildDebugSnapshot()
    {
        return _sourceStates.Values
            .Where(state => state.ReferenceCount > 0)
            .OrderBy(state => state.ServerDisplayIndex)
            .ThenBy(state => state.RenderLayer)
            .Select(state => $"Display {state.ServerDisplayIndex + 1} / {state.RenderLayer} | refs={state.ReferenceCount} | sourceId={state.SourceId}")
            .ToArray();
    }
}
