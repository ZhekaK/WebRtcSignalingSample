using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// Runtime sender workflow: session lifecycle and signaling orchestration.
/// </summary>
public sealed partial class SenderSession : IDisposable
{
    private readonly SenderManager _manager;
    private readonly SenderTrackService _trackService;
    private readonly SenderStatsReporter _statsReporter = new();
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

    private CancellationTokenSource _sessionCts = new();
    private FlexSignalingChannel _signalingChannel;
    private WebRtcOffererPeerController _peerController;
    private bool _isTransmissionPaused;
    private bool _isDisposed;
    private int _autoRestartRequested;

    public bool IsConnectionReady => _peerController?.HasPeer == true && _signalingChannel?.IsConnected == true;

    public SenderSession(SenderManager manager)
    {
        _manager = manager;
        _trackService = new SenderTrackService(manager);
    }

    public async Task InitializeAsync(bool forceReconnect)
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
            {
                SyncTracksWithSources();
                return;
            }

            if (_peerController != null || _signalingChannel != null)
                ShutdownConnection();

            ResetSessionCancellation();
            token = _sessionCts.Token;
            if (token.IsCancellationRequested)
                return;

            _signalingChannel = new FlexSignalingChannel();
            SubscribeToSignaling(_signalingChannel);
            await _signalingChannel.ConnectAsync(_manager.IP, _manager.Port, token);

            if (_isDisposed || token.IsCancellationRequested)
                return;

            Debug.Log($"SENDER signaling connected to {_manager.IP}:{_manager.Port}");

            await Awaitable.MainThreadAsync();
            if (_isDisposed || token.IsCancellationRequested)
                return;

            _peerController = new WebRtcOffererPeerController(
                _manager,
                peer => _trackService.BuildTrackMapJson(peer),
                SendLocalTrackMap,
                SendLocalDescription,
                SendLocalCandidate,
                OnIceConnectionChange,
                "[Sender]");
            _peerController.CreatePeer();

            _manager.EnsureWebRtcUpdateLoop();
            _signalingChannel.StartLoops(token);
            _manager.StartCoroutine(LogSenderStatsLoop(token));

            if (!SyncTracksWithSources())
                Debug.LogWarning("No valid SourceRenderTextures configured for sender.");
        }
        catch (Exception ex)
        {
            if (_isDisposed || token.IsCancellationRequested)
                return;

            Debug.LogException(ex);
            RequestAutoRestart();
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

        await InitializeAsync(forceReconnect: true);
    }

    public async Task ChangeDestinationEndpointAsync(string newIp, int newPort)
    {
        if (string.IsNullOrWhiteSpace(newIp))
        {
            Debug.LogError("New IP is empty.");
            return;
        }

        if (newPort <= 0)
        {
            Debug.LogError($"Invalid port: {newPort}");
            return;
        }

        bool changed = !string.Equals(_manager.IP, newIp, StringComparison.Ordinal) || _manager.Port != newPort;
        _manager.IP = newIp;
        _manager.Port = newPort;

        if (!changed)
            return;

        await InitializeAsync(forceReconnect: true);
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

            case SignalingMessageTypes.TrackMap:
                break;

            default:
                Debug.LogWarning($"[Sender] Unknown signaling type '{type}'.");
                break;
        }
    }

    private void OnSignalingConnectionLost(Exception ex)
    {
        if (_isDisposed)
            return;

        if (ex != null)
            Debug.LogWarning($"[Sender] Signaling disconnected: {ex.Message}");

        RequestAutoRestart();
    }

    private void SendLocalCandidate(RTCIceCandidateInit candidate)
    {
        _signalingChannel?.Send(SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidate));
    }

    private void SendLocalDescription(RTCSessionDescription description)
    {
        _signalingChannel?.Send(SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    private void SendLocalTrackMap(string json)
    {
        _signalingChannel?.Send(SignalingMessageTypes.TrackMap, json);
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"Sender IceConnectionState: {state}");

        if (state == RTCIceConnectionState.Failed ||
            state == RTCIceConnectionState.Disconnected ||
            state == RTCIceConnectionState.Closed)
        {
            RequestAutoRestart();
        }
    }

    private void RequestAutoRestart()
    {
        if (_isDisposed || _sessionCts == null || _sessionCts.IsCancellationRequested)
            return;

        if (Interlocked.Exchange(ref _autoRestartRequested, 1) == 1)
            return;

        _ = AutoRestartAsync();
    }

    private async Task AutoRestartAsync()
    {
        try
        {
            await Task.Delay(350);

            if (_isDisposed || _sessionCts == null || _sessionCts.IsCancellationRequested)
                return;

            await _manager.RestartTransmissionAsync();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _autoRestartRequested, 0);
        }
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

        Interlocked.Exchange(ref _autoRestartRequested, 0);
        _statsReporter.Reset();

        var signalingChannel = _signalingChannel;
        _signalingChannel = null;
        UnsubscribeFromSignaling(signalingChannel);
        signalingChannel?.Dispose();

        var peerController = _peerController;
        _peerController = null;
        var peer = peerController?.DetachPeer();
        _trackService.Shutdown(peer);
        WebRtcOffererPeerController.DisposePeer(peer);
        peerController?.Dispose();
    }
}
