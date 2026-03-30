using FlexNet;
using FlexNet.Interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

internal sealed class WebRtcMediaServerClientSession : IDisposable
{
    private readonly struct SignalingEnvelope
    {
        public readonly string Type;
        public readonly string Payload;

        public SignalingEnvelope(string type, string payload)
        {
            Type = type;
            Payload = payload;
        }
    }

    private readonly WebRtcMediaServer _server;
    private readonly SenderManager _manager;
    private readonly IFlexClient _signalingClient;
    private readonly WebRtcMediaServerTrackService _trackService;
    private readonly ConcurrentQueue<SignalingEnvelope> _outgoingMessages = new();
    private readonly SemaphoreSlim _outgoingSignal = new(0, int.MaxValue);

    private CancellationTokenSource _sessionCts = new();
    private Task _sendLoopTask;
    private Task _receiveLoopTask;
    private RTCPeerConnection _peer;

    private DelegateOnIceConnectionChange _onIceConnectionChange;
    private DelegateOnIceCandidate _onIceCandidate;
    private DelegateOnNegotiationNeeded _onNegotiationNeeded;

    private MediaServerSourceCatalog.ResolvedSource[] _activeSources = Array.Empty<MediaServerSourceCatalog.ResolvedSource>();
    private bool _isDisposed;
    private bool _isNegotiationInProgress;

    public string SessionId { get; }
    public string ClientName { get; private set; } = string.Empty;

    public WebRtcMediaServerClientSession(
        WebRtcMediaServer server,
        SenderManager manager,
        IFlexClient signalingClient,
        string sessionId)
    {
        _server = server;
        _manager = manager;
        _signalingClient = signalingClient;
        _trackService = new WebRtcMediaServerTrackService(manager);
        SessionId = sessionId;
    }

    public async Task StartAsync()
    {
        try
        {
            await Awaitable.MainThreadAsync();
            if (_isDisposed)
                return;

            var config = new RTCConfiguration
            {
                iceServers = Array.Empty<RTCIceServer>()
            };

            _peer = new RTCPeerConnection(ref config);
            _onIceCandidate = OnIceCandidate;
            _onIceConnectionChange = OnIceConnectionChange;
            _onNegotiationNeeded = OnNegotiationNeeded;

            _peer.OnIceCandidate = _onIceCandidate;
            _peer.OnIceConnectionChange = _onIceConnectionChange;
            _peer.OnNegotiationNeeded = _onNegotiationNeeded;

            _manager.EnsureWebRtcUpdateLoop();

            CancellationToken token = _sessionCts.Token;
            _sendLoopTask = Task.Run(() => SendSignalingLoopAsync(token), token);
            _receiveLoopTask = Task.Run(() => ReceiveSignalingLoopAsync(token), token);

            EnqueueSignalingMessage(MediaServerMessageTypes.Hello, JsonUtility.ToJson(new MediaServerHelloMessage
            {
                sessionId = SessionId,
                serverName = Application.productName,
                defaultTrackCount = _server.BuildDefaultRequest().subscriptions?.Length ?? 0
            }));

            EnqueueCatalog();
            ApplySubscription(null);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Dispose();
            _server.NotifySessionClosed(this);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            _sessionCts.Cancel();
        }
        catch
        {
        }

        while (_outgoingMessages.TryDequeue(out _))
        {
        }

        var peer = _peer;
        _peer = null;

        if (peer != null)
        {
            peer.OnIceCandidate = null;
            peer.OnIceConnectionChange = null;
            peer.OnNegotiationNeeded = null;
            _trackService.Shutdown(peer);
            peer.Close();
            peer.Dispose();
        }

        _signalingClient?.Dispose();
        _outgoingSignal.Dispose();
        _sessionCts.Dispose();
    }

    private void EnqueueCatalog()
    {
        EnqueueSignalingMessage(MediaServerMessageTypes.Catalog, JsonUtility.ToJson(_server.BuildCatalogMessage()));
    }

    private void ApplySubscription(MediaSubscriptionRequest request)
    {
        if (_isDisposed || _peer == null)
            return;

        if (!string.IsNullOrWhiteSpace(request?.clientName))
            ClientName = request.clientName.Trim();

        var nextSources = _server.ResolveSubscription(request);
        _server.LogRequestedActions(SessionId, ClientName, _activeSources, nextSources);
        _activeSources = nextSources;

        bool hasTracks = _trackService.SyncTracksWithSources(
            _peer,
            nextSources,
            isTransmissionPaused: false,
            out bool anyChanged,
            out bool topologyChanged);

        _trackService.ApplyPreferredVideoCodecs(_peer);
        _trackService.ApplyTransmissionStateToAllSenders(isTransmissionPaused: false);

        EnqueueSignalingMessage(MediaServerMessageTypes.SubscribeAck, JsonUtility.ToJson(new MediaSubscriptionAck
        {
            sessionId = SessionId,
            acceptedCount = nextSources.Length,
            message = hasTracks
                ? $"Accepted {nextSources.Length} source bindings."
                : "No valid sources selected."
        }));

        if (!hasTracks)
        {
            EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, _trackService.BuildTrackMapJson(_peer));
            return;
        }

        if (topologyChanged)
        {
            TryScheduleNegotiation();
            return;
        }

