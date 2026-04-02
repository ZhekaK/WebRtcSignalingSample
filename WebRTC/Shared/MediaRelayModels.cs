using System;

/// <summary>
/// Greeting that informs a receiver about the currently available sender.
/// </summary>
[Serializable]
public class SenderHelloMessage
{
    public string sessionId = string.Empty;
    public string serverName = string.Empty;
    public int defaultTrackCount = 0;
}

/// <summary>
/// One available publishable source in the sender catalog.
/// </summary>
[Serializable]
public class MediaSourceDescriptor
{
    public string sourceId = string.Empty;
    public int serverDisplayIndex = -1;
    public string renderLayer = string.Empty;
    public string sourceName = string.Empty;
    public int width = 0;
    public int height = 0;
    public bool isDefaultLayer = false;
}

/// <summary>
/// Snapshot of all currently available media sources on the sender.
/// </summary>
[Serializable]
public class MediaCatalogMessage
{
    public long revision = 0;
    public MediaSourceDescriptor[] sources = Array.Empty<MediaSourceDescriptor>();
}

/// <summary>
/// One requested source binding from sender-side source to a client-side output slot.
/// </summary>
[Serializable]
public class MediaSubscriptionEntry
{
    public string sourceId = string.Empty;
    public int clientSlotIndex = -1;
    public int clientMonitorIndex = -1;
    public int clientPanelIndex = -1;
    public string note = string.Empty;
}

/// <summary>
/// Receiver request that selects which sender sources should be sent to this subscriber.
/// </summary>
[Serializable]
public class MediaSubscriptionRequest
{
    public string clientName = string.Empty;
    public bool useDefaultLayout = false;
    public bool allowEmptySelection = false;
    public MediaSubscriptionEntry[] subscriptions = Array.Empty<MediaSubscriptionEntry>();
}

/// <summary>
/// Sender acknowledgment for a subscription request.
/// </summary>
[Serializable]
public class MediaSubscriptionAck
{
    public string sessionId = string.Empty;
    public int acceptedCount = 0;
    public string message = string.Empty;
}

/// <summary>
/// FlexNet message types exchanged between sender and receiver through the signaling server.
/// </summary>
public static class MediaRelayMessageTypes
{
    public const string Subscribe = "media-subscribe";
    public const string SubscribeAck = "media-subscribe-ack";
}

public static class SignalingServerMessageTypes
{
    public const string PeerRemoved = "signaling-peer-removed";
}
