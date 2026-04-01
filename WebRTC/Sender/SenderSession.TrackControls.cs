using System.Collections;
using System.Threading;
using UnityEngine;

public sealed partial class SenderSession
{
    public void PauseTransmission()
    {
        _isTransmissionPaused = true;
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
    }

    public void ResumeTransmission()
    {
        _isTransmissionPaused = false;
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);

        var peer = _peerController?.Peer;
        if (peer != null)
            SendLocalTrackMap(_trackService.BuildTrackMapJson(peer));
    }

    public bool ApplyEncoderSettingsNow()
    {
        var peer = _peerController?.Peer;
        if (peer == null || !_trackService.HasTracks)
            return false;

        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
        return true;
    }

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

    public bool AddOrReplaceSourceTrack(int displayIndex, RenderTexture sourceTexture)
    {
        if (displayIndex < 0 || sourceTexture == null)
            return false;

        var peer = _peerController?.Peer;
        if (peer == null)
            return false;

        bool changed = _trackService.AddOrReplaceSourceTrack(
            peer,
            displayIndex,
            sourceTexture,
            _isTransmissionPaused,
            out bool topologyChanged);

        if (!changed)
            return false;

        PublishTrackState(topologyChanged);
        return true;
    }

    public bool RemoveSourceTrack(int displayIndex)
    {
        var peer = _peerController?.Peer;
        if (displayIndex < 0 || peer == null)
            return false;

        bool removed = _trackService.RemoveSourceTrack(peer, displayIndex, out bool topologyChanged);
        if (!removed)
            return false;

        PublishTrackState(topologyChanged);
        return true;
    }

    public bool SyncTracksWithSources()
    {
        var peer = _peerController?.Peer;
        bool hasTracks = _trackService.SyncTracksWithSources(
            peer,
            _manager.SourceRenderTextures,
            _isTransmissionPaused,
            out bool anyChanged,
            out bool topologyChanged);

        if (anyChanged || topologyChanged)
            PublishTrackState(topologyChanged);

        return hasTracks;
    }

    private void PublishTrackState(bool topologyChanged)
    {
        var peer = _peerController?.Peer;
        if (peer == null)
            return;

        _trackService.ApplyPreferredVideoCodecs(peer);
        _trackService.ApplyTransmissionStateToAllSenders(_isTransmissionPaused);
        SendLocalTrackMap(_trackService.BuildTrackMapJson(peer));

        if (topologyChanged)
            _peerController.RequestNegotiation();
    }

    private IEnumerator LogSenderStatsLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_isDisposed)
        {
            int intervalSeconds = Mathf.Max(1, _manager.SenderStatsLogIntervalSec);
            var peer = _peerController?.Peer;

            if (_manager.EnableSenderStatsLogging && peer != null && (_signalingChannel?.IsConnected ?? false))
            {
                var op = peer.GetStats();
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
}
