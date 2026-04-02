using FlexNet;
using FlexNet.Extensions;
using FlexNet.Server;
using FlexNet.Server.Controllers;

internal static class FlexNetSignalingContextExtensions
{
    public static T ReadBody<T>(this FlexContext context)
    {
        context.RequestBody.GetContent(out T request);
        return request;
    }

    public static void WriteResponse(this FlexContext context, ResponseCode code, string message)
    {
        context.AddContent(new ResponseHeader
        {
            ResponseCode = code,
            Message = message ?? string.Empty,
        });
    }

    public static void WriteOk(this FlexContext context, string message)
    {
        context.WriteResponse(ResponseCode.Ok, message);
    }

    public static void WriteOk<T>(this FlexContext context, string message, T payload)
    {
        context.WriteOk(message);
        context.AddContent(payload);
    }

    public static void WriteNotFound(this FlexContext context, string message)
    {
        context.WriteResponse(ResponseCode.NotFound, message);
    }
}
