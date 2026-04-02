using FlexNet.Attributes;
using FlexNet.ContentCodecs;
using FlexNet.Interfaces;
using System.Buffers;
using UnityEngine;

public abstract class FlexJsonContentCodec<TContent> : ContentCodec<TContent>
    where TContent : class, new()
{
    private readonly IContentCodec<string> _stringCodec;

    protected FlexJsonContentCodec(IContentCodec<string> stringCodec)
    {
        _stringCodec = stringCodec;
    }

    public override TContent Decode(ref SequenceReader<byte> reader)
    {
        string json = _stringCodec.Decode(ref reader);
        if (string.IsNullOrWhiteSpace(json))
            return new TContent();

        TContent value = JsonUtility.FromJson<TContent>(json);
        return value ?? new TContent();
    }

    public override int Encode(IBufferWriter<byte> writer, TContent value)
    {
        string json = JsonUtility.ToJson(value ?? new TContent());
        return _stringCodec.Encode(writer, json);
    }

    public override int GetSize(TContent value)
    {
        string json = JsonUtility.ToJson(value ?? new TContent());
        return _stringCodec.GetSize(json);
    }
}

[ContentCodec]
public sealed class SenderHelloMessageCodec : FlexJsonContentCodec<SenderHelloMessage>
{
    public SenderHelloMessageCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class MediaSourceDescriptorCodec : FlexJsonContentCodec<MediaSourceDescriptor>
{
    public MediaSourceDescriptorCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class MediaCatalogMessageCodec : FlexJsonContentCodec<MediaCatalogMessage>
{
    public MediaCatalogMessageCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class MediaSubscriptionEntryCodec : FlexJsonContentCodec<MediaSubscriptionEntry>
{
    public MediaSubscriptionEntryCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class MediaSubscriptionRequestCodec : FlexJsonContentCodec<MediaSubscriptionRequest>
{
    public MediaSubscriptionRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class MediaSubscriptionAckCodec : FlexJsonContentCodec<MediaSubscriptionAck>
{
    public MediaSubscriptionAckCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingRegisterRequestCodec : FlexJsonContentCodec<FlexSignalingRegisterRequest>
{
    public FlexSignalingRegisterRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingRegisterResponseCodec : FlexJsonContentCodec<FlexSignalingRegisterResponse>
{
    public FlexSignalingRegisterResponseCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingUnregisterRequestCodec : FlexJsonContentCodec<FlexSignalingUnregisterRequest>
{
    public FlexSignalingUnregisterRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingPublishCatalogRequestCodec : FlexJsonContentCodec<FlexSignalingPublishCatalogRequest>
{
    public FlexSignalingPublishCatalogRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingReceiverMessageRequestCodec : FlexJsonContentCodec<FlexSignalingReceiverMessageRequest>
{
    public FlexSignalingReceiverMessageRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingSenderMessageRequestCodec : FlexJsonContentCodec<FlexSignalingSenderMessageRequest>
{
    public FlexSignalingSenderMessageRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingPollRequestCodec : FlexJsonContentCodec<FlexSignalingPollRequest>
{
    public FlexSignalingPollRequestCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingEnvelopeCodec : FlexJsonContentCodec<FlexSignalingEnvelope>
{
    public FlexSignalingEnvelopeCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

[ContentCodec]
public sealed class FlexSignalingPollResponseCodec : FlexJsonContentCodec<FlexSignalingPollResponse>
{
    public FlexSignalingPollResponseCodec(IContentCodec<string> stringCodec) : base(stringCodec) { }
}

