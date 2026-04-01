using FlexNet.Extensions;
using FlexNet.Server;
using FlexNet.Server.Controllers;
using FlexNet.Vibe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public class FlexNetMediaServer
{
    private RecyclableMemoryStreamManager _memoryStreamManager;

    private void Initialize()
    {
        _memoryStreamManager = new RecyclableMemoryStreamManager(
                new RecyclableMemoryStreamManager.Options()
                {
                    BlockSize = 128 * 1024,
                    LargeBufferMultiple = 1024 * 1024,
                    MaximumBufferSize = 16 * 1024 * 1024,
                    MaximumLargePoolFreeBytes = 64 * 1024 * 1024,
                    MaximumSmallPoolFreeBytes = 32 * 1024 * 1024,
                });

        var builder = new FlexServer.Builder();
        builder.Services.AddSingleton(new IPEndPoint(IPAddress.Loopback, 1611));
        builder.Services.AddSingleton(_memoryStreamManager);
        builder.UseListener<VibeListener>();
        builder.AddController<SendEndpointController>(routeId: 1);
        builder.AddController<ReceiveEndpointController>(routeId: 2);
        builder.Services.AddSingleton<IMediatorService, MediatorService>();
        builder.Services.AddContentCodecs();
        var server = builder.Build();
        server.Run();

    }
}

public class SendEndpointController : IApiController
{
    private IMediatorService _mediator;
    public SendEndpointController(IMediatorService mediatorService)
    {
        _mediator = mediatorService;
    }

    public Task HandleAsync(FlexContext context)
    {
        context.RequestBody.GetContent(out int clientId).GetContent(out int receiverId).GetContent(out string message);

        _mediator.AddMessage(message, receiverId, clientId);
        Console.WriteLine($"Клиент {clientId} отправил сообщение для клиента {receiverId}");
        context.AddContent(new ResponseHeader() { ResponseCode = ResponseCode.Created, Message = "Сообщение оставлено." });
        return Task.CompletedTask;
    }

}

public class ReceiveEndpointController : IApiController
{
    private IMediatorService _mediator;
    public ReceiveEndpointController(IMediatorService mediatorService)
    {
        _mediator = mediatorService;
    }

    public Task HandleAsync(FlexContext context)
    {
        context.RequestBody.GetContent(out int clientId);

        Console.WriteLine($"Клиент {clientId} запросил сообщение для себя.");
        var (message, senderId) = _mediator.GetMessage(clientId);
        context.AddContent(new ResponseHeader() { ResponseCode = ResponseCode.Ok, Message = $"Отправитель {senderId}: {message}" });
        return Task.CompletedTask;
    }

}

public interface IMediatorService
{
    void AddMessage(string message, int receiverId, int senderId);
    (string message, int senderId) GetMessage(int receiverId);
}

public class MediatorService : IMediatorService
{
    private readonly Dictionary<int, (string, int)> _messages;

    public void AddMessage(string message, int receiverId, int senderId)
    {
        _messages[receiverId] = (message, senderId);
    }

    public (string message, int senderId) GetMessage(int receiverId)
    {
        var success = false;
        (string message, int senderId) result;
        do
        {
            success = _messages.TryGetValue(receiverId, out result);
            Task.Delay(1000).Wait();
        } while (!success);

        return result;
    }
}
