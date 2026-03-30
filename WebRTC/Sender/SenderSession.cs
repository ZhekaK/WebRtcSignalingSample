using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// Runtime sender workflow: signaling, peer lifecycle, and dynamic track management.
/// </summary>
public sealed class SenderSession : IDisposable
{
    /// <summary>
    /// Outbound signaling envelope processed by a dedicated send loop.
    /// </summary>
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

    private readonly SenderManager _manager;
    private readonly SenderTrackService _trackService;
    private readonly SenderStatsReporter _statsReporter = new();
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly SemaphoreSlim _outgoingSignal = new(0, int.MaxValue);
    private readonly ConcurrentQueue<SignalingEnvelope> _outgoingMessages = new();
    private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();

    private IFlexClient _signalingClient;
    private RTCPeerConnection _peer;

    private DelegateOnIceConnectionChange _onIceConnectionChange;
    private DelegateOnIceCandidate _onIceCandidate;
    private DelegateOnNegotiationNeeded _onNegotiationNeeded;

    private CancellationTokenSource _sessionCts = new();
    private Task _receiveLoopTask;
    private Task _sendLoopTask;

    private bool _isTransmissionPaused;
    private bool _isDisposed;
    private bool _isNegotiationInProgress;
    private int _autoRestartRequested;

    public bool IsConnectionReady => _peer != null && IsSignalingConnected();

    public SenderSession(SenderManager manager)
    {
        _manager = manager;
        _trackService = new SenderTrackService(manager);
    }

    /// <summary>
    /// Starts or reconnects sender runtime.
    /// </summary>
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

            if (_peer != null && IsSignalingConnected())
            {
                SyncTracksWithSources();
                return;
            }

            if (_peer != null || _signalingClient != null)
                ShutdownConnection();

            ResetSessionCancellation();
            token = _sessionCts.Token;

            if (token.IsCancellationRequested)
                return;

            _signalingClient = new VibeClient(ContentCodecDIProvider.Default, _memoryStreamManager);
            await _signalingClient.ConnectAsync(_manager.IP, _manager.Port);

            if (_isDisposed || token.IsCancellationRequested)
                return;

            Debug.Log($"SENDER signaling connected to {_manager.IP}:{_manager.Port}");

            await Awaitable.MainThreadAsync();

            if (_isDisposed || token.IsCancellationRequested)
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

