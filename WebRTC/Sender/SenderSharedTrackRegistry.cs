using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

internal sealed class SenderSharedTrackRegistry : IDisposable
{
    internal readonly struct TrackLease
    {
        public readonly string Key;
        public readonly VideoStreamTrack Track;
        public readonly bool IsShared;

        public TrackLease(string key, VideoStreamTrack track, bool isShared)
        {
            Key = key ?? string.Empty;
            Track = track;
            IsShared = isShared;
        }

        public bool IsValid => Track != null;
    }

    private sealed class SharedTrackEntry
    {
        public string Key = string.Empty;
        public VideoStreamTrack Track;
        public int RefCount;
    }

    private readonly Dictionary<string, SharedTrackEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private bool _isDisposed;

    public TrackLease Acquire(string sourceId, RenderTexture sourceTexture, bool allowShared)
    {
        if (_isDisposed || sourceTexture == null)
            return default;

        if (!allowShared)
            return new TrackLease(string.Empty, new VideoStreamTrack(sourceTexture), false);

        string key = BuildKey(sourceId, sourceTexture);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out SharedTrackEntry entry))
            {
                entry = new SharedTrackEntry
                {
                    Key = key,
                    Track = new VideoStreamTrack(sourceTexture),
                    RefCount = 0,
                };
                _entries[key] = entry;
            }

            entry.RefCount++;
            return new TrackLease(entry.Key, entry.Track, true);
        }
    }

    public void Release(TrackLease lease)
    {
        if (!lease.IsValid)
            return;

        if (!lease.IsShared)
        {
            DisposeTrackSafe(lease.Track);
            return;
        }

        lock (_lock)
        {
            if (!_entries.TryGetValue(lease.Key, out SharedTrackEntry entry))
                return;

            entry.RefCount = Math.Max(0, entry.RefCount - 1);
            if (entry.RefCount > 0)
                return;

            _entries.Remove(lease.Key);
            DisposeTrackSafe(entry.Track);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        lock (_lock)
        {
            foreach (SharedTrackEntry entry in _entries.Values)
                DisposeTrackSafe(entry.Track);

            _entries.Clear();
        }
    }

    public static string BuildKey(string sourceId, RenderTexture sourceTexture)
    {
        if (sourceTexture == null)
            return sourceId ?? string.Empty;

        return $"{sourceId ?? string.Empty}::{sourceTexture.GetInstanceID()}";
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
