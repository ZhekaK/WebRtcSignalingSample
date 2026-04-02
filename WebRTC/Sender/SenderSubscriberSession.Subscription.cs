using System;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;

internal sealed partial class SenderSubscriberSession
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

        RTCPeerConnection peer = _peerController?.Peer;
        if (peer != null)
            SendTrackMap(_trackService.BuildTrackMapJson(peer));
    }

    public bool ApplyEncoderSettingsNow()
    {
        RTCPeerConnection peer = _peerController?.Peer;
        if (peer == null)
            return false;

        EnsureStatsLoopIfNeeded();
        _trackService.ApplyPreferredVideoCodecs(peer);
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
        return true;
    }

    public bool ReapplyCurrentSubscription()
    {
        if (_peerController?.Peer == null)
            return false;

        if (_lastSubscriptionRequest == null)
            return false;

        ApplySubscription(_lastSubscriptionRequest, rememberRequest: false, sendAck: false);
        return true;
    }

    private void ApplySubscription(MediaSubscriptionRequest request, bool rememberRequest, bool sendAck)
    {
        RTCPeerConnection peer = _peerController?.Peer;
        if (_isDisposed || peer == null)
            return;

        EnsureStatsLoopIfNeeded();

        if (!string.IsNullOrWhiteSpace(request?.clientName))
            ClientName = request.clientName.Trim();

        if (rememberRequest)
            _lastSubscriptionRequest = CloneRequest(request);

        SenderSourceCatalog.ResolvedSource[] nextSources = _hub.ResolveSubscription(request);
        _hub.LogRequestedActions(ReceiverClientId, ClientName, nextSources);
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
            _hub.SendToReceiver(ReceiverClientId, MediaRelayMessageTypes.SubscribeAck, JsonUtility.ToJson(new MediaSubscriptionAck
            {
                sessionId = ReceiverClientId,
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
