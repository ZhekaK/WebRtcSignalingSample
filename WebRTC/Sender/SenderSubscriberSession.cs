using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;

internal sealed partial class SenderSubscriberSession : IDisposable
{
    private readonly SenderRelayHub _hub;
    private readonly SenderManager _manager;
    private readonly SenderSubscriberTrackService _trackService;
    private readonly SenderStatsReporter _statsReporter = new();

    private WebRtcOffererPeerController _peerController;
    private SenderSourceCatalog.ResolvedSource[] _activeSources = Array.Empty<SenderSourceCatalog.ResolvedSource>();
    private MediaSubscriptionRequest _lastSubscriptionRequest;
    private Coroutine _statsCoroutine;
    private bool _isDisposed;
    private bool _isTransmissionPaused;
    private bool _isStarted;

    public string ReceiverClientId { get; }
    public string ClientName { get; private set; } = string.Empty;

    public SenderSubscriberSession(SenderRelayHub hub, SenderManager manager, string receiverClientId)
    {
        _hub = hub;
        _manager = manager;
        _trackService = new SenderSubscriberTrackService(manager, hub.SharedTrackRegistry);
        ReceiverClientId = receiverClientId ?? string.Empty;
    }

    public void EnsureStarted()
    {
        if (_isDisposed || _isStarted)
            return;

        _peerController = new WebRtcOffererPeerController(
            _manager,
            peer => _trackService.BuildTrackMapJson(peer),
            SendTrackMap,
            SendDescription,
            SendCandidate,
            OnIceConnectionChange,
            $"[Sender] {ReceiverClientId}");
        _peerController.CreatePeer();
        _manager.EnsureWebRtcUpdateLoop();
        EnsureStatsLoopIfNeeded();
        _isStarted = true;
    }

    public void HandleRemoteMessage(string type, string json)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(type))
            return;

        EnsureStarted();

        switch (type)
        {
            case SignalingMessageTypes.Candidate:
                _peerController?.AddIceCandidate(JsonUtility.FromJson<RTCIceCandidateInit>(json));
                break;

            case SignalingMessageTypes.Description:
                _peerController?.SetRemoteDescription(JsonUtility.FromJson<RTCSessionDescription>(json));
                break;

            case MediaRelayMessageTypes.Subscribe:
                ApplySubscription(
                    string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MediaSubscriptionRequest>(json),
                    rememberRequest: true,
                    sendAck: true);
                break;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopStatsLoop();

        WebRtcOffererPeerController peerController = _peerController;
        _peerController = null;

        RTCPeerConnection peer = peerController?.DetachPeer();
        _trackService.Shutdown(peer);
        WebRtcOffererPeerController.DisposePeer(peer);
        peerController?.Dispose();
    }

    private void SendCandidate(RTCIceCandidateInit candidate)
    {
        _hub.SendToReceiver(ReceiverClientId, SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidate));
    }

    private void SendDescription(RTCSessionDescription description)
    {
        _hub.SendToReceiver(ReceiverClientId, SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    private void SendTrackMap(string json)
    {
        _hub.SendToReceiver(ReceiverClientId, SignalingMessageTypes.TrackMap, json);
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"[Sender] {ReceiverClientId} IceConnectionState: {state}");
    }

    private void EnsureStatsLoopIfNeeded()
    {
        if (_statsCoroutine != null || !_manager.EnableSenderStatsLogging)
            return;

        _statsReporter.Reset();
        _statsCoroutine = _manager.StartCoroutine(StatsLoop());
    }

    private void StopStatsLoop()
    {
        if (_statsCoroutine == null)
            return;

        try
        {
            _manager.StopCoroutine(_statsCoroutine);
        }
        catch
        {
        }

        _statsCoroutine = null;
        _statsReporter.Reset();
    }

    private IEnumerator StatsLoop()
    {
        while (!_isDisposed)
        {
            if (!_manager.EnableSenderStatsLogging)
            {
                _statsCoroutine = null;
                yield break;
            }

            RTCPeerConnection peer = _peerController?.Peer;
            if (peer != null)
            {
                RTCStatsReportAsyncOperation operation = null;
                try
                {
                    operation = peer.GetStats();
                }
                catch (ObjectDisposedException)
                {
                }

                if (operation != null)
                {
                    yield return operation;

                    if (_isDisposed)
                    {
                        _statsCoroutine = null;
                        yield break;
                    }

                    if (operation.IsError)
                    {
                        Debug.LogWarning($"[Sender] {ReceiverClientId} GetStats failed: {operation.Error.message}");
                    }
                    else
                    {
                        using RTCStatsReport report = operation.Value;
                        _statsReporter.Log(report, ReceiverClientId);
                    }
                }
            }

            float waitSeconds = Mathf.Max(1, _manager.SenderStatsLogIntervalSec);
            float elapsed = 0f;
            while (elapsed < waitSeconds && !_isDisposed)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _statsCoroutine = null;
    }
}
