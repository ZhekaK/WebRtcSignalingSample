using System.Threading.Tasks;
using UnityEngine;

public partial class SenderManager
{
    [ContextMenu("Pause Transmission")]
    public void PauseTransmission()
    {
        if (!(_relayHub?.PauseAllSessions() ?? false))
            Debug.LogWarning("[Sender] Pause requested, but there are no active receiver sessions.");
    }

    [ContextMenu("Resume Transmission")]
    public void ResumeTransmission()
    {
        if (!(_relayHub?.ResumeAllSessions() ?? false))
            Debug.LogWarning("[Sender] Resume requested, but there are no active receiver sessions.");
    }

    [ContextMenu("Apply Encoder Settings Now")]
    public bool ApplyEncoderSettingsNow()
    {
        bool applied = _relayHub != null && _relayHub.ApplyEncoderSettingsNow();
        if (!applied)
            Debug.LogWarning("[Sender] ApplyEncoderSettingsNow requested, but there are no active receiver sessions.");

        return applied;
    }

    [ContextMenu("Sync Tracks With Sources")]
    public bool SyncTracksWithSources()
    {
        bool synced = _relayHub != null && _relayHub.RefreshAllClientSubscriptions();
        if (!synced)
            Debug.LogWarning("[Sender] SyncTracksWithSources requested, but there are no active receiver sessions.");

        return synced;
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
            _relayHub ??= new SenderRelayHub(this);
            await _relayHub.RestartAsync();
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
        IP = newIp;
        Port = newPort;

        if (_relayHub != null)
            await _relayHub.RestartAsync();
    }
}
