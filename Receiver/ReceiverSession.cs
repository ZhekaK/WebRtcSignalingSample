using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime receiver workflow: signaling host, peer lifecycle, track routing, and output binding.
/// </summary>
public sealed class ReceiverSession : IDisposable
{
    /// <summary>
    /// Stores a track until a mapping message arrives.
    /// </summary>
    private sealed class PendingTrack
    {
        public string TrackId;
        public string Mid;
        public VideoStreamTrack Track;
    }

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

    private struct InboundStatsSnapshot
    {
        public long TimestampUs;
        public ulong BytesReceived;
        public ulong PacketsDiscarded;
        public uint FramesReceived;
        public uint FramesDecoded;
        public uint FramesDropped;
        public uint FreezeCount;
        public ulong QpSum;
        public double JitterBufferDelay;
        public double JitterBufferTargetDelay;
        public double JitterBufferMinimumDelay;
        public ulong JitterBufferEmittedCount;
        public uint PliCount;
        public uint NackCount;
    }

    private readonly ReceiverManager _manager;
    private readonly ReceiverRouteTable _routeTable = new();

    private readonly Dictionary<string, PendingTrack> _tracksWaitingForMap = new();
    private readonly Dictionary<string, Unity.WebRTC.OnVideoReceived> _trackCallbacks = new();
    private readonly Dictionary<string, InboundStatsSnapshot> _inboundStatsSnapshots = new();

    private readonly ConcurrentQueue<SignalingEnvelope> _outgoingMessages = new();
    private readonly SemaphoreSlim _outgoingSignal = new(0, int.MaxValue);
    private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();

    private IFlexClient _signalingClient;
    private RTCPeerConnection _peer;

    private DelegateOnIceConnectionChange _onIceConnectionChange;
    private DelegateOnIceCandidate _onIceCandidate;
    private DelegateOnTrack _onTrack;

    private CancellationTokenSource _sessionCts = new();
    private Task _receiveLoopTask;
    private Task _sendLoopTask;

    private VideoStreamTrack[] _displayTracks = Array.Empty<VideoStreamTrack>();
    private Texture[] _receivedTextures = Array.Empty<Texture>();
    private string[] _receivedTrackIds = Array.Empty<string>();
    private string[] _receivedMids = Array.Empty<string>();

    private bool _isReceivingPaused;
    private bool _isDisposed;
    private long _lastTrackMapRevision;
    private int _autoRestartRequested;

