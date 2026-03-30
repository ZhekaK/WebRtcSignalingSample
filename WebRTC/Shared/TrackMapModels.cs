using System;

/// <summary>
/// Maps one sender display stream to a specific WebRTC track/transceiver.
/// </summary>
[Serializable]
public class TrackMapEntry
{
    public int displayIndex = -1;
    public string trackId = string.Empty;
    public string transceiverMid = string.Empty;
    public string sourceName = string.Empty;
    public string sourceId = string.Empty;
    public int serverDisplayIndex = -1;
    public string renderLayer = string.Empty;
    public int clientMonitorIndex = -1;
    public int clientPanelIndex = -1;
}

/// <summary>
/// Message payload that carries the full display-to-track mapping.
/// </summary>
[Serializable]
public class TrackMapMessage
{
    public int activeTrackCount = 0;
    public long revision = 0;
    public TrackMapEntry[] tracks = Array.Empty<TrackMapEntry>();
}

/// <summary>
/// Signaling message type constants used by sender and receiver.
/// </summary>
public static class SignalingMessageTypes
{
    public const string Candidate = "candidate";
    public const string Description = "description";
    public const string TrackMap = "tracks-map";
}
