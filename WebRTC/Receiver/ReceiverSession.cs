using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public sealed class ReceiverSession : IDisposable
{
    private readonly ReceiverManager _manager;
    private readonly ReceiverTrackBindingService _trackBindingService;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

    private CancellationTokenSource _sessionCts = new();
    private ReceiverSignalingClient _signalingClient;
    private ReceiverPeerController _peerController;
    private bool _isDisposed;

    public bool IsConnectionReady => _peerController?.HasPeer == true && _signalingClient?.IsConnected == true;

    public ReceiverSession(ReceiverManager manager)
    {
        _manager = manager;
        _trackBindingService = new ReceiverTrackBindingService(manager);
    }

    public async Task InitializeAsync(bool forceReconnect = false)
    {
        bool lockAcquired = false;
        CancellationToken token = default;

        try
        {
            await _reconnectLock.WaitAsync();
            lockAcquired = true;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_isDisposed)
                return;

            if (forceReconnect)
                ShutdownConnection();

            if (IsConnectionReady)
                return;

            if (_peerController != null || _signalingClient != null)
                ShutdownConnection();

            ResetSessionCancellation();
            token = _sessionCts.Token;
            _trackBindingService.PrepareForConnection(Mathf.Max(_manager.OutputImages?.Length ?? 0, 0));

            _signalingClient = new ReceiverSignalingClient();
            SubscribeToSignaling(_signalingClient);
            await _signalingClient.ConnectAsync(_manager.RuntimeMode, _manager.IP, _manager.Port, token);

            if (_isDisposed || token.IsCancellationRequested)
                return;

            await Awaitable.MainThreadAsync();
            if (_isDisposed || token.IsCancellationRequested)
                return;

            _peerController = new ReceiverPeerController(
                _manager,
                _trackBindingService.HandleTrack,
                SendLocalCandidate,
                SendLocalDescription);
            _peerController.CreatePeer();

            _manager.EnsureWebRtcUpdateLoop();
            _signalingClient.StartLoops(token);
            _manager.OnSessionConnected();
        }
        catch (Exception ex)
        {
            if (_isDisposed || token.IsCancellationRequested)
                return;

            ShutdownConnection();
            Debug.LogWarning($"[Receiver] Connection attempt failed: {ex.Message}");
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    _reconnectLock.Release();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SemaphoreFullException)
                {
                }
            }
        }
    }

    public async Task RestartAsync()
    {
        if (_isDisposed)
            return;

        await InitializeAsync(true);
    }

    public void PauseReceiving()
    {
        _trackBindingService.PauseReceiving();
    }

    public void ResumeReceiving()
    {
        _trackBindingService.ResumeReceiving();
    }

    public bool SendMediaSubscriptionRequest(MediaSubscriptionRequest request)
    {
        return _signalingClient != null && _signalingClient.SendSubscriptionRequest(request);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        ShutdownConnection();
        _sessionCts?.Dispose();
        _reconnectLock.Dispose();
    }

    private void SubscribeToSignaling(ReceiverSignalingClient signalingClient)
    {
        if (signalingClient == null)
            return;

        signalingClient.CandidateReceived += OnRemoteCandidateReceived;
        signalingClient.DescriptionReceived += OnRemoteDescriptionReceived;
        signalingClient.TrackMapReceived += OnTrackMapReceived;
        signalingClient.HelloReceived += OnHelloReceived;
        signalingClient.CatalogReceived += OnCatalogReceived;
        signalingClient.SubscribeAckReceived += OnSubscribeAckReceived;
        signalingClient.ConnectionLost += OnSignalingConnectionLost;
    }

    private void UnsubscribeFromSignaling(ReceiverSignalingClient signalingClient)
    {
        if (signalingClient == null)
            return;

        signalingClient.CandidateReceived -= OnRemoteCandidateReceived;
        signalingClient.DescriptionReceived -= OnRemoteDescriptionReceived;
        signalingClient.TrackMapReceived -= OnTrackMapReceived;
        signalingClient.HelloReceived -= OnHelloReceived;
        signalingClient.CatalogReceived -= OnCatalogReceived;
        signalingClient.SubscribeAckReceived -= OnSubscribeAckReceived;
        signalingClient.ConnectionLost -= OnSignalingConnectionLost;
    }

    private void OnRemoteCandidateReceived(RTCIceCandidateInit candidate)
    {
        _peerController?.AddIceCandidate(candidate);
    }

    private void OnRemoteDescriptionReceived(RTCSessionDescription description)
    {
        _peerController?.EnqueueRemoteDescription(description);
    }

    private void OnTrackMapReceived(string json)
    {
        _trackBindingService.ApplyTrackMap(json);
    }

    private void OnHelloReceived(MediaServerHelloMessage hello)
    {
        Debug.Log($"[Receiver] MediaServer hello: {hello?.sessionId}");
    }

    private void OnCatalogReceived(MediaCatalogMessage catalog)
    {
        _manager.OnCatalogReceived(catalog);
    }

    private void OnSubscribeAckReceived(MediaSubscriptionAck ack)
    {
        Debug.Log($"[Receiver] SubscribeAck: {ack?.message}");
    }

    private void OnSignalingConnectionLost(Exception ex)
    {
        if (_isDisposed)
            return;

        if (ex != null)
            Debug.LogWarning($"[Receiver] Signaling disconnected: {ex.Message}");

        ShutdownConnection();
    }

    private void SendLocalCandidate(RTCIceCandidateInit candidate)
    {
        _signalingClient?.SendCandidate(candidate);
    }

    private void SendLocalDescription(RTCSessionDescription description)
    {
        _signalingClient?.SendDescription(description);
    }

    private void ResetSessionCancellation()
    {
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();
    }

    private void ShutdownConnection()
    {
        try
        {
            _sessionCts?.Cancel();
        }
        catch
        {
        }

        var signalingClient = _signalingClient;
        _signalingClient = null;
        UnsubscribeFromSignaling(signalingClient);
        signalingClient?.Dispose();

        var peerController = _peerController;
        _peerController = null;
        peerController?.Dispose();

        _trackBindingService.Shutdown();
    }
}
