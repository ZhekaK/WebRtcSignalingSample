using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

internal sealed class SenderTrackService
{
    private sealed class SenderSlot
    {
        public int DisplayIndex;
        public RenderTexture SourceTexture;
        public VideoStreamTrack Track;
        public RTCRtpSender Sender;
        public RTCRtpTransceiver Transceiver;
    }

    private readonly SenderManager _manager;
    private readonly Dictionary<int, SenderSlot> _slots = new();
    private long _trackMapRevision;

    public bool HasTracks => _slots.Count > 0;

    public SenderTrackService(SenderManager manager)
    {
        _manager = manager;
    }

    public bool AddOrReplaceSourceTrack(
        RTCPeerConnection peer,
        int displayIndex,
        RenderTexture sourceTexture,
        bool isTransmissionPaused,
        out bool topologyChanged)
    {
        topologyChanged = false;

        if (displayIndex < 0 || sourceTexture == null || peer == null)
            return false;

        bool changed;
        if (_slots.TryGetValue(displayIndex, out var slot))
        {
            changed = ReplaceSlotTrackSource(peer, slot, sourceTexture, isTransmissionPaused);
        }
        else
        {
            changed = AddSlot(peer, displayIndex, sourceTexture, isTransmissionPaused);
            topologyChanged = changed;
        }

        return changed;
    }

    public bool RemoveSourceTrack(RTCPeerConnection peer, int displayIndex, out bool topologyChanged)
    {
        topologyChanged = false;
        if (displayIndex < 0 || peer == null)
            return false;

        bool removed = RemoveSlot(peer, displayIndex);
        if (!removed)
            return false;

        topologyChanged = true;
        return true;
    }

    public bool SyncTracksWithSources(
        RTCPeerConnection peer,
        List<RenderTexture> sources,
        bool isTransmissionPaused,
        out bool anyChanged,
        out bool topologyChanged)
    {
        anyChanged = false;
        topologyChanged = false;

        if (peer == null)
            return false;

        var resolvedSources = sources ?? new List<RenderTexture>();
        var desiredIndices = new HashSet<int>();

        for (int i = 0; i < resolvedSources.Count; i++)
        {
            var source = resolvedSources[i];
            if (source == null)
                continue;

            desiredIndices.Add(i);

            if (_slots.TryGetValue(i, out var slot))
            {
                if (!ReferenceEquals(slot.SourceTexture, source))
                    anyChanged |= ReplaceSlotTrackSource(peer, slot, source, isTransmissionPaused);
            }
            else
            {
                if (AddSlot(peer, i, source, isTransmissionPaused))
                {
                    anyChanged = true;
                    topologyChanged = true;
                }
            }
        }

        var staleIndices = _slots.Keys.Where(index => !desiredIndices.Contains(index)).ToArray();
        foreach (int staleIndex in staleIndices)
        {
            if (RemoveSlot(peer, staleIndex))
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
        if (peer == null)
            return string.Empty;

        var orderedSlots = _slots.Values.OrderBy(slot => slot.DisplayIndex);
        var entries = new List<TrackMapEntry>(_slots.Count);

        foreach (var slot in orderedSlots)
        {
            if (slot.Transceiver == null)
                slot.Transceiver = FindTransceiverBySender(peer, slot.Sender);

            entries.Add(new TrackMapEntry
            {
                displayIndex = slot.DisplayIndex,
                trackId = slot.Track?.Id ?? string.Empty,
                transceiverMid = slot.Transceiver?.Mid ?? string.Empty,
                sourceName = slot.SourceTexture != null ? slot.SourceTexture.name : string.Empty
            });
        }

        return JsonUtility.ToJson(new TrackMapMessage
        {
            activeTrackCount = entries.Count,
            revision = Interlocked.Increment(ref _trackMapRevision),
            tracks = entries.ToArray()
        });
    }

    public bool ResizeRenderTexture(RenderTexture texture, int width, int height)
    {
        if (texture == null || width <= 0 || height <= 0)
            return false;

        if (texture.width == width && texture.height == height)
            return true;

        texture.Release();
        texture.width = width;
        texture.height = height;
        texture.Create();

        return texture.IsCreated();
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

    private bool AddSlot(RTCPeerConnection peer, int displayIndex, RenderTexture sourceTexture, bool isTransmissionPaused)
    {
        if (peer == null || sourceTexture == null)
            return false;

        var track = new VideoStreamTrack(sourceTexture);
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

        _slots[displayIndex] = new SenderSlot
        {
            DisplayIndex = displayIndex,
            SourceTexture = sourceTexture,
            Track = track,
            Sender = sender,
            Transceiver = FindTransceiverBySender(peer, sender)
        };

        Debug.Log($"Sender track added for display {displayIndex + 1}: {track.Id}");
        return true;
    }

    private bool ReplaceSlotTrackSource(
        RTCPeerConnection peer,
        SenderSlot slot,
        RenderTexture sourceTexture,
        bool isTransmissionPaused)
    {
        if (slot == null || sourceTexture == null)
            return false;

        if (ReferenceEquals(slot.SourceTexture, sourceTexture) && slot.Track != null)
            return false;

        var newTrack = new VideoStreamTrack(sourceTexture);
        SetTrackEnabled(newTrack, !isTransmissionPaused);

        if (slot.Sender != null)
        {
            bool replaced = slot.Sender.ReplaceTrack(newTrack);
            if (!replaced)
            {
                Debug.LogError($"ReplaceTrack failed for display {slot.DisplayIndex + 1}");
                DisposeTrackSafe(newTrack);
                return false;
            }
        }

        var oldTrack = slot.Track;
        slot.Track = newTrack;
        slot.SourceTexture = sourceTexture;
        slot.Transceiver ??= FindTransceiverBySender(peer, slot.Sender);
        DisposeTrackSafe(oldTrack);

        return true;
    }

    private bool RemoveSlot(RTCPeerConnection peer, int displayIndex)
    {
        if (!_slots.TryGetValue(displayIndex, out var slot))
            return false;

        try
        {
            if (peer != null && slot.Sender != null)
            {
                RTCErrorType error = peer.RemoveTrack(slot.Sender);
                if (error != RTCErrorType.None)
                    Debug.LogError($"RemoveTrack failed for display {displayIndex + 1}: {error}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        DisposeTrackSafe(slot.Track);
        slot.Track = null;
        slot.SourceTexture = null;
        slot.Sender = null;
        slot.Transceiver = null;
        _slots.Remove(displayIndex);

        Debug.Log($"Sender track removed for display {displayIndex + 1}");
        return true;
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
}
