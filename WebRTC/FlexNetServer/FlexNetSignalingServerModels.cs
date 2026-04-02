using System;

public enum SignalingClientRole
{
    Unknown = 0,
    Sender = 1,
    Receiver = 2,
}

[Serializable]
public sealed class FlexSignalingRegisterRequest
{
    public string clientName = string.Empty;
    public SignalingClientRole role = SignalingClientRole.Unknown;
}

[Serializable]
public sealed class FlexSignalingRegisterResponse
{
    public string clientId = string.Empty;
    public string senderClientId = string.Empty;
    public SignalingClientRole role = SignalingClientRole.Unknown;
    public MediaCatalogMessage catalog;
}

[Serializable]
public sealed class FlexSignalingUnregisterRequest
{
    public string clientId = string.Empty;
}

[Serializable]
public sealed class FlexSignalingPublishCatalogRequest
{
    public string senderClientId = string.Empty;
    public MediaCatalogMessage catalog;
}

[Serializable]
public sealed class FlexSignalingReceiverMessageRequest
{
    public string receiverClientId = string.Empty;
    public string type = string.Empty;
    public string payload = string.Empty;
}

[Serializable]
public sealed class FlexSignalingSenderMessageRequest
{
    public string senderClientId = string.Empty;
    public string receiverClientId = string.Empty;
    public string type = string.Empty;
    public string payload = string.Empty;
}

[Serializable]
public sealed class FlexSignalingPollRequest
{
    public string clientId = string.Empty;
    public int timeoutMs = 25000;
    public int maxMessages = 64;
}

[Serializable]
public sealed class FlexSignalingEnvelope
{
    public string fromClientId = string.Empty;
    public string toClientId = string.Empty;
    public string type = string.Empty;
    public string payload = string.Empty;
}

[Serializable]
public sealed class FlexSignalingPollResponse
{
    public string senderClientId = string.Empty;
    public MediaCatalogMessage catalog;
    public FlexSignalingEnvelope[] messages = Array.Empty<FlexSignalingEnvelope>();
}
