using System;

/// <summary>
/// Initial greeting from the FlexNet media server to a newly connected WebRTC client.
/// </summary>
[Serializable]
public class MediaServerHelloMessage
{
    public string sessionId = string.Empty;
    public string serverName = string.Empty;
    public int defaultTrackCount = 0;
}

/// <summary>
/// One available publishable source in the media server catalog.
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
/// Snapshot of all currently available media sources on the server.
/// </summary>
[Serializable]
public class MediaCatalogMessage
{
    public long revision = 0;
    public MediaSourceDescriptor[] sources = Array.Empty<MediaSourceDescriptor>();
}

/// <summary>
/// One requested source binding from server-side source to a client-side output slot.
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
/// Client request that selects which server sources should be sent to this subscriber.
/// </summary>
[Serializable]
public class MediaSubscriptionRequest
{
    public string clientName = string.Empty;
    public bool useDefaultLayout = false;
    public MediaSubscriptionEntry[] subscriptions = Array.Empty<MediaSubscriptionEntry>();
}

/// <summary>
/// Server acknowledgment for a subscription request.
/// </summary>
[Serializable]
public class MediaSubscriptionAck
{
    public string sessionId = string.Empty;
    public int acceptedCount = 0;
    public string message = string.Empty;
}

/// <summary>
/// FlexNet signaling message types used by the multi-client media server.
/// </summary>
public static class MediaServerMessageTypes
{
    public const string Hello = "media-hello";
    public const string Catalog = "media-catalog";
    public const string Subscribe = "media-subscribe";
    public const string SubscribeAck = "media-subscribe-ack";
}
