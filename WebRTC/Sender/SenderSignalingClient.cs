using FlexNet.Server;
using FlexNet.Server.Controllers;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class SenderSignalingClient : IDisposable
{
    private readonly FlexRouteClient _transport = new();

    private string _clientId = string.Empty;
    private bool _isDisposed;

    public string ClientId => _clientId;
    public bool IsConnected => !_isDisposed && _transport.IsConnected && !string.IsNullOrWhiteSpace(_clientId);

    public event Action<FlexSignalingEnvelope> MessageReceived;
    public event Action<Exception> ConnectionLost;

    public async Task ConnectAsync(string ip, int port, string clientName, CancellationToken token)
    {
        if (_isDisposed)
            return;

        await _transport.ConnectAsync(ip, port, token);
        var (header, response) = await _transport.SendAsync<FlexSignalingRegisterRequest, FlexSignalingRegisterResponse>(
            FlexNetSignalingRouteIds.RegisterClient,
            new FlexSignalingRegisterRequest
            {
                clientName = clientName,
                role = SignalingClientRole.Sender,
            },
            token);

        if (header == null || header.ResponseCode != ResponseCode.Ok || response == null)
            throw new InvalidOperationException(header?.Message ?? "sender-register-failed");

        _clientId = response.clientId ?? string.Empty;
    }

    public void StartPolling(CancellationToken token)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(_clientId))
            return;

        _ = Task.Run(() => PollLoopAsync(token), token);
    }

    public async Task<bool> PublishCatalogAsync(MediaCatalogMessage catalog, CancellationToken token)
    {
        if (!IsConnected)
            return false;

        ResponseHeader header = await _transport.SendAsync(
            FlexNetSignalingRouteIds.PublishCatalog,
            new FlexSignalingPublishCatalogRequest
            {
                senderClientId = _clientId,
                catalog = catalog,
            },
            token);

        return header?.ResponseCode == ResponseCode.Ok;
    }

    public void SendToReceiver(string receiverClientId, string type, string payload)
    {
        _ = SendToReceiverAsync(receiverClientId, type, payload);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        string clientId = _clientId;
        _clientId = string.Empty;
        _ = BestEffortUnregisterAndDisposeAsync(clientId);
    }

    private async Task BestEffortUnregisterAndDisposeAsync(string clientId)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(clientId) && _transport.IsConnected)
            {
                using CancellationTokenSource timeoutCts = new(500);
                await _transport.SendAsync(
                    FlexNetSignalingRouteIds.UnregisterClient,
                    new FlexSignalingUnregisterRequest { clientId = clientId },
                    timeoutCts.Token);
            }
        }
        catch
        {
        }
        finally
        {
            _transport.Dispose();
        }
    }

    private async Task SendToReceiverAsync(string receiverClientId, string type, string payload)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(receiverClientId) || string.IsNullOrWhiteSpace(type))
            return;

        try
        {
            await _transport.SendAsync(
                FlexNetSignalingRouteIds.SendFromSender,
                new FlexSignalingSenderMessageRequest
                {
                    senderClientId = _clientId,
                    receiverClientId = receiverClientId,
                    type = type,
                    payload = payload ?? string.Empty,
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
                ConnectionLost?.Invoke(ex);
        }
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        Exception disconnectException = null;

        try
        {
            while (!token.IsCancellationRequested && !_isDisposed)
            {
                var (header, response) = await _transport.PollAsync<FlexSignalingPollRequest, FlexSignalingPollResponse>(
                    FlexNetSignalingRouteIds.PollMessages,
                    new FlexSignalingPollRequest
                    {
                        clientId = _clientId,
                    },
                    token);

                if (token.IsCancellationRequested || _isDisposed)
                    break;

                if (header == null || header.ResponseCode != ResponseCode.Ok)
                    throw new InvalidOperationException(header?.Message ?? "sender-poll-failed");

                await Awaitable.MainThreadAsync();
                if (token.IsCancellationRequested || _isDisposed)
                    break;

                foreach (FlexSignalingEnvelope envelope in response?.messages ?? Array.Empty<FlexSignalingEnvelope>())
                {
                    if (envelope != null)
                        MessageReceived?.Invoke(envelope);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            disconnectException = ex;
        }
        finally
        {
            if (!_isDisposed)
            {
                await Awaitable.MainThreadAsync();
                if (!_isDisposed)
                    ConnectionLost?.Invoke(disconnectException);
            }
        }
    }
}
