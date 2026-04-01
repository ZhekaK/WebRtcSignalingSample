using System.Threading.Tasks;
using UnityEngine;

public partial class SenderManager
{
    [ContextMenu("Pause Transmission")]
    public void PauseTransmission()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            if (!(_mediaServer?.PauseAllSessions() ?? false))
                Debug.LogWarning("[MediaServer] Pause requested, but there are no active client sessions.");

            return;
        }

        _session?.PauseTransmission();
    }

    [ContextMenu("Resume Transmission")]
    public void ResumeTransmission()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            if (!(_mediaServer?.ResumeAllSessions() ?? false))
                Debug.LogWarning("[MediaServer] Resume requested, but there are no active client sessions.");

            return;
        }

        _session?.ResumeTransmission();
    }

    [ContextMenu("Apply Encoder Settings Now")]
    public bool ApplyEncoderSettingsNow()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
            return _mediaServer != null && _mediaServer.ApplyEncoderSettingsNow();

        return _session != null && _session.ApplyEncoderSettingsNow();
    }

    [ContextMenu("Sync Tracks With Sources")]
    public bool SyncTracksWithSources()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
            return _mediaServer != null && _mediaServer.RefreshAllClientSubscriptions();

        return _session != null && _session.SyncTracksWithSources();
    }

    [ContextMenu("Test Add Track")]
    public void TestAddTrack()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            if (!(_mediaServer?.DebugAddTrackToFirstClient() ?? false))
                Debug.LogWarning("[MediaServer] Test Add Track failed: no connected client or no additional source is available.");

            return;
        }

        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
        {
            Debug.LogError("No source textures configured.");
            return;
        }

        if (!AddSourceTrack(SourceRenderTextures[SourceRenderTextures.Count - 1]))
            Debug.LogError("Add track failed");

        if (!SyncTracksWithSources())
            Debug.LogError("Sync track failed");
    }

    [ContextMenu("Test Remove Track")]
    public void TestRemoveTrack()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            if (!(_mediaServer?.DebugRemoveTrackFromFirstClient() ?? false))
                Debug.LogWarning("[MediaServer] Test Remove Track failed: no connected client or there is nothing to remove.");

            return;
        }

        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
        {
            Debug.LogError("No source textures configured.");
            return;
        }

        if (!RemoveSourceTrack(SourceRenderTextures.Count - 1))
            Debug.LogError("Remove track failed");

        if (!SyncTracksWithSources())
            Debug.LogError("Sync track failed");
    }

    [ContextMenu("Restart Transmission Clean")]
    public void RestartTransmission()
    {
        _ = RestartTransmissionAsync();
    }

    public async Task RestartTransmissionAsync()
    {
        if (_isRestartingTransmission)
            return;

        _isRestartingTransmission = true;
        try
        {
            if (RuntimeMode == SenderTransportMode.MediaServer)
            {
                _mediaServer ??= new WebRtcMediaServer(this);
                await _mediaServer.RestartAsync();
                return;
            }

            if (_session == null)
                _session = new SenderSession(this);

            await _session.RestartAsync();
        }
        finally
        {
            _isRestartingTransmission = false;
        }
    }

    public void ChangeDestinationIp(string newIp)
    {
        _ = ChangeDestinationEndpointAsync(newIp, Port);
    }

    public void ChangeDestinationEndpoint(string newIp, int newPort)
    {
        _ = ChangeDestinationEndpointAsync(newIp, newPort);
    }

    public async Task ChangeDestinationEndpointAsync(string newIp, int newPort)
    {
        if (_session == null)
            return;

        await _session.ChangeDestinationEndpointAsync(newIp, newPort);
    }
}
