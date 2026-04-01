using FlexNet.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

internal sealed partial class WebRtcMediaServerClientSession : IDisposable
{
    private readonly WebRtcMediaServer _server;
    private readonly SenderManager _manager;
    private readonly WebRtcMediaServerTrackService _trackService;
    private readonly FlexSignalingChannel _signalingChannel;

    private CancellationTokenSource _sessionCts = new();
    private WebRtcOffererPeerController _peerController;
    private MediaServerSourceCatalog.ResolvedSource[] _activeSources = Array.Empty<MediaServerSourceCatalog.ResolvedSource>();
    private MediaSubscriptionRequest _lastSubscriptionRequest;
    private bool _isDisposed;
    private bool _isTransmissionPaused;

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
        _trackService = new WebRtcMediaServerTrackService(manager);
        _signalingChannel = new FlexSignalingChannel(signalingClient);
        SessionId = sessionId;
    }

    public async Task StartAsync()
    {
        try
        {
            await Awaitable.MainThreadAsync();
            if (_isDisposed)
                return;

            _peerController = new WebRtcOffererPeerController(
                _manager,
                peer => _trackService.BuildTrackMapJson(peer),
                SendTrackMap,
                SendDescription,
                SendCandidate,
                OnIceConnectionChange,
                $"[MediaServer] {SessionId}");
            _peerController.CreatePeer();

            _manager.EnsureWebRtcUpdateLoop();
            SubscribeToSignaling(_signalingChannel);
            _signalingChannel.StartLoops(_sessionCts.Token);

            SendHello();
            EnqueueCatalog();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            _server.NotifySessionClosed(this);
            Dispose();
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

        var signalingChannel = _signalingChannel;
        UnsubscribeFromSignaling(signalingChannel);
        signalingChannel?.Dispose();

        var peerController = _peerController;
        _peerController = null;
        var peer = peerController?.DetachPeer();
        _trackService.Shutdown(peer);
        WebRtcOffererPeerController.DisposePeer(peer);
        peerController?.Dispose();

        _sessionCts.Dispose();
    }

    private void SubscribeToSignaling(FlexSignalingChannel signalingChannel)
    {
        if (signalingChannel == null)
            return;

        signalingChannel.MessageReceived += OnSignalingMessageReceived;
        signalingChannel.ConnectionLost += OnSignalingConnectionLost;
    }

    private void UnsubscribeFromSignaling(FlexSignalingChannel signalingChannel)
    {
        if (signalingChannel == null)
            return;

        signalingChannel.MessageReceived -= OnSignalingMessageReceived;
        signalingChannel.ConnectionLost -= OnSignalingConnectionLost;
    }

    private void OnSignalingMessageReceived(string type, string json)
    {
        switch (type)
        {
            case SignalingMessageTypes.Candidate:
                _peerController?.AddIceCandidate(JsonUtility.FromJson<RTCIceCandidateInit>(json));
                break;

            case SignalingMessageTypes.Description:
                _peerController?.SetRemoteDescription(JsonUtility.FromJson<RTCSessionDescription>(json));
                break;

            case MediaServerMessageTypes.Subscribe:
                ApplySubscription(
                    string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MediaSubscriptionRequest>(json),
                    rememberRequest: true,
                    sendAck: true);
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

    private void OnSignalingConnectionLost(Exception ex)
    {
        if (_isDisposed)
            return;

        if (ex != null)
            Debug.LogWarning($"[MediaServer] Signaling disconnected for {SessionId}: {ex.Message}");

        _server.NotifySessionClosed(this);
        Dispose();
    }

    private void SendCandidate(RTCIceCandidateInit candidate)
    {
        _signalingChannel.Send(SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidate));
    }

    private void SendDescription(RTCSessionDescription description)
    {
        _signalingChannel.Send(SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    private void SendTrackMap(string json)
    {
        _signalingChannel.Send(SignalingMessageTypes.TrackMap, json);
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"[MediaServer] {SessionId} IceConnectionState: {state}");
    }
}
