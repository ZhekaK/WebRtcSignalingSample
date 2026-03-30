using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

internal sealed class WebRtcMediaServerTrackService
{
    private sealed class ServerSlot
    {
        public int ClientSlotIndex;
        public int ClientMonitorIndex;
        public int ClientPanelIndex;
        public int ServerDisplayIndex;
        public string SourceId;
        public RenderLayer RenderLayer;
        public string SourceName;
        public RenderTexture SourceTexture;
        public VideoStreamTrack Track;
        public RTCRtpSender Sender;
        public RTCRtpTransceiver Transceiver;
    }

    private readonly SenderManager _manager;
    private readonly Dictionary<int, ServerSlot> _slots = new();
    private long _trackMapRevision;

    public bool HasTracks => _slots.Count > 0;

    public WebRtcMediaServerTrackService(SenderManager manager)
    {
        _manager = manager;
    }

    public bool SyncTracksWithSources(
        RTCPeerConnection peer,
        IReadOnlyList<MediaServerSourceCatalog.ResolvedSource> sources,
        bool isTransmissionPaused,
        out bool anyChanged,
        out bool topologyChanged)
    {
        anyChanged = false;
        topologyChanged = false;

        if (peer == null)
            return false;

        var desiredSlotIndices = new HashSet<int>();
        var resolvedSources = sources ?? Array.Empty<MediaServerSourceCatalog.ResolvedSource>();

        foreach (var source in resolvedSources)
        {
            if (source.ClientSlotIndex < 0 || source.SourceTexture == null)
                continue;

            desiredSlotIndices.Add(source.ClientSlotIndex);

            if (_slots.TryGetValue(source.ClientSlotIndex, out var slot))
            {
                if (ReplaceSlotSource(peer, slot, source, isTransmissionPaused))
                    anyChanged = true;
            }
            else
            {
                if (AddSlot(peer, source, isTransmissionPaused))
                {
                    anyChanged = true;
                    topologyChanged = true;
                }
            }
        }

        var staleSlotIndices = _slots.Keys.Where(slotIndex => !desiredSlotIndices.Contains(slotIndex)).ToArray();
        foreach (int staleSlotIndex in staleSlotIndices)
        {
            if (RemoveSlot(peer, staleSlotIndex))
            {
                anyChanged = true;
                topologyChanged = true;
            }
        }

        return HasTracks;
    }