        if (anyChanged)
            EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, _trackService.BuildTrackMapJson(_peer));
    }

    private async Task SendSignalingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _outgoingSignal.WaitAsync(token);

                while (_outgoingMessages.TryDequeue(out var envelope))
                {
                    if (_isDisposed)
                        break;

                    _signalingClient.AddContent(envelope.Type).AddContent(envelope.Payload).Send();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    private async Task ReceiveSignalingLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !_isDisposed)
            {
                using var results = await _signalingClient.ReceiveAsync();
                if (token.IsCancellationRequested || _isDisposed)
                    break;

                results.GetContent(out string type).GetContent(out string json);
                await Awaitable.MainThreadAsync();

                if (token.IsCancellationRequested || _isDisposed)
                    break;

                switch (type)
                {
                    case SignalingMessageTypes.Candidate:
                        if (_peer == null)
                            break;

                        var candidate = JsonUtility.FromJson<RTCIceCandidateInit>(json);
                        _peer.AddIceCandidate(new RTCIceCandidate(candidate));
                        break;

                    case SignalingMessageTypes.Description:
                        var description = JsonUtility.FromJson<RTCSessionDescription>(json);
                        if (IsCurrentPeer(_peer))
                            _manager.StartCoroutine(SetRemoteDescription(_peer, description));
                        break;

                    case MediaServerMessageTypes.Subscribe:
                        var request = string.IsNullOrWhiteSpace(json)
                            ? null
                            : JsonUtility.FromJson<MediaSubscriptionRequest>(json);
                        ApplySubscription(request);
                        break;

                    case MediaServerMessageTypes.Hello:
                    case MediaServerMessageTypes.Catalog:
                    case MediaServerMessageTypes.SubscribeAck:
                    case SignalingMessageTypes.TrackMap:
                        break;

                    default:
                        Debug.LogWarning($"[MediaServer] Unknown signaling message type '{type}' for {SessionId}.");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            _server.NotifySessionClosed(this);

            if (!_isDisposed)
            {
                await Awaitable.MainThreadAsync();
                Dispose();
            }
        }
    }

    private void EnqueueSignalingMessage(string type, string payload)
    {
        if (_isDisposed || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(payload))
            return;

        _outgoingMessages.Enqueue(new SignalingEnvelope(type, payload));

        try
        {
            _outgoingSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnNegotiationNeeded()
    {
        TryScheduleNegotiation();
    }

    private void TryScheduleNegotiation()
    {
        var peer = _peer;
        if (!IsCurrentPeer(peer) || _isNegotiationInProgress)
            return;

        _manager.StartCoroutine(PeerNegotiationNeeded(peer));
    }

    private IEnumerator SetRemoteDescription(RTCPeerConnection peer, RTCSessionDescription description)
    {
        if (!IsCurrentPeer(peer))
            yield break;

        RTCSetSessionDescriptionAsyncOperation operation;
        try
        {
            operation = peer.SetRemoteDescription(ref description);
        }
        catch (ObjectDisposedException)
        {
            yield break;
        }

        yield return operation;

        if (!IsCurrentPeer(peer))
            yield break;

        if (operation.IsError)
            Debug.LogError($"[MediaServer] SetRemoteDescription error for {SessionId}: {operation.Error.message}");
    }

    private IEnumerator PeerNegotiationNeeded(RTCPeerConnection peer)
    {
        if (!IsCurrentPeer(peer) || _isNegotiationInProgress)
            yield break;

        _isNegotiationInProgress = true;

        try
        {
            RTCSignalingState signalingState;
            try
            {
                signalingState = peer.SignalingState;
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            if (!IsCurrentPeer(peer) || signalingState != RTCSignalingState.Stable)
                yield break;

            RTCSessionDescriptionAsyncOperation operation;
            try
            {
                operation = peer.CreateOffer();
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            yield return operation;

            if (!IsCurrentPeer(peer))
                yield break;

            if (operation.IsError)
            {
                Debug.LogError($"[MediaServer] CreateOffer error for {SessionId}: {operation.Error.message}");
                yield break;
            }

            yield return _manager.StartCoroutine(OnCreateOfferSuccess(peer, operation.Desc));
        }
        finally
        {
            _isNegotiationInProgress = false;
        }
    }

    private IEnumerator OnCreateOfferSuccess(RTCPeerConnection peer, RTCSessionDescription description)
    {
        if (!IsCurrentPeer(peer))
            yield break;

        RTCSetSessionDescriptionAsyncOperation operation;
        try
        {
            operation = peer.SetLocalDescription(ref description);
        }
        catch (ObjectDisposedException)
        {
            yield break;
        }

        yield return operation;

        if (!IsCurrentPeer(peer))
            yield break;

        if (operation.IsError)
        {
            Debug.LogError($"[MediaServer] SetLocalDescription error for {SessionId}: {operation.Error.message}");
            yield break;
        }

        EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, _trackService.BuildTrackMapJson(peer));
        EnqueueSignalingMessage(SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        var candidateInfo = new RTCIceCandidateInit
        {
            candidate = candidate.Candidate,
            sdpMid = candidate.SdpMid,
            sdpMLineIndex = candidate.SdpMLineIndex
        };

        EnqueueSignalingMessage(SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidateInfo));
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"[MediaServer] {SessionId} IceConnectionState: {state}");
    }

    private bool IsCurrentPeer(RTCPeerConnection peer)
    {
        return !_isDisposed && peer != null && ReferenceEquals(peer, _peer);
    }
}
