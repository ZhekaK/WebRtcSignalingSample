using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class FlexSignalingChannel : IDisposable
{
    private readonly struct SignalingEnvelope
    {
        public readonly string Type;
        public readonly string Payload;

        public SignalingEnvelope(string type, string payload)
        {
            Type = type;
            Payload = payload;
        }
    }

    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    private readonly ConcurrentQueue<SignalingEnvelope> _outgoingMessages = new();
    private readonly SemaphoreSlim _outgoingSignal = new(0, int.MaxValue);

    private IFlexClient _client;
    private Task _sendLoopTask;
    private Task _receiveLoopTask;
    private bool _isDisposed;

    public event Action<string, string> MessageReceived;
    public event Action<Exception> ConnectionLost;

    public bool IsConnected
    {
        get
        {
            try
            {
                return _client != null && _client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    public FlexSignalingChannel()
    {
    }

    public FlexSignalingChannel(IFlexClient client)
    {
        _client = client;
    }

    public void AttachAcceptedClient(IFlexClient client)
    {
        if (_isDisposed)
        {
            client?.Dispose();
            return;
        }

        _client = client;
    }

    public async Task ConnectAsync(string ip, int port, CancellationToken token)
    {
        if (_isDisposed)
            return;

        _client = new VibeClient(ContentCodecDIProvider.Default, MemoryStreamManager);
        await _client.ConnectAsync(ip, port);
    }

    public void StartLoops(CancellationToken token)
    {
        if (_isDisposed || _client == null)
            return;

        _sendLoopTask = Task.Run(() => SendSignalingLoopAsync(token), token);
        _receiveLoopTask = Task.Run(() => ReceiveSignalingLoopAsync(token), token);
    }

    public bool Send(string type, string payload)
    {
        if (_isDisposed || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(payload))
            return false;

        _outgoingMessages.Enqueue(new SignalingEnvelope(type, payload));

        try
        {
            _outgoingSignal.Release();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        while (_outgoingMessages.TryDequeue(out _))
        {
        }

        _client?.Dispose();
        _client = null;

        try
        {
            _outgoingSignal.Dispose();
        }
        catch
        {
        }
    }

    private async Task SendSignalingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _outgoingSignal.WaitAsync(token);

                while (_outgoingMessages.TryDequeue(out var envelope))
                {
                    var client = _client;
                    if (client == null)
                        continue;

                    client.AddContent(envelope.Type).AddContent(envelope.Payload).Send();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                break;
            }
        }
    }

    private async Task ReceiveSignalingLoopAsync(CancellationToken token)
    {
        Exception disconnectException = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = _client;
                if (client == null)
                    break;

                using var results = await client.ReceiveAsync();
                if (token.IsCancellationRequested || _isDisposed)
                    break;

                results.GetContent(out string type).GetContent(out string json);
                await Awaitable.MainThreadAsync();

                if (token.IsCancellationRequested || _isDisposed)
                    break;

                MessageReceived?.Invoke(type, json);
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


