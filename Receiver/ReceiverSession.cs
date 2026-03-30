using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public class ReceiverSession : IDisposable
{
    private readonly ReceiverManager _manager;

    private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();

    private IFlexClient _signalingClient;
    private RTCPeerConnection _peer;

    private CancellationTokenSource _cts = new();
    private ConcurrentQueue<(string, string)> _outgoing = new();
    private SemaphoreSlim _signal = new(0);

    public ReceiverSession(ReceiverManager manager)
    {
        _manager = manager;
    }

    public async Task InitializeAsync(bool forceReconnect = false)
    {
        if (forceReconnect)
            Shutdown();

        if (_manager.RuntimeMode == ReceiverTransportMode.MediaServer)
        {
            _signalingClient = new VibeClient(ContentCodecDIProvider.Default, _memoryStreamManager);
            await _signalingClient.ConnectAsync(_manager.IP, _manager.Port);

            Debug.Log($"[Receiver] Connected to MediaServer {_manager.IP}:{_manager.Port}");
        }
        else
        {
            var listener = new VibeListener(
                System.Net.IPAddress.Any,
                _manager.Port,
                ContentCodecDIProvider.Default,
                _memoryStreamManager);

            listener.Start();
            _signalingClient = await listener.AcceptFlexClientAsync();
            listener.Dispose();
        }

        var config = new RTCConfiguration
        {
            iceServers = Array.Empty<RTCIceServer>()
        };

        _peer = new RTCPeerConnection(ref config);

        _peer.OnIceCandidate = OnIceCandidate;
        _peer.OnTrack = OnTrack;

        _ = Task.Run(SendLoop);
        _ = Task.Run(ReceiveLoop);

        if (_manager.AutoRequestDefaultLayout)
            SendDefaultRequest();
    }

    public async Task RestartAsync()
    {
        await InitializeAsync(true);
    }

    public void SendDefaultRequest()
    {
        SendMediaSubscriptionRequest(new MediaSubscriptionRequest
        {
            clientName = "Receiver",
            useDefaultLayout = true,
            subscriptions = Array.Empty<MediaSubscriptionEntry>()
        });
    }

    public bool SendMediaSubscriptionRequest(MediaSubscriptionRequest request)
    {
        if (_signalingClient == null || !_signalingClient.Connected)
        {
            Debug.LogError("[Receiver] Not connected to signaling server");
            return false;
        }

        string json = JsonUtility.ToJson(request);
        _outgoing.Enqueue(("media-subscribe", json));
        _signal.Release();

        return true;
    }

    private async Task SendLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            await _signal.WaitAsync();

            while (_outgoing.TryDequeue(out var msg))
            {
                _signalingClient
                    .AddContent(msg.Item1)
                    .AddContent(msg.Item2)
                    .Send();
            }
        }
    }

    private async Task ReceiveLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            using var res = await _signalingClient.ReceiveAsync();

            res.GetContent(out string type).GetContent(out string json);

            await Awaitable.MainThreadAsync();

            switch (type)
            {
                case "candidate":
                    var cand = JsonUtility.FromJson<RTCIceCandidateInit>(json);
                    _peer.AddIceCandidate(new RTCIceCandidate(cand));
                    break;

                case "description":
                    var desc = JsonUtility.FromJson<RTCSessionDescription>(json);
                    _manager.StartCoroutine(SetRemote(desc));
                    break;

                case "media-hello":
                    Debug.Log("[Receiver] hello");
                    break;

                case "media-catalog":
                    Debug.Log("[Receiver] catalog received");
                    break;

                case "media-subscribe-ack":
                    Debug.Log("[Receiver] subscribe ack");
                    break;

                default:
                    Debug.Log($"[Receiver] unknown: {type}");
                    break;
            }
        }
    }

    private IEnumerator SetRemote(RTCSessionDescription desc)
    {
        var op = _peer.SetRemoteDescription(ref desc);
        yield return op;

        var answerOp = _peer.CreateAnswer();
        yield return answerOp;

        var answer = answerOp.Desc;

        var setLocal = _peer.SetLocalDescription(ref answer);
        yield return setLocal;

        Send("description", JsonUtility.ToJson(answer));
    }

    private void Send(string type, string payload)
    {
        _outgoing.Enqueue((type, payload));
        _signal.Release();
    }

    private void OnIceCandidate(RTCIceCandidate c)
    {
        Send("candidate", JsonUtility.ToJson(new RTCIceCandidateInit
        {
            candidate = c.Candidate,
            sdpMid = c.SdpMid,
            sdpMLineIndex = c.SdpMLineIndex
        }));
    }

    private void OnTrack(RTCTrackEvent e)
    {
        if (e.Track is VideoStreamTrack video)
        {
            video.OnVideoReceived += tex =>
            {
                _manager.ApplyTexture(0, tex);
            };
        }
    }

    private void Shutdown()
    {
        _cts.Cancel();

        _peer?.Close();
        _peer?.Dispose();
        _peer = null;

        _signalingClient?.Dispose();
        _signalingClient = null;
    }

    public void Dispose()
    {
        Shutdown();
    }
}
