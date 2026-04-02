using FlexNet.Server;
using FlexNet.Server.Controllers;
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

internal sealed class ReceiverSignalingClient : IDisposable
{
    private readonly FlexRouteClient _transport = new();

    private string _clientId = string.Empty;
    private string _senderClientId = string.Empty;
    private long _lastCatalogRevision = -1;
    private bool _isDisposed;

    public event Action<RTCIceCandidateInit> CandidateReceived;
    public event Action<RTCSessionDescription> DescriptionReceived;
    public event Action<string> TrackMapReceived;
    public event Action<SenderHelloMessage> HelloReceived;
    public event Action<MediaCatalogMessage> CatalogReceived;
    public event Action<MediaSubscriptionAck> SubscribeAckReceived;
    public event Action<Exception> ConnectionLost;

    public bool IsConnected => !_isDisposed && _transport.IsConnected && !string.IsNullOrWhiteSpace(_clientId);

    public async Task ConnectAsync(string ip, int port, string clientName, CancellationToken token)
    {
        if (_isDisposed)
            return;

        await _transport.ConnectAsync(ip, port, token);
        var (header, response) = await _transport.SendAsync<FlexSignalingRegisterRequest, FlexSignalingRegisterResponse>(
            FlexNetSignalingRouteIds.RegisterClient,
            new FlexSignalingRegisterRequest
            {
                clientName = clientName ?? Application.productName,
                role = SignalingClientRole.Receiver,
            },
            token);

        if (header == null || header.ResponseCode != ResponseCode.Ok || response == null)
            throw new InvalidOperationException(header?.Message ?? "receiver-register-failed");

        _clientId = response.clientId ?? string.Empty;
        ProcessServerState(response.senderClientId, response.catalog);
        Debug.Log($"[Receiver] Connected to signaling server {ip}:{port}");
    }

    public void StartLoops(CancellationToken token)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(_clientId))
            return;

        _ = Task.Run(() => PollLoopAsync(token), token);
    }

    public bool SendSubscriptionRequest(MediaSubscriptionRequest request)
    {
        if (request == null || !IsConnected)
            return false;

        _ = SendToSenderAsync(MediaRelayMessageTypes.Subscribe, JsonUtility.ToJson(request));
        return true;
    }

    public void SendDescription(RTCSessionDescription description)
    {
        _ = SendToSenderAsync(SignalingMessageTypes.Description, JsonUtility.ToJson(description));
    }

    public void SendCandidate(RTCIceCandidateInit candidate)
    {
        _ = SendToSenderAsync(SignalingMessageTypes.Candidate, JsonUtility.ToJson(candidate));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        string clientId = _clientId;
        _clientId = string.Empty;
        _senderClientId = string.Empty;
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

    private async Task SendToSenderAsync(string type, string payload)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(type))
            return;

        try
        {
            ResponseHeader header = await _transport.SendAsync(
                FlexNetSignalingRouteIds.SendFromReceiver,
                new FlexSignalingReceiverMessageRequest
                {
                    receiverClientId = _clientId,
                    type = type,
                    payload = payload ?? string.Empty,
                },
                CancellationToken.None);

            if (!_isDisposed && header?.ResponseCode != ResponseCode.Ok)
                Debug.LogWarning($"[Receiver] Failed to send '{type}': {header?.Message}");
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
                    throw new InvalidOperationException(header?.Message ?? "receiver-poll-failed");

                await Awaitable.MainThreadAsync();
                if (token.IsCancellationRequested || _isDisposed)
                    break;

                ProcessServerState(response?.senderClientId, response?.catalog);

                foreach (FlexSignalingEnvelope envelope in response?.messages ?? Array.Empty<FlexSignalingEnvelope>())
                {
                    if (envelope == null)
                        continue;

                    switch (envelope.type)
                    {
                        case SignalingMessageTypes.Candidate:
                            CandidateReceived?.Invoke(JsonUtility.FromJson<RTCIceCandidateInit>(envelope.payload));
                            break;

                        case SignalingMessageTypes.Description:
                            DescriptionReceived?.Invoke(JsonUtility.FromJson<RTCSessionDescription>(envelope.payload));
                            break;

                        case SignalingMessageTypes.TrackMap:
                            TrackMapReceived?.Invoke(envelope.payload);
                            break;

                        case MediaRelayMessageTypes.SubscribeAck:
                            SubscribeAckReceived?.Invoke(JsonUtility.FromJson<MediaSubscriptionAck>(envelope.payload));
                            break;

                        default:
                            Debug.LogWarning($"[Receiver] Unknown signaling type '{envelope.type}'");
                            break;
                    }
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

    private void ProcessServerState(string senderClientId, MediaCatalogMessage catalog)
    {
        if (!string.Equals(_senderClientId, senderClientId ?? string.Empty, StringComparison.Ordinal))
        {
            _senderClientId = senderClientId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(_senderClientId))
            {
                HelloReceived?.Invoke(new SenderHelloMessage
                {
                    sessionId = _senderClientId,
                    serverName = "FlexNetSignalingServer",
                    defaultTrackCount = catalog?.sources?.Length ?? 0,
                });
            }
        }

        if (catalog != null && catalog.revision != _lastCatalogRevision)
        {
            _lastCatalogRevision = catalog.revision;
            CatalogReceived?.Invoke(catalog);
        }
    }
}
