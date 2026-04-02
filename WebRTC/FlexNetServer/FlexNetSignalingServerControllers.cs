using FlexNet.Server;
using FlexNet.Server.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

public sealed class RegisterClientController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public RegisterClientController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public Task HandleAsync(FlexContext context)
    {
        FlexSignalingRegisterRequest request = context.ReadBody<FlexSignalingRegisterRequest>();
        FlexSignalingRegisterResponse response = _signalingService.Register(request);
        context.WriteOk("client-registered", response);
        return Task.CompletedTask;
    }
}

public sealed class UnregisterClientController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public UnregisterClientController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public Task HandleAsync(FlexContext context)
    {
        FlexSignalingUnregisterRequest request = context.ReadBody<FlexSignalingUnregisterRequest>();
        if (_signalingService.Unregister(request.clientId))
            context.WriteOk("client-unregistered");
        else
            context.WriteNotFound("client-not-found");

        return Task.CompletedTask;
    }
}

public sealed class PublishCatalogController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public PublishCatalogController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public Task HandleAsync(FlexContext context)
    {
        FlexSignalingPublishCatalogRequest request = context.ReadBody<FlexSignalingPublishCatalogRequest>();
        if (_signalingService.PublishCatalog(request))
            context.WriteOk("catalog-published");
        else
            context.WriteNotFound("sender-not-found");

        return Task.CompletedTask;
    }
}

public sealed class SendFromReceiverController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public SendFromReceiverController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public Task HandleAsync(FlexContext context)
    {
        FlexSignalingReceiverMessageRequest request = context.ReadBody<FlexSignalingReceiverMessageRequest>();
        if (_signalingService.SendFromReceiver(request))
            context.WriteOk("message-relayed-to-sender");
        else
            context.WriteNotFound("sender-not-found");

        return Task.CompletedTask;
    }
}

public sealed class SendFromSenderController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public SendFromSenderController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public Task HandleAsync(FlexContext context)
    {
        FlexSignalingSenderMessageRequest request = context.ReadBody<FlexSignalingSenderMessageRequest>();
        if (_signalingService.SendFromSender(request))
            context.WriteOk("message-relayed-to-receiver");
        else
            context.WriteNotFound("receiver-not-found");

        return Task.CompletedTask;
    }
}

public sealed class PollMessagesController : IApiController
{
    private readonly IFlexSignalingService _signalingService;

    public PollMessagesController(IFlexSignalingService signalingService)
    {
        _signalingService = signalingService;
    }

    public async Task HandleAsync(FlexContext context)
    {
        FlexSignalingPollRequest request = context.ReadBody<FlexSignalingPollRequest>();
        FlexSignalingPollResponse response = await _signalingService.PollAsync(request, CancellationToken.None);
        if (response == null)
        {
            context.WriteNotFound("client-not-found");
            return;
        }

        context.WriteOk("messages-polled", response);
    }
}

public static class FlexSignalingControllerRegistration
{
    public static void AddSignalingControllers(FlexServer.Builder builder)
    {
        builder.Services.AddSingleton<IFlexSignalingService, FlexSignalingService>();

        builder.AddController<RegisterClientController>(FlexNetSignalingRouteIds.RegisterClient);
        builder.AddController<UnregisterClientController>(FlexNetSignalingRouteIds.UnregisterClient);
        builder.AddController<PublishCatalogController>(FlexNetSignalingRouteIds.PublishCatalog);
        builder.AddController<SendFromReceiverController>(FlexNetSignalingRouteIds.SendFromReceiver);
        builder.AddController<SendFromSenderController>(FlexNetSignalingRouteIds.SendFromSender);
        builder.AddController<PollMessagesController>(FlexNetSignalingRouteIds.PollMessages);
    }
}