    public ReceiverSession(ReceiverManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Starts receiver runtime and begins listening for sender signaling.
    /// </summary>
    public async Task InitializeAsync(bool forceReconnect = false)
    {
        try
        {
            if (_isDisposed)
                return;

            if (forceReconnect)
                ShutdownConnection();

            if (_peer != null && _signalingClient != null)
                return;

            ResetSessionCancellation();
            EnsureCapacity(Mathf.Max(_manager.OutputImages?.Length ?? 0, 0));

            using var listener = new VibeListener(System.Net.IPAddress.Any, _manager.Port, ContentCodecDIProvider.Default, _memoryStreamManager);
            listener.Start();
            _signalingClient = await listener.AcceptFlexClientAsync();

            Debug.Log($"RECEIVER signaling accepted on port {_manager.Port}");

            await Awaitable.MainThreadAsync();

            var config = new RTCConfiguration
            {
                iceServers = Array.Empty<RTCIceServer>()
            };

            _peer = new RTCPeerConnection(ref config);

            _onIceConnectionChange = OnIceConnectionChange;
            _onIceCandidate = OnIceCandidate;
            _onTrack = OnTrack;

            _peer.OnIceCandidate = _onIceCandidate;
            _peer.OnIceConnectionChange = _onIceConnectionChange;
            _peer.OnTrack = _onTrack;

            _manager.EnsureWebRtcUpdateLoop();

            var token = _sessionCts.Token;
            _sendLoopTask = Task.Run(() => SendSignalingLoopAsync(token), token);
            _receiveLoopTask = Task.Run(ReceiveSignalingLoopAsync, token);
            _manager.StartCoroutine(LogReceiverStatsLoop(token));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// Performs a clean receiver restart by reconnecting signaling and rebuilding peer state.
    /// </summary>
    public async Task RestartAsync()
    {
        if (_isDisposed)
            return;

        await InitializeAsync(forceReconnect: true);
    }

    /// <summary>
    /// Stops video decoding updates and clears current output textures.
    /// </summary>
    public void PauseReceiving()
    {
        _isReceivingPaused = true;

        foreach (var track in _displayTracks)
        {
            UnregisterTrackCallback(track);
            SetTrackEnabled(track, false);
        }

        foreach (var pending in _tracksWaitingForMap.Values)
            SetTrackEnabled(pending.Track, false);

        SetAllReceiverTracksEnabled(false);

        for (int i = 0; i < _receivedTextures.Length; i++)
            _receivedTextures[i] = null;

        ClearOutputImages();
    }

    /// <summary>
    /// Re-enables track decoding and output rendering.
    /// </summary>
    public void ResumeReceiving()
    {
        _isReceivingPaused = false;

        SetAllReceiverTracksEnabled(true);

        foreach (var pending in _tracksWaitingForMap.Values)
            SetTrackEnabled(pending.Track, true);

        for (int i = 0; i < _displayTracks.Length; i++)
        {
            var track = _displayTracks[i];
            if (track == null)
                continue;

            SetTrackEnabled(track, true);
            RegisterTrackCallback(i, track);

            if (track.Texture != null)
                ApplyReceivedTexture(i, track.Id, track.Texture);
        }
    }

    private void ClearOutputImages()
    {
        var outputs = _manager.OutputImages;
        if (outputs == null)
            return;

        for (int i = 0; i < outputs.Length; i++)
        {
            if (outputs[i] != null && outputs[i].texture != null)
                outputs[i].texture = null;
        }
    }

    private IEnumerator LogReceiverStatsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_isDisposed)
        {
            int intervalSeconds = Mathf.Max(1, _manager.ReceiverStatsLogIntervalSec);

            if (_manager.EnableReceiverStatsLogging && _peer != null && _signalingClient != null)
            {
                var op = _peer.GetStats();
                yield return op;

                if (token.IsCancellationRequested || _isDisposed)
                    yield break;

                if (op.IsError)
                {
                    Debug.LogWarning($"[WebRTC Receiver Stats] GetStats failed: {op.Error.errorType}");
                }
                else if (op.Value != null)
                {
                    LogReceiverStats(op.Value);
                }
            }

            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    private void LogReceiverStats(RTCStatsReport report)
    {
        if (report == null)
            return;

        var inboundVideoStats = new List<RTCInboundRTPStreamStats>();
        var codecsById = new Dictionary<string, RTCCodecStats>(StringComparer.Ordinal);

        foreach (var stats in report.Stats.Values)
        {
            switch (stats)
            {
                case RTCInboundRTPStreamStats inbound when IsVideoKind(inbound.kind):
                    inboundVideoStats.Add(inbound);
                    break;
                case RTCCodecStats codec:
                    if (!string.IsNullOrEmpty(codec.Id) && !codecsById.ContainsKey(codec.Id))
                        codecsById.Add(codec.Id, codec);
                    break;
            }
        }

        if (inboundVideoStats.Count == 0)
            return;

        double totalBitrateBps = 0d;
        double totalFps = 0d;
        double totalJitterMs = 0d;
        int jitterSamples = 0;
        uint maxFrameWidth = 0;
        uint maxFrameHeight = 0;

        double totalQpDelta = 0d;
        double totalJitterBufferDelayDeltaMs = 0d;
        double totalJitterBufferTargetMs = 0d;
        double totalJitterBufferMinimumMs = 0d;
        ulong totalFramesDecodedDelta = 0;
        ulong totalJitterBufferEmittedDelta = 0;
        ulong totalPacketsDiscardedDelta = 0;
        uint totalFramesReceivedDelta = 0;
        uint totalFramesDroppedDelta = 0;
        uint totalFreezeDelta = 0;
        uint totalPliDelta = 0;
        uint totalNackDelta = 0;
        int jitterBufferTargetSamples = 0;
        int jitterBufferMinimumSamples = 0;

        var codecMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inbound in inboundVideoStats)
        {
            totalFps += Math.Max(0d, inbound.framesPerSecond);
            maxFrameWidth = Math.Max(maxFrameWidth, inbound.frameWidth);
            maxFrameHeight = Math.Max(maxFrameHeight, inbound.frameHeight);

            if (inbound.jitter >= 0d)
            {
                totalJitterMs += inbound.jitter * 1000d;
                jitterSamples++;
            }

            if (!string.IsNullOrEmpty(inbound.codecId) &&
                codecsById.TryGetValue(inbound.codecId, out var codecStats) &&
                !string.IsNullOrWhiteSpace(codecStats.mimeType))
            {
                codecMimeTypes.Add(codecStats.mimeType);
            }

            string statKey = string.IsNullOrEmpty(inbound.Id) ? inbound.ssrc.ToString() : inbound.Id;
            if (_inboundStatsSnapshots.TryGetValue(statKey, out var previous))
            {
                long durationUs = inbound.Timestamp - previous.TimestampUs;
                if (durationUs > 0 && inbound.bytesReceived >= previous.BytesReceived)
                {
                    double durationSec = durationUs / 1_000_000d;
                    ulong bytesDelta = inbound.bytesReceived - previous.BytesReceived;
                    totalBitrateBps += 8d * bytesDelta / durationSec;
                }

                if (inbound.framesReceived >= previous.FramesReceived)
                    totalFramesReceivedDelta += inbound.framesReceived - previous.FramesReceived;

                if (inbound.packetsDiscarded >= previous.PacketsDiscarded)
                    totalPacketsDiscardedDelta += inbound.packetsDiscarded - previous.PacketsDiscarded;

                if (inbound.framesDropped >= previous.FramesDropped)
                    totalFramesDroppedDelta += inbound.framesDropped - previous.FramesDropped;

                if (inbound.freezeCount >= previous.FreezeCount)
                    totalFreezeDelta += inbound.freezeCount - previous.FreezeCount;

                if (inbound.framesDecoded >= previous.FramesDecoded && inbound.qpSum >= previous.QpSum)
                {
                    uint framesDecodedDelta = inbound.framesDecoded - previous.FramesDecoded;
                    ulong qpDelta = inbound.qpSum - previous.QpSum;
                    if (framesDecodedDelta > 0)
                    {
                        totalFramesDecodedDelta += framesDecodedDelta;
                        totalQpDelta += qpDelta;
                    }
                }

                if (inbound.jitterBufferDelay >= previous.JitterBufferDelay &&
                    inbound.jitterBufferEmittedCount >= previous.JitterBufferEmittedCount)
                {
                    totalJitterBufferDelayDeltaMs += (inbound.jitterBufferDelay - previous.JitterBufferDelay) * 1000d;
                    totalJitterBufferEmittedDelta += inbound.jitterBufferEmittedCount - previous.JitterBufferEmittedCount;
                }

                if (inbound.pliCount >= previous.PliCount)
                    totalPliDelta += inbound.pliCount - previous.PliCount;

                if (inbound.nackCount >= previous.NackCount)
                    totalNackDelta += inbound.nackCount - previous.NackCount;
            }

            if (inbound.jitterBufferTargetDelay > 0d)
            {
                totalJitterBufferTargetMs += inbound.jitterBufferTargetDelay * 1000d;
                jitterBufferTargetSamples++;
            }

            if (inbound.jitterBufferMinimumDelay > 0d)
            {
                totalJitterBufferMinimumMs += inbound.jitterBufferMinimumDelay * 1000d;
                jitterBufferMinimumSamples++;
            }

            _inboundStatsSnapshots[statKey] = new InboundStatsSnapshot
            {
                TimestampUs = inbound.Timestamp,
                BytesReceived = inbound.bytesReceived,
                PacketsDiscarded = inbound.packetsDiscarded,
                FramesReceived = inbound.framesReceived,
                FramesDecoded = inbound.framesDecoded,
                FramesDropped = inbound.framesDropped,
                FreezeCount = inbound.freezeCount,
                QpSum = inbound.qpSum,
                JitterBufferDelay = inbound.jitterBufferDelay,
                JitterBufferTargetDelay = inbound.jitterBufferTargetDelay,
                JitterBufferMinimumDelay = inbound.jitterBufferMinimumDelay,
                JitterBufferEmittedCount = inbound.jitterBufferEmittedCount,
                PliCount = inbound.pliCount,
                NackCount = inbound.nackCount
            };
        }

        string codecText = codecMimeTypes.Count > 0
            ? string.Join(",", codecMimeTypes.OrderBy(value => value))
            : "unknown";
        string resolutionText = maxFrameWidth > 0 && maxFrameHeight > 0
            ? $"{maxFrameWidth}x{maxFrameHeight}"
            : "n/a";
        string avgQpText = totalFramesDecodedDelta > 0
            ? (totalQpDelta / totalFramesDecodedDelta).ToString("F1")
            : "n/a";
        string dropText = totalFramesReceivedDelta > 0
            ? $"{(100d * totalFramesDroppedDelta / totalFramesReceivedDelta):F2}%"
            : "n/a";
        string jitterText = jitterSamples > 0
            ? $"{(totalJitterMs / jitterSamples):F1} ms"
            : "n/a";
        string jitterBufferText = totalJitterBufferEmittedDelta > 0
            ? $"{(totalJitterBufferDelayDeltaMs / totalJitterBufferEmittedDelta):F2} ms"
            : "n/a";
        string jitterBufferTargetText = jitterBufferTargetSamples > 0
            ? $"{(totalJitterBufferTargetMs / jitterBufferTargetSamples):F2} ms"
            : "n/a";
        string jitterBufferMinimumText = jitterBufferMinimumSamples > 0
            ? $"{(totalJitterBufferMinimumMs / jitterBufferMinimumSamples):F2} ms"
            : "n/a";

        Debug.Log(
            $"[WebRTC Receiver Stats] rx={(totalBitrateBps / 1_000_000d):F1} Mbps streams={inboundVideoStats.Count} " +
            $"codec={codecText} res={resolutionText} fps={totalFps:F1} drop={dropText} avgQp={avgQpText} " +
            $"jitter={jitterText} jbuf={jitterBufferText} target={jitterBufferTargetText} min={jitterBufferMinimumText} " +
            $"discardDelta={totalPacketsDiscardedDelta} freezeDelta={totalFreezeDelta} nackDelta={totalNackDelta} pliDelta={totalPliDelta}");
    }

    private static bool IsVideoKind(string kind)
    {
        return string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase);
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

    private async Task ReceiveSignalingLoopAsync()
    {
        while (!_sessionCts.IsCancellationRequested)
        {
            try
            {
                var signalingClient = _signalingClient;
                if (signalingClient == null)
                    break;

                using var results = await signalingClient.ReceiveAsync();
                if (_sessionCts.IsCancellationRequested)
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
                        _manager.StartCoroutine(SetRemoteDescriptionAndSendAnswer(description));
                        break;

                    case SignalingMessageTypes.TrackMap:
                        ApplyTrackMap(json);
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

        if (!_sessionCts.IsCancellationRequested && !_isDisposed)
            RequestAutoRestart();
    }

    private void ApplyTrackMap(string json)
    {
        var map = JsonUtility.FromJson<TrackMapMessage>(json);
        if (map == null)
        {
            Debug.LogWarning("Invalid tracks-map payload received.");
            return;
        }

        if (map.revision > 0)
        {
            if (map.revision < _lastTrackMapRevision)
                return;

            _lastTrackMapRevision = map.revision;
        }

        if (map.activeTrackCount <= 0 || map.tracks == null || map.tracks.Length == 0)
        {
            _routeTable.Clear();
            UnbindDisplaysNotInMap(new HashSet<int>());
            ShrinkCapacity(0);
            return;
        }

        if (!_routeTable.Apply(map))
        {
            Debug.LogWarning("Invalid or empty tracks-map received.");
            return;
        }

        EnsureCapacity(_routeTable.RequiredDisplaySlots);
        UnbindDisplaysNotInMap(_routeTable.GetMappedDisplayIndices());
        TryBindWaitingTracks();
        ShrinkCapacity(_routeTable.RequiredDisplaySlots);
    }

    private void TryBindWaitingTracks()
    {
        if (_tracksWaitingForMap.Count == 0)
            return;

        var waiting = _tracksWaitingForMap.Values.ToArray();
        foreach (var pending in waiting)
        {
            if (_routeTable.TryResolve(pending.TrackId, pending.Mid, out int displayIndex))
            {
                BindTrackToDisplay(displayIndex, pending.Mid, pending.Track);
                _tracksWaitingForMap.Remove(pending.TrackId);
            }
        }
    }

    private void UnbindDisplaysNotInMap(HashSet<int> validDisplayIndices)
    {
        for (int i = 0; i < _displayTracks.Length; i++)
        {
            if (validDisplayIndices.Contains(i))
                continue;

            ClearDisplaySlot(i);
        }
    }

    private void OnTrack(RTCTrackEvent trackEvent)
    {
        if (trackEvent.Track is not VideoStreamTrack videoTrack)
            return;

        string trackId = videoTrack.Id;
        string mid = trackEvent.Transceiver?.Mid ?? string.Empty;

        if (_routeTable.TryResolve(trackId, mid, out int displayIndex))
        {
            BindTrackToDisplay(displayIndex, mid, videoTrack);
            return;
        }

        if (_tracksWaitingForMap.TryGetValue(trackId, out var existingPending) && !ReferenceEquals(existingPending.Track, videoTrack))
            DisposeTrackSafe(existingPending.Track);

        _tracksWaitingForMap[trackId] = new PendingTrack
        {
            TrackId = trackId,
            Mid = mid,
            Track = videoTrack
        };

        if (_isReceivingPaused)
            SetTrackEnabled(videoTrack, false);
    }

    private void BindTrackToDisplay(int displayIndex, string mid, VideoStreamTrack videoTrack)
    {
        EnsureCapacity(displayIndex + 1);

        if (_displayTracks[displayIndex] != null && _displayTracks[displayIndex] != videoTrack)
        {
            var oldTrack = _displayTracks[displayIndex];
            UnregisterTrackCallback(oldTrack);
            SetTrackEnabled(oldTrack, false);
            DisposeTrackSafe(oldTrack);
        }

        _displayTracks[displayIndex] = videoTrack;
        _receivedTrackIds[displayIndex] = videoTrack.Id;
        _receivedMids[displayIndex] = mid;

        if (_isReceivingPaused)
        {
            UnregisterTrackCallback(videoTrack);
            SetTrackEnabled(videoTrack, false);
            return;
        }

        SetTrackEnabled(videoTrack, true);
        RegisterTrackCallback(displayIndex, videoTrack);

        if (videoTrack.Texture != null)
            ApplyReceivedTexture(displayIndex, videoTrack.Id, videoTrack.Texture);
    }

    private void RegisterTrackCallback(int displayIndex, VideoStreamTrack track)
    {
        UnregisterTrackCallback(track);

        Unity.WebRTC.OnVideoReceived callback = texture => ApplyReceivedTexture(displayIndex, track.Id, texture);
        _trackCallbacks[track.Id] = callback;
        track.OnVideoReceived += callback;
    }

    private void UnregisterTrackCallback(VideoStreamTrack track)
    {
        if (track == null)
            return;

        if (_trackCallbacks.TryGetValue(track.Id, out var callback))
        {
            track.OnVideoReceived -= callback;
            _trackCallbacks.Remove(track.Id);
        }
    }

    private void ApplyReceivedTexture(int displayIndex, string trackId, Texture texture)
    {
        if (_isReceivingPaused)
            return;

        if (displayIndex < 0 || displayIndex >= _receivedTextures.Length)
            return;

        if (_receivedTrackIds[displayIndex] != trackId)
            return;

        if (ReferenceEquals(_receivedTextures[displayIndex], texture))
            return;

        _receivedTextures[displayIndex] = texture;

        var outputs = _manager.OutputImages;
        if (outputs != null && displayIndex < outputs.Length && outputs[displayIndex] != null && !ReferenceEquals(outputs[displayIndex].texture, texture))
            outputs[displayIndex].texture = texture;
    }

    private void EnsureCapacity(int count)
    {
        count = Mathf.Max(count, 0);

        if (_displayTracks.Length < count)
            Array.Resize(ref _displayTracks, count);

        if (_receivedTextures.Length < count)
            Array.Resize(ref _receivedTextures, count);

        if (_receivedTrackIds.Length < count)
            Array.Resize(ref _receivedTrackIds, count);

        if (_receivedMids.Length < count)
            Array.Resize(ref _receivedMids, count);

        _manager.SetInspectionArrays(_receivedTextures, _receivedTrackIds, _receivedMids);
    }

    private void ShrinkCapacity(int count)
    {
        count = Mathf.Max(0, count);

        if (_displayTracks.Length > count)
            Array.Resize(ref _displayTracks, count);

        if (_receivedTextures.Length > count)
            Array.Resize(ref _receivedTextures, count);

        if (_receivedTrackIds.Length > count)
            Array.Resize(ref _receivedTrackIds, count);

        if (_receivedMids.Length > count)
            Array.Resize(ref _receivedMids, count);

        _manager.SetInspectionArrays(_receivedTextures, _receivedTrackIds, _receivedMids);
    }

    private void ClearDisplaySlot(int displayIndex)
    {
        if (displayIndex < 0 || displayIndex >= _displayTracks.Length)
            return;

        var track = _displayTracks[displayIndex];
        UnregisterTrackCallback(track);
        SetTrackEnabled(track, false);
        DisposeTrackSafe(track);

        _displayTracks[displayIndex] = null;
        _receivedTextures[displayIndex] = null;
        _receivedTrackIds[displayIndex] = null;
        _receivedMids[displayIndex] = null;

        var outputs = _manager.OutputImages;
        if (outputs != null && displayIndex < outputs.Length && outputs[displayIndex] != null && outputs[displayIndex].texture != null)
            outputs[displayIndex].texture = null;
    }

    private void DisposePendingTracks()
    {
        foreach (var pending in _tracksWaitingForMap.Values)
            DisposeTrackSafe(pending.Track);

        _tracksWaitingForMap.Clear();
    }

    private static void DisposeTrackSafe(MediaStreamTrack track)
    {
        if (track == null)
            return;

        try
        {
            track.Stop();
        }
        catch
        {
            // Track may already be detached.
        }

        try
        {
            track.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private IEnumerator SetRemoteDescriptionAndSendAnswer(RTCSessionDescription description)
    {
        if (_peer == null)
            yield break;

        var setRemoteOperation = _peer.SetRemoteDescription(ref description);
        yield return setRemoteOperation;

        if (setRemoteOperation.IsError)
        {
            Debug.LogError($"SetRemoteDescription error: {setRemoteOperation.Error.message}");
            yield break;
        }

        var createAnswerOperation = _peer.CreateAnswer();
        yield return createAnswerOperation;

        if (createAnswerOperation.IsError)
        {
            Debug.LogError($"CreateAnswer error: {createAnswerOperation.Error.message}");
            yield break;
        }

        var answer = createAnswerOperation.Desc;
        var setLocalOperation = _peer.SetLocalDescription(ref answer);
        yield return setLocalOperation;

        if (setLocalOperation.IsError)
        {
            Debug.LogError($"SetLocalDescription error: {setLocalOperation.Error.message}");
            yield break;
        }

        EnqueueSignalingMessage(SignalingMessageTypes.Description, JsonUtility.ToJson(answer));
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

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"Receiver IceConnectionState: {state}");

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

            await _manager.RestartReceivingAsync();
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

    private void SetAllReceiverTracksEnabled(bool enabled)
    {
        if (_peer == null)
            return;

        foreach (var transceiver in _peer.GetTransceivers())
            SetTrackEnabled(transceiver?.Receiver?.Track, enabled);
    }

    private static void SetTrackEnabled(MediaStreamTrack track, bool enabled)
    {
        if (track == null)
            return;

        try
        {
            track.Enabled = enabled;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void ResetSessionCancellation()
    {
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();
    }

    private void ShutdownConnection()
    {
        _sessionCts?.Cancel();
        Interlocked.Exchange(ref _autoRestartRequested, 0);

        while (_outgoingMessages.TryDequeue(out _))
        {
        }

        DisposePendingTracks();
        _routeTable.Clear();
        _inboundStatsSnapshots.Clear();

        for (int i = 0; i < _displayTracks.Length; i++)
            ClearDisplaySlot(i);

        ShrinkCapacity(0);
        _lastTrackMapRevision = 0;

        _signalingClient?.Dispose();
        _signalingClient = null;

        if (_peer != null)
        {
            _peer.OnIceCandidate = null;
            _peer.OnIceConnectionChange = null;
            _peer.OnTrack = null;
            _peer.Close();
            _peer.Dispose();
            _peer = null;
        }

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
    }
}
