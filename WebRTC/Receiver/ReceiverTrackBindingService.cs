using System;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;

internal sealed class ReceiverTrackBindingService
{
    private sealed class PendingTrack
    {
        public string TrackId;
        public string Mid;
        public VideoStreamTrack Track;
    }

    private readonly ReceiverManager _manager;
    private readonly ReceiverRouteTable _routeTable = new();
    private readonly Dictionary<string, PendingTrack> _tracksWaitingForMap = new();
    private readonly Dictionary<string, Unity.WebRTC.OnVideoReceived> _trackCallbacks = new();

    private VideoStreamTrack[] _displayTracks = Array.Empty<VideoStreamTrack>();
    private Texture[] _receivedTextures = Array.Empty<Texture>();
    private string[] _receivedTrackIds = Array.Empty<string>();
    private string[] _receivedMids = Array.Empty<string>();
    private bool _isReceivingPaused;
    private long _lastTrackMapRevision;

    public ReceiverTrackBindingService(ReceiverManager manager)
    {
        _manager = manager;
        PrepareForConnection(Mathf.Max(_manager.OutputImages?.Length ?? 0, 0));
    }

    public void PrepareForConnection(int outputSlotCount)
    {
        EnsureCapacity(Mathf.Max(outputSlotCount, 0));
    }

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

        for (int i = 0; i < _receivedTextures.Length; i++)
            _receivedTextures[i] = null;

        _manager.ClearOutputImages();
    }

    public void ResumeReceiving()
    {
        _isReceivingPaused = false;

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

        foreach (var pending in _tracksWaitingForMap.Values)
            SetTrackEnabled(pending.Track, true);
    }

    public void ApplyTrackMap(string json)
    {
        var map = JsonUtility.FromJson<TrackMapMessage>(json);
        if (map == null)
        {
            Debug.LogWarning("[Receiver] Invalid tracks-map payload.");
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
            Debug.LogWarning("[Receiver] Invalid tracks-map received.");
            return;
        }

        EnsureCapacity(_routeTable.RequiredDisplaySlots);
        UnbindDisplaysNotInMap(_routeTable.GetMappedDisplayIndices());
        TryBindWaitingTracks();
        ShrinkCapacity(_routeTable.RequiredDisplaySlots);
    }

    public void HandleTrack(RTCTrackEvent trackEvent)
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

    public void Shutdown()
    {
        _routeTable.Clear();
        _lastTrackMapRevision = 0;

        foreach (var pendingTrack in _tracksWaitingForMap.Values)
        {
            UnregisterTrackCallback(pendingTrack.Track);
            SetTrackEnabled(pendingTrack.Track, false);
            DisposeTrackSafe(pendingTrack.Track);
        }

        _tracksWaitingForMap.Clear();

        for (int i = 0; i < _displayTracks.Length; i++)
            ClearDisplaySlot(i);

        _trackCallbacks.Clear();
        _manager.SetInspectionArrays(_receivedTextures, _receivedTrackIds, _receivedMids);
        _manager.ClearOutputImages();
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
        _manager.ApplyTexture(displayIndex, texture);
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

        _manager.ApplyTexture(displayIndex, null);
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
