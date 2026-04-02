using FlexNet;
using FlexNet.Extensions;
using FlexNet.Interfaces;
using FlexNet.Server;
using FlexNet.Server.Controllers;
using FlexNet.Vibe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

internal sealed class FlexRouteClient : IDisposable
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();
    private static readonly IContentCodecProvider CodecProvider = CreateCodecProvider();

    private readonly SemaphoreSlim _commandLock = new(1, 1);

    private VibeClient _commandClient;
    private VibeClient _pollClient;
    private bool _isDisposed;

    public bool IsConnected
    {
        get
        {
            try
            {
                return !_isDisposed &&
                       _commandClient != null && _commandClient.Connected &&
                       _pollClient != null && _pollClient.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task ConnectAsync(string ip, int port, CancellationToken token)
    {
        if (_isDisposed)
            return;

        DisposeClients();

        _commandClient = new VibeClient(CodecProvider, MemoryStreamManager);
        _pollClient = new VibeClient(CodecProvider, MemoryStreamManager);

        await _commandClient.ConnectAsync(ip, port);
        await _pollClient.ConnectAsync(ip, port);
    }

    public async Task<ResponseHeader> SendAsync<TRequest>(int routeId, TRequest request, CancellationToken token)
    {
        await _commandLock.WaitAsync(token);
        try
        {
            var result = await SendInternalAsync<TRequest, object>(_commandClient, routeId, request, expectPayload: false, token);
            return result.Header;
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task<(ResponseHeader Header, TResponse Payload)> SendAsync<TRequest, TResponse>(int routeId, TRequest request, CancellationToken token)
    {
        await _commandLock.WaitAsync(token);
        try
        {
            return await SendInternalAsync<TRequest, TResponse>(_commandClient, routeId, request, expectPayload: true, token);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public Task<(ResponseHeader Header, TResponse Payload)> PollAsync<TRequest, TResponse>(int routeId, TRequest request, CancellationToken token)
    {
        return SendInternalAsync<TRequest, TResponse>(_pollClient, routeId, request, expectPayload: true, token);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        DisposeClients();
        _commandLock.Dispose();
    }

    private async Task<(ResponseHeader Header, TResponse Payload)> SendInternalAsync<TRequest, TResponse>(
        VibeClient client,
        int routeId,
        TRequest request,
        bool expectPayload,
        CancellationToken token)
    {
        if (_isDisposed || client == null)
            return (new ResponseHeader { ResponseCode = ResponseCode.NotFound, Message = "client-disposed" }, default);

        await client
            .AddContent(new RequestHeader { RouteId = routeId })
            .AddContent(request)
            .SendAsync(token);

        using var result = await client.ReceiveAsync(token);
        if (token.IsCancellationRequested)
            return (new ResponseHeader { ResponseCode = ResponseCode.NotFound, Message = "operation-cancelled" }, default);

        result.GetContent(out ResponseHeader header).Dispose();
        if (!expectPayload)
            return (header, default);

        result.GetContent(out TResponse payload).Dispose();
        return (header, payload);
    }

    private void DisposeClients()
    {
        try
        {
            _commandClient?.Close();
        }
        catch
        {
        }

        try
        {
            _commandClient?.Dispose();
        }
        catch
        {
        }

        try
        {
            _pollClient?.Close();
        }
        catch
        {
        }

        try
        {
            _pollClient?.Dispose();
        }
        catch
        {
        }

        _commandClient = null;
        _pollClient = null;
    }

    private static IContentCodecProvider CreateCodecProvider()
    {
        ServiceCollection services = new();
        services.AddContentCodecs();
        return new ContentCodecDIProvider(services.BuildServiceProvider());
    }
}
