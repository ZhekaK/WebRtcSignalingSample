using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

internal sealed class SenderSubscriberTrackService
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
        public SenderSharedTrackRegistry.TrackLease TrackLease;
        public RTCRtpSender Sender;
        public RTCRtpTransceiver Transceiver;
    }

    private readonly SenderManager _manager;
    private readonly SenderSharedTrackRegistry _sharedTrackRegistry;
    private readonly Dictionary<int, ServerSlot> _slots = new();
    private long _trackMapRevision;

    public bool HasTracks => _slots.Count > 0;

    public SenderSubscriberTrackService(SenderManager manager, SenderSharedTrackRegistry sharedTrackRegistry)
    {
        _manager = manager;
        _sharedTrackRegistry = sharedTrackRegistry;
    }

    public bool SyncTracksWithSources(
        RTCPeerConnection peer,
        IReadOnlyList<SenderSourceCatalog.ResolvedSource> sources,
        bool isTransmissionPaused,
        out bool anyChanged,
        out bool topologyChanged)
    {
        anyChanged = false;
        topologyChanged = false;

        if (peer == null)
            return false;

        var desiredSlotIndices = new HashSet<int>();
        var resolvedSources = sources ?? Array.Empty<SenderSourceCatalog.ResolvedSource>();

        foreach (SenderSourceCatalog.ResolvedSource source in resolvedSources)
        {
            if (source.ClientSlotIndex < 0 || source.SourceTexture == null)
                continue;

            desiredSlotIndices.Add(source.ClientSlotIndex);

            if (_slots.TryGetValue(source.ClientSlotIndex, out ServerSlot slot))
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

        int[] staleSlotIndices = _slots.Keys.Where(slotIndex => !desiredSlotIndices.Contains(slotIndex)).ToArray();
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

        RTCRtpCodecCapability[] allCodecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
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

        foreach (RTCRtpTransceiver transceiver in peer.GetTransceivers())
        {
            if (transceiver == null)
                continue;

            RTCErrorType error = transceiver.SetCodecPreferences(codecPreferences);
            if (error != RTCErrorType.None)
                Debug.LogError($"SetCodecPreferences failed: {error}");
        }
    }

    public void ApplyTransmissionStateToAllSenders(bool isTransmissionPaused)
    {
        ApplySenderParametersAll(isTransmissionPaused);
    }

    public string BuildTrackMapJson(RTCPeerConnection peer)
    {
        IEnumerable<ServerSlot> orderedSlots = _slots.Values.OrderBy(slot => slot.ClientSlotIndex);
        List<TrackMapEntry> entries = new(_slots.Count);

        foreach (ServerSlot slot in orderedSlots)
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
        foreach (ServerSlot slot in _slots.Values)
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

            ReleaseTrack(slot.TrackLease);
            slot.Track = null;
            slot.Sender = null;
            slot.Transceiver = null;
            slot.SourceTexture = null;
            slot.TrackLease = default;
        }

        _slots.Clear();
        _trackMapRevision = 0;
    }

    private bool AddSlot(RTCPeerConnection peer, SenderSourceCatalog.ResolvedSource source, bool isTransmissionPaused)
    {
        if (peer == null || source.SourceTexture == null)
            return false;

        SenderSharedTrackRegistry.TrackLease lease = AcquireTrack(source, ignoreSlotIndex: -1);
        if (!lease.IsValid)
            return false;

        RTCRtpSender sender;
        try
        {
            sender = peer.AddTrack(lease.Track);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ReleaseTrack(lease);
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
            Track = lease.Track,
            TrackLease = lease,
            Sender = sender,
            Transceiver = FindTransceiverBySender(peer, sender)
        };

        ApplySenderParameters(sender, 0, 0, isTransmissionPaused, applyBitrateOnly: true);
        Debug.Log($"[Sender] Track added for client slot {source.ClientSlotIndex}: {lease.Track.Id} ({source.SourceName})");
        return true;
    }

    private bool ReplaceSlotSource(
        RTCPeerConnection peer,
        ServerSlot slot,
        SenderSourceCatalog.ResolvedSource source,
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

        SenderSharedTrackRegistry.TrackLease newLease = AcquireTrack(source, slot.ClientSlotIndex);
        if (!newLease.IsValid)
            return false;

        if (slot.Sender != null)
        {
            bool replaced = slot.Sender.ReplaceTrack(newLease.Track);
            if (!replaced)
            {
                Debug.LogError($"ReplaceTrack failed for client slot {slot.ClientSlotIndex}");
                ReleaseTrack(newLease);
                return false;
            }
        }

        SenderSharedTrackRegistry.TrackLease oldLease = slot.TrackLease;
        slot.SourceTexture = source.SourceTexture;
        slot.Track = newLease.Track;
        slot.TrackLease = newLease;
        slot.ClientMonitorIndex = source.ClientMonitorIndex;
        slot.ClientPanelIndex = source.ClientPanelIndex;
        slot.ServerDisplayIndex = source.ServerDisplayIndex;
        slot.SourceId = source.SourceId;
        slot.RenderLayer = source.RenderLayer;
        slot.SourceName = source.SourceName;
        slot.Transceiver ??= FindTransceiverBySender(peer, slot.Sender);

        ApplySenderParameters(slot.Sender, 0, 0, isTransmissionPaused, applyBitrateOnly: true);
        ReleaseTrack(oldLease);
        return true;
    }

    private bool RemoveSlot(RTCPeerConnection peer, int clientSlotIndex)
    {
        if (!_slots.TryGetValue(clientSlotIndex, out ServerSlot slot))
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

        ReleaseTrack(slot.TrackLease);
        _slots.Remove(clientSlotIndex);

        Debug.Log($"[Sender] Track removed for client slot {clientSlotIndex}");
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
            out ulong perStreamMaxBitrate,
            out ulong perStreamMinBitrate);

        foreach (ServerSlot slot in _slots.Values)
            ApplySenderParameters(slot.Sender, perStreamMaxBitrate, perStreamMinBitrate, isTransmissionPaused);
    }

    private void ApplySenderParameters(
        RTCRtpSender sender,
        ulong perStreamMaxBitrate,
        ulong perStreamMinBitrate,
        bool isTransmissionPaused,
        bool applyBitrateOnly = false)
    {
        if (sender == null)
            return;

        RTCRtpSendParameters parameters = sender.GetParameters();
        if (parameters.encodings == null)
            return;

        foreach (RTCRtpEncodingParameters encoding in parameters.encodings)
        {
            encoding.active = !isTransmissionPaused;
            encoding.scaleResolutionDownBy = 1.0;
            encoding.maxFramerate = _manager.UseMaxFps
                ? null
                : (uint)Mathf.Clamp(_manager.MaxFramerate, 1, 90);

            if (!applyBitrateOnly || perStreamMaxBitrate > 0)
                encoding.maxBitrate = perStreamMaxBitrate;
            if (!applyBitrateOnly || perStreamMaxBitrate > 0)
                encoding.minBitrate = Math.Min(perStreamMinBitrate, perStreamMaxBitrate);
        }

        RTCError error = sender.SetParameters(parameters);
        if (error.errorType != RTCErrorType.None)
            Debug.LogError($"SetParameters failed: {error.message}");
    }

    private RTCRtpTransceiver FindTransceiverBySender(RTCPeerConnection peer, RTCRtpSender sender)
    {
        if (peer == null || sender == null)
            return null;

        foreach (RTCRtpTransceiver transceiver in peer.GetTransceivers())
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

    private SenderSharedTrackRegistry.TrackLease AcquireTrack(SenderSourceCatalog.ResolvedSource source, int ignoreSlotIndex)
    {
        bool canUseSharedTrack = CanUseSharedTrackInThisPeer(source, ignoreSlotIndex);
        return _sharedTrackRegistry.Acquire(source.SourceId, source.SourceTexture, canUseSharedTrack);
    }

    private bool CanUseSharedTrackInThisPeer(SenderSourceCatalog.ResolvedSource source, int ignoreSlotIndex)
    {
        foreach (ServerSlot slot in _slots.Values)
        {
            if (slot.ClientSlotIndex == ignoreSlotIndex)
                continue;

            if (string.Equals(slot.SourceId, source.SourceId, StringComparison.Ordinal) &&
                ReferenceEquals(slot.SourceTexture, source.SourceTexture))
            {
                return false;
            }
        }

        return true;
    }

    private void ReleaseTrack(SenderSharedTrackRegistry.TrackLease lease)
    {
        _sharedTrackRegistry.Release(lease);
    }
}