    public void ApplyPreferredVideoCodecs(RTCPeerConnection peer)
    {
        if (peer == null)
            return;

        var allCodecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
        if (allCodecs == null || allCodecs.Length == 0)
            return;

        string preferredMimeType = GetPreferredVideoCodecMimeType();
        RTCRtpCodecCapability[] codecPreferences = allCodecs;

        if (!string.IsNullOrEmpty(preferredMimeType))
        {
            codecPreferences = allCodecs
                .Where(codec => string.Equals(codec.mimeType, preferredMimeType, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (codecPreferences.Length == 0)
            {
                Debug.LogWarning($"Preferred codec {preferredMimeType} is not available. Using default codec order.");
                codecPreferences = allCodecs;
            }
        }

        foreach (var transceiver in peer.GetTransceivers())
        {
            if (transceiver == null)
                continue;

            var error = transceiver.SetCodecPreferences(codecPreferences);
            if (error != RTCErrorType.None)
                Debug.LogError($"SetCodecPreferences failed: {error}");
        }
    }

    public void ApplyTransmissionStateToAllSenders(bool isTransmissionPaused)
    {
        foreach (var slot in _slots.Values)
            SetTrackEnabled(slot.Track, !isTransmissionPaused);

        ApplySenderParametersAll(isTransmissionPaused);
    }

    public string BuildTrackMapJson(RTCPeerConnection peer)
    {
        var orderedSlots = _slots.Values.OrderBy(slot => slot.ClientSlotIndex);
        var entries = new List<TrackMapEntry>(_slots.Count);

        foreach (var slot in orderedSlots)
        {
            if (peer != null && slot.Transceiver == null)
                slot.Transceiver = FindTransceiverBySender(peer, slot.Sender);

            entries.Add(new TrackMapEntry
            {
                displayIndex = slot.ClientSlotIndex,
                trackId = slot.Track?.Id ?? string.Empty,
                transceiverMid = slot.Transceiver?.Mid ?? string.Empty,
                sourceName = slot.SourceName ?? string.Empty,
                sourceId = slot.SourceId ?? string.Empty,
                serverDisplayIndex = slot.ServerDisplayIndex,
                renderLayer = slot.RenderLayer.ToString(),
                clientMonitorIndex = slot.ClientMonitorIndex,
                clientPanelIndex = slot.ClientPanelIndex
            });
        }

        return JsonUtility.ToJson(new TrackMapMessage
        {
            activeTrackCount = entries.Count,
            revision = Interlocked.Increment(ref _trackMapRevision),
            tracks = entries.ToArray()
        });
    }

    public void Shutdown(RTCPeerConnection peer)
    {
        foreach (var slot in _slots.Values)
        {
            try
            {
                if (peer != null && slot.Sender != null)
                    peer.RemoveTrack(slot.Sender);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            DisposeTrackSafe(slot.Track);
            slot.Track = null;
            slot.Sender = null;
            slot.Transceiver = null;
            slot.SourceTexture = null;
        }

        _slots.Clear();
        _trackMapRevision = 0;
    }

    private bool AddSlot(RTCPeerConnection peer, MediaServerSourceCatalog.ResolvedSource source, bool isTransmissionPaused)
    {
        if (peer == null || source.SourceTexture == null)
            return false;

        var track = new VideoStreamTrack(source.SourceTexture);
        SetTrackEnabled(track, !isTransmissionPaused);

        RTCRtpSender sender;
        try
        {
            sender = peer.AddTrack(track);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            DisposeTrackSafe(track);
            return false;
        }

        _slots[source.ClientSlotIndex] = new ServerSlot
        {
            ClientSlotIndex = source.ClientSlotIndex,
            ClientMonitorIndex = source.ClientMonitorIndex,
            ClientPanelIndex = source.ClientPanelIndex,
            ServerDisplayIndex = source.ServerDisplayIndex,
            SourceId = source.SourceId,
            RenderLayer = source.RenderLayer,
            SourceName = source.SourceName,
            SourceTexture = source.SourceTexture,
            Track = track,
            Sender = sender,
            Transceiver = FindTransceiverBySender(peer, sender)
        };

        Debug.Log($"[MediaServer] Track added for client slot {source.ClientSlotIndex}: {track.Id} ({source.SourceName})");
        return true;
    }

    private bool ReplaceSlotSource(
        RTCPeerConnection peer,
        ServerSlot slot,
        MediaServerSourceCatalog.ResolvedSource source,
        bool isTransmissionPaused)
    {
        if (slot == null || source.SourceTexture == null)
            return false;

        bool metadataChanged =
            slot.ClientMonitorIndex != source.ClientMonitorIndex ||
            slot.ClientPanelIndex != source.ClientPanelIndex ||
            slot.ServerDisplayIndex != source.ServerDisplayIndex ||
            !string.Equals(slot.SourceId, source.SourceId, StringComparison.Ordinal) ||
            slot.RenderLayer != source.RenderLayer ||
            !string.Equals(slot.SourceName, source.SourceName, StringComparison.Ordinal);

        if (string.Equals(slot.SourceId, source.SourceId, StringComparison.Ordinal) &&
            ReferenceEquals(slot.SourceTexture, source.SourceTexture) &&
            slot.Track != null)
        {
            slot.ClientMonitorIndex = source.ClientMonitorIndex;
            slot.ClientPanelIndex = source.ClientPanelIndex;
            slot.ServerDisplayIndex = source.ServerDisplayIndex;
            slot.SourceId = source.SourceId;
            slot.RenderLayer = source.RenderLayer;
            slot.SourceName = source.SourceName;
            return metadataChanged;
        }

        var newTrack = new VideoStreamTrack(source.SourceTexture);
        SetTrackEnabled(newTrack, !isTransmissionPaused);

        if (slot.Sender != null)
        {
            bool replaced = slot.Sender.ReplaceTrack(newTrack);
            if (!replaced)
            {
                Debug.LogError($"ReplaceTrack failed for client slot {slot.ClientSlotIndex}");
                DisposeTrackSafe(newTrack);
                return false;
            }
        }

        var oldTrack = slot.Track;
        slot.SourceTexture = source.SourceTexture;
        slot.Track = newTrack;
        slot.ClientMonitorIndex = source.ClientMonitorIndex;
        slot.ClientPanelIndex = source.ClientPanelIndex;
        slot.ServerDisplayIndex = source.ServerDisplayIndex;
        slot.SourceId = source.SourceId;
        slot.RenderLayer = source.RenderLayer;
        slot.SourceName = source.SourceName;
        slot.Transceiver ??= FindTransceiverBySender(peer, slot.Sender);

        DisposeTrackSafe(oldTrack);
        return true;
    }

    private bool RemoveSlot(RTCPeerConnection peer, int clientSlotIndex)
    {
        if (!_slots.TryGetValue(clientSlotIndex, out var slot))
            return false;

        try
        {
            if (peer != null && slot.Sender != null)
            {
                RTCErrorType error = peer.RemoveTrack(slot.Sender);
                if (error != RTCErrorType.None)
                    Debug.LogError($"RemoveTrack failed for client slot {clientSlotIndex}: {error}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        DisposeTrackSafe(slot.Track);
        _slots.Remove(clientSlotIndex);

        Debug.Log($"[MediaServer] Track removed for client slot {clientSlotIndex}");
        return true;
    }

    private void ApplySenderParametersAll(bool isTransmissionPaused)
    {
        if (_slots.Count == 0)
            return;

        int streamCount = Mathf.Max(1, _slots.Count);
        SenderBitratePlanner.BuildPerStreamPlan(
            _manager.TotalMaxBitrateMbps,
            _manager.TotalMinBitrateMbps,
            streamCount,
            out var perStreamMaxBitrate,
            out var perStreamMinBitrate);

        foreach (var slot in _slots.Values)
            ApplySenderParameters(slot.Sender, perStreamMaxBitrate, perStreamMinBitrate, isTransmissionPaused);
    }

    private void ApplySenderParameters(
        RTCRtpSender sender,
        ulong perStreamMaxBitrate,
        ulong perStreamMinBitrate,
        bool isTransmissionPaused)
    {
        if (sender == null)
            return;

        var parameters = sender.GetParameters();
        if (parameters.encodings == null)
            return;

        foreach (var encoding in parameters.encodings)
        {
            encoding.active = !isTransmissionPaused;
            encoding.scaleResolutionDownBy = 1.0;
            encoding.maxFramerate = _manager.UseMaxFps
                ? null
                : (uint)Mathf.Clamp(_manager.MaxFramerate, 1, 90);
            encoding.maxBitrate = perStreamMaxBitrate;
            encoding.minBitrate = Math.Min(perStreamMinBitrate, perStreamMaxBitrate);
        }

        var error = sender.SetParameters(parameters);
        if (error.errorType != RTCErrorType.None)
            Debug.LogError($"SetParameters failed: {error.message}");
    }

    private RTCRtpTransceiver FindTransceiverBySender(RTCPeerConnection peer, RTCRtpSender sender)
    {
        if (peer == null || sender == null)
            return null;

        foreach (var transceiver in peer.GetTransceivers())
        {
            if (ReferenceEquals(transceiver.Sender, sender))
                return transceiver;
        }

        return null;
    }

    private string GetPreferredVideoCodecMimeType()
    {
        switch (_manager.PreferredVideoCodec)
        {
            case SenderVideoCodecPreference.H264:
                return "video/H264";
            case SenderVideoCodecPreference.VP8:
                return "video/VP8";
            case SenderVideoCodecPreference.VP9:
                return "video/VP9";
            case SenderVideoCodecPreference.AV1:
                return "video/AV1";
            case SenderVideoCodecPreference.Auto:
            default:
                return null;
        }
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
}
