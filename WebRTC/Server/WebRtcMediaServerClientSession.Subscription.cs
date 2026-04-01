using System;
using System.Linq;
using UnityEngine;

internal sealed partial class WebRtcMediaServerClientSession
{
    public void PauseTransmission()
    {
        _isTransmissionPaused = true;
        _trackService.ApplyTransmissionStateToAllSenders(true);
    }

    public void ResumeTransmission()
    {
        _isTransmissionPaused = false;
        _trackService.ApplyTransmissionStateToAllSenders(false);

        var peer = _peerController?.Peer;
        if (peer != null)
            SendTrackMap(_trackService.BuildTrackMapJson(peer));
    }

    public bool ApplyEncoderSettingsNow()
    {
        var peer = _peerController?.Peer;
        if (peer == null)
            return false;

        _trackService.ApplyPreferredVideoCodecs(peer);
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
        return true;
    }

    public bool ReapplyCurrentSubscription()
    {
        if (_peerController?.Peer == null)
            return false;

        if (_lastSubscriptionRequest == null)
        {
            EnqueueCatalog();
            return false;
        }

        ApplySubscription(_lastSubscriptionRequest, rememberRequest: false, sendAck: false);
        EnqueueCatalog();
        return true;
    }

    public bool DebugAddNextSource()
    {
        if (_peerController?.Peer == null)
            return false;

        var catalog = _server.BuildCatalogMessage();
        if (catalog?.sources == null || catalog.sources.Length == 0)
            return false;

        var activeIds = _activeSources.Select(source => source.SourceId).ToHashSet(StringComparer.Ordinal);
        var nextSource = catalog.sources.FirstOrDefault(source => source != null && !activeIds.Contains(source.sourceId));
        if (nextSource == null)
            return false;

        MediaSubscriptionRequest request = BuildEditableRequestFromCurrentSelection();
        int nextSlotIndex = request.subscriptions.Length == 0
            ? 0
            : request.subscriptions.Max(entry => entry.clientSlotIndex) + 1;

        Array.Resize(ref request.subscriptions, request.subscriptions.Length + 1);
        request.subscriptions[^1] = new MediaSubscriptionEntry
        {
            sourceId = nextSource.sourceId,
            clientSlotIndex = nextSlotIndex,
            clientMonitorIndex = 0,
            clientPanelIndex = nextSlotIndex,
            note = "debug-add"
        };

        ApplySubscription(request, rememberRequest: true, sendAck: true);
        return true;
    }

    public bool DebugRemoveLastSource()
    {
        if (_peerController?.Peer == null || _activeSources.Length == 0)
            return false;

        MediaSubscriptionRequest request = BuildEditableRequestFromCurrentSelection();
        if (request.subscriptions.Length == 0)
            return false;

        int removeIndex = Array.FindLastIndex(request.subscriptions, entry => entry != null);
        if (removeIndex < 0)
            return false;

        var remaining = request.subscriptions
            .Where((entry, index) => entry != null && index != removeIndex)
            .ToArray();

        request.subscriptions = remaining;
        request.useDefaultLayout = false;
        request.allowEmptySelection = remaining.Length == 0;
        ApplySubscription(request, rememberRequest: true, sendAck: true);
        return true;
    }

    private void SendHello()
    {
        _signalingChannel.Send(MediaServerMessageTypes.Hello, JsonUtility.ToJson(new MediaServerHelloMessage
        {
            sessionId = SessionId,
            serverName = Application.productName,
            defaultTrackCount = _server.BuildDefaultRequest().subscriptions?.Length ?? 0
        }));
    }

    private void EnqueueCatalog()
    {
        _signalingChannel.Send(MediaServerMessageTypes.Catalog, JsonUtility.ToJson(_server.BuildCatalogMessage()));
    }

    private void ApplySubscription(MediaSubscriptionRequest request, bool rememberRequest, bool sendAck)
    {
        var peer = _peerController?.Peer;
        if (_isDisposed || peer == null)
            return;

        if (!string.IsNullOrWhiteSpace(request?.clientName))
            ClientName = request.clientName.Trim();

        if (rememberRequest)
            _lastSubscriptionRequest = CloneRequest(request);

        var nextSources = _server.ResolveSubscription(request);
        _server.LogRequestedActions(SessionId, ClientName, nextSources);
        _activeSources = nextSources;

        bool hasTracks = _trackService.SyncTracksWithSources(
            peer,
            nextSources,
            _isTransmissionPaused,
            out bool anyChanged,
            out bool topologyChanged);

        _trackService.ApplyPreferredVideoCodecs(peer);
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);

        if (sendAck)
        {
            _signalingChannel.Send(MediaServerMessageTypes.SubscribeAck, JsonUtility.ToJson(new MediaSubscriptionAck
            {
                sessionId = SessionId,
                acceptedCount = nextSources.Length,
                message = hasTracks
                    ? $"Accepted {nextSources.Length} source bindings."
                    : "No valid sources selected."
            }));
        }

        if (topologyChanged)
        {
            _peerController.RequestNegotiation();
            return;
        }

        if (!hasTracks || anyChanged)
            SendTrackMap(_trackService.BuildTrackMapJson(peer));
    }

    private MediaSubscriptionRequest BuildEditableRequestFromCurrentSelection()
    {
        if (_lastSubscriptionRequest != null && !_lastSubscriptionRequest.useDefaultLayout)
            return CloneRequest(_lastSubscriptionRequest);

        return new MediaSubscriptionRequest
        {
            clientName = string.IsNullOrWhiteSpace(ClientName) ? "Receiver" : ClientName,
            useDefaultLayout = false,
            allowEmptySelection = _activeSources.Length == 0,
            subscriptions = _activeSources
                .Select(source => new MediaSubscriptionEntry
                {
                    sourceId = source.SourceId,
                    clientSlotIndex = source.ClientSlotIndex,
                    clientMonitorIndex = source.ClientMonitorIndex,
                    clientPanelIndex = source.ClientPanelIndex,
                    note = "current"
                })
                .ToArray()
        };
    }

    private static MediaSubscriptionRequest CloneRequest(MediaSubscriptionRequest request)
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
