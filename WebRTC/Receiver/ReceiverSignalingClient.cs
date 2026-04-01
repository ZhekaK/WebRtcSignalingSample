using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

internal sealed class ReceiverSignalingClient : IDisposable
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

    public event Action<RTCIceCandidateInit> CandidateReceived;
    public event Action<RTCSessionDescription> DescriptionReceived;
    public event Action<string> TrackMapReceived;
    public event Action<MediaServerHelloMessage> HelloReceived;
    public event Action<MediaCatalogMessage> CatalogReceived;
    public event Action<MediaSubscriptionAck> SubscribeAckReceived;
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

    public async Task ConnectAsync(ReceiverTransportMode mode, string ip, int port, CancellationToken token)
    {
        if (_isDisposed)
            return;

        if (mode == ReceiverTransportMode.MediaServer)
        {
            _client = new VibeClient(ContentCodecDIProvider.Default, MemoryStreamManager);
            await _client.ConnectAsync(ip, port);
            Debug.Log($"[Receiver] Connected to MediaServer {ip}:{port}");
            return;
        }

        using var listener = new VibeListener(System.Net.IPAddress.Any, port, ContentCodecDIProvider.Default, MemoryStreamManager);
        listener.Start();
        _client = await listener.AcceptFlexClientAsync();
        Debug.Log($"[Receiver] Direct peer accepted on port {port}");
    }

    public void StartLoops(CancellationToken token)
    {
        if (_isDisposed || _client == null)
            return;

        _sendLoopTask = Task.Run(() => SendSignalingLoopAsync(token), token);
        _receiveLoopTask = Task.Run(() => ReceiveSignalingLoopAsync(token), token);
    }

    public bool SendSubscriptionRequest(MediaSubscriptionRequest request)
    {
        if (request == null || !IsConnected)
            return false;

        EnqueueSignalingMessage(MediaServerMessageTypes.Subscribe, JsonUtility.ToJson(request));
        return true;
    }

    public void SendDescription(RTCSessionDescription description)
    {
        EnqueueSignalingMessage(SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    public void SendCandidate(RTCIceCandidateInit candidate)
    {
        EnqueueSignalingMessage(SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidate));
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
                Debug.LogException(ex);
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

                switch (type)
                {
                    case SignalingMessageTypes.Candidate:
                        CandidateReceived?.Invoke(JsonUtility.FromJson<RTCIceCandidateInit>(json));
                        break;

                    case SignalingMessageTypes.Description:
                        DescriptionReceived?.Invoke(JsonUtility.FromJson<RTCSessionDescription>(json));
                        break;

                    case SignalingMessageTypes.TrackMap:
                        TrackMapReceived?.Invoke(json);
                        break;

                    case MediaServerMessageTypes.Hello:
                        HelloReceived?.Invoke(JsonUtility.FromJson<MediaServerHelloMessage>(json));
                        break;

                    case MediaServerMessageTypes.Catalog:
                        CatalogReceived?.Invoke(JsonUtility.FromJson<MediaCatalogMessage>(json));
                        break;

                    case MediaServerMessageTypes.SubscribeAck:
                        SubscribeAckReceived?.Invoke(JsonUtility.FromJson<MediaSubscriptionAck>(json));
                        break;

                    default:
                        Debug.LogWarning($"[Receiver] Unknown signaling type '{type}'");
                        break;
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

    private void EnqueueSignalingMessage(string type, string payload)
    {
        if (_isDisposed || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(payload))
            return;

        _outgoingMessages.Enqueue(new SignalingEnvelope(type, payload));

        try
        {
            _outgoingSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