            _sendLoopTask = Task.Run(() => SendSignalingLoopAsync(token), token);
            _receiveLoopTask = Task.Run(() => ReceiveSignalingLoopAsync(token), token);
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
                    // Session disposed while InitializeAsync was still unwinding.
                }
                catch (SemaphoreFullException)
                {
                    // Defensive: semaphore state already recovered.
                }
            }
        }
    }

    /// <summary>
    /// Performs a clean sender restart by reconnecting signaling and rebuilding tracks.
    /// </summary>
    public async Task RestartAsync()
    {
        if (_isDisposed)
            return;

        await InitializeAsync(forceReconnect: true);
    }

    /// <summary>
    /// Pauses transmission without destroying tracks.
    /// </summary>
    public void PauseTransmission()
    {
        _isTransmissionPaused = true;
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
    }

    /// <summary>
    /// Resumes transmission and republishes current track map.
    /// </summary>
    public void ResumeTransmission()
    {
        _isTransmissionPaused = false;
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);

        string trackMapJson = _trackService.BuildTrackMapJson(_peer);
        EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, trackMapJson);
    }

    /// <summary>
    /// Re-applies codec and encoder settings to all current RTP senders.
    /// </summary>
    public bool ApplyEncoderSettingsNow()
    {
        if (_peer == null || !_trackService.HasTracks)
            return false;

        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
        return true;
    }

    /// <summary>
    /// Resizes source RenderTexture and refreshes the track for a display index.
    /// </summary>
    public bool ChangeSourceResolution(int displayIndex, int width, int height)
    {
        if (displayIndex < 0 || _manager.SourceRenderTextures == null || displayIndex >= _manager.SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        var source = _manager.SourceRenderTextures[displayIndex];
        if (source == null)
        {
            Debug.LogError($"SourceRenderTextures[{displayIndex}] is null.");
            return false;
        }

        if (!_trackService.ResizeRenderTexture(source, width, height))
            return false;

        return RefreshSourceTrack(displayIndex);
    }

    /// <summary>
    /// Recreates sender track from current source texture.
    /// </summary>
    public bool RefreshSourceTrack(int displayIndex)
    {
        if (displayIndex < 0)
            return false;

        if (_manager.SourceRenderTextures == null || displayIndex >= _manager.SourceRenderTextures.Count)
            return false;

        var source = _manager.SourceRenderTextures[displayIndex];
        if (source == null)
            return false;

        return AddOrReplaceSourceTrack(displayIndex, source);
    }

    /// <summary>
    /// Adds a new sender track or replaces existing source for the given display index.
    /// </summary>
    public bool AddOrReplaceSourceTrack(int displayIndex, RenderTexture sourceTexture)
    {
        if (displayIndex < 0 || sourceTexture == null)
            return false;

        if (_peer == null)
            return false;

        bool changed = _trackService.AddOrReplaceSourceTrack(
            _peer,
            displayIndex,
            sourceTexture,
            _isTransmissionPaused,
            out bool topologyChanged);

        if (!changed)
            return false;

        PublishTrackState(topologyChanged);
        return true;
    }

    /// <summary>
    /// Removes sender track for the given display index.
    /// </summary>
    public bool RemoveSourceTrack(int displayIndex)
    {
        if (displayIndex < 0 || _peer == null)
            return false;

        bool removed = _trackService.RemoveSourceTrack(_peer, displayIndex, out bool topologyChanged);
        if (!removed)
            return false;

        PublishTrackState(topologyChanged);
        return true;
    }

    /// <summary>
    /// Synchronizes sender tracks with current manager SourceRenderTextures collection.
    /// </summary>
    public bool SyncTracksWithSources()
    {
        bool hasTracks = _trackService.SyncTracksWithSources(
            _peer,
            _manager.SourceRenderTextures,
            _isTransmissionPaused,
            out bool anyChanged,
            out bool topologyChanged);

        if (anyChanged || topologyChanged)
            PublishTrackState(topologyChanged);

        return hasTracks;
    }

    private bool IsSignalingConnected()
    {
        if (_signalingClient == null)
            return false;

        try
        {
            return _signalingClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Updates destination endpoint and performs a reconnect.
    /// </summary>
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

    private void PublishTrackState(bool topologyChanged)
    {
        var peer = _peer;
        if (!IsCurrentPeer(peer))
            return;

        _trackService.ApplyPreferredVideoCodecs(peer);
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);

        string trackMapJson = _trackService.BuildTrackMapJson(peer);
        EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, trackMapJson);

        if (topologyChanged)
            TryScheduleNegotiation();
    }

    private IEnumerator LogSenderStatsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_isDisposed)
        {
            int intervalSeconds = Mathf.Max(1, _manager.SenderStatsLogIntervalSec);

            if (_manager.EnableSenderStatsLogging && _peer != null && IsSignalingConnected())
            {
                var op = _peer.GetStats();
                yield return op;

                if (token.IsCancellationRequested || _isDisposed)
                    yield break;

                if (op.IsError)
                {
                    Debug.LogWarning($"[WebRTC Sender Stats] GetStats failed: {op.Error.errorType}");
                }
                else if (op.Value != null)
                {
                    _statsReporter.Log(op.Value);
                }
            }

            yield return new WaitForSeconds(intervalSeconds);
        }
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
                    var client = _signalingClient;
                    if (client == null)
                        continue;

                    client.AddContent(envelope.Type).AddContent(envelope.Payload).Send();
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                var signalingClient = _signalingClient;
                if (signalingClient == null)
                    break;

                using var results = await signalingClient.ReceiveAsync();
                if (token.IsCancellationRequested)
                    break;

                results.GetContent(out string type).GetContent(out string json);
                await Awaitable.MainThreadAsync();

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
                        var peer = _peer;
                        if (IsCurrentPeer(peer))
                            _manager.StartCoroutine(SetRemoteDescription(peer, description));
                        break;

                    case SignalingMessageTypes.TrackMap:
                        // Sender ignores receiver map.
                        break;

                    default:
                        throw new NotImplementedException();
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

        if (!token.IsCancellationRequested && !_isDisposed)
            RequestAutoRestart();
    }

    private void EnqueueSignalingMessage(string type, string payload)
    {
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(payload))
            return;

        _outgoingMessages.Enqueue(new SignalingEnvelope(type, payload));

        try
        {
            _outgoingSignal.Release();
        }
        catch (ObjectDisposedException)
        {
            // Session already disposed.
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
            Debug.LogError($"SetRemoteDescription error: {operation.Error.message}");
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
                Debug.LogError($"CreateOffer error: {operation.Error.message}");
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
            Debug.LogError($"SetLocalDescription error: {operation.Error.message}");
            yield break;
        }

        string trackMapJson = _trackService.BuildTrackMapJson(peer);
        EnqueueSignalingMessage(SignalingMessageTypes.TrackMap, trackMapJson);
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

    private bool IsCurrentPeer(RTCPeerConnection peer)
    {
        return !_isDisposed && peer != null && ReferenceEquals(peer, _peer);
    }

    private void ShutdownConnection()
    {
        _sessionCts?.Cancel();
        Interlocked.Exchange(ref _autoRestartRequested, 0);

        while (_outgoingMessages.TryDequeue(out _))
        {
        }

        _statsReporter.Reset();
        _isNegotiationInProgress = false;

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
        _signalingClient = null;

        _receiveLoopTask = null;
        _sendLoopTask = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        ShutdownConnection();

        _sessionCts?.Dispose();
        _outgoingSignal.Dispose();
        _reconnectLock.Dispose();
    }
}
