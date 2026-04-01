using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

internal sealed class ReceiverPeerController : IDisposable
{
    private readonly ReceiverManager _manager;
    private readonly Action<RTCTrackEvent> _trackReceived;
    private readonly Action<RTCIceCandidateInit> _candidateReady;
    private readonly Action<RTCSessionDescription> _descriptionReady;
    private readonly Queue<RTCSessionDescription> _pendingRemoteDescriptions = new();

    private RTCPeerConnection _peer;
    private DelegateOnIceConnectionChange _onIceConnectionChange;
    private DelegateOnIceCandidate _onIceCandidate;
    private DelegateOnTrack _onTrack;
    private bool _isDisposed;
    private bool _isRemoteDescriptionProcessing;

    public bool HasPeer => _peer != null;

    public ReceiverPeerController(
        ReceiverManager manager,
        Action<RTCTrackEvent> trackReceived,
        Action<RTCIceCandidateInit> candidateReady,
        Action<RTCSessionDescription> descriptionReady)
    {
        _manager = manager;
        _trackReceived = trackReceived;
        _candidateReady = candidateReady;
        _descriptionReady = descriptionReady;
    }

    public void CreatePeer()
    {
        if (_isDisposed || _peer != null)
            return;

        var config = new RTCConfiguration
        {
            iceServers = Array.Empty<RTCIceServer>()
        };

        _peer = new RTCPeerConnection(ref config);
        _onIceConnectionChange = OnIceConnectionChange;
        _onIceCandidate = OnIceCandidate;
        _onTrack = OnTrack;

        _peer.OnIceCandidate = _onIceCandidate;
        _peer.OnIceConnectionChange = _onIceConnectionChange;
        _peer.OnTrack = _onTrack;
    }

    public void AddIceCandidate(RTCIceCandidateInit candidate)
    {
        if (_peer == null)
            return;

        _peer.AddIceCandidate(new RTCIceCandidate(candidate));
    }

    public void EnqueueRemoteDescription(RTCSessionDescription description)
    {
        var peer = _peer;
        if (!IsCurrentPeer(peer))
            return;

        _pendingRemoteDescriptions.Enqueue(description);
        if (_isRemoteDescriptionProcessing)
            return;

        _manager.StartCoroutine(ProcessRemoteDescriptions(peer));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _pendingRemoteDescriptions.Clear();
        _isRemoteDescriptionProcessing = false;

        var peer = _peer;
        _peer = null;

        if (peer != null)
        {
            peer.OnIceCandidate = null;
            peer.OnIceConnectionChange = null;
            peer.OnTrack = null;
            peer.Close();
            peer.Dispose();
        }
    }

    private IEnumerator ProcessRemoteDescriptions(RTCPeerConnection peer)
    {
        if (!IsCurrentPeer(peer) || _isRemoteDescriptionProcessing)
            yield break;

        _isRemoteDescriptionProcessing = true;

        try
        {
            while (IsCurrentPeer(peer))
            {
                if (_pendingRemoteDescriptions.Count == 0)
                    yield break;

                RTCSessionDescription description = _pendingRemoteDescriptions.Dequeue();
                yield return SetRemoteDescriptionAndSendAnswer(peer, description);
            }
        }
        finally
        {
            _isRemoteDescriptionProcessing = false;

            if (_pendingRemoteDescriptions.Count > 0 && IsCurrentPeer(peer))
                _manager.StartCoroutine(ProcessRemoteDescriptions(peer));
        }
    }

    private IEnumerator SetRemoteDescriptionAndSendAnswer(RTCPeerConnection peer, RTCSessionDescription description)
    {
        if (!IsCurrentPeer(peer))
            yield break;

        RTCSetSessionDescriptionAsyncOperation setRemote;
        try
        {
            setRemote = peer.SetRemoteDescription(ref description);
        }
        catch (ObjectDisposedException)
        {
            yield break;
        }

        yield return setRemote;

        if (!IsCurrentPeer(peer))
            yield break;

        if (setRemote.IsError)
        {
            Debug.LogError($"[Receiver] SetRemoteDescription error: {setRemote.Error.message}");
            yield break;
        }

        if (description.type != RTCSdpType.Offer)
            yield break;

        var answerOp = peer.CreateAnswer();
        yield return answerOp;

        if (!IsCurrentPeer(peer))
            yield break;

        if (answerOp.IsError)
        {
            Debug.LogError($"[Receiver] CreateAnswer error: {answerOp.Error.message}");
            yield break;
        }

        var answer = answerOp.Desc;
        var setLocal = peer.SetLocalDescription(ref answer);
        yield return setLocal;

        if (!IsCurrentPeer(peer))
            yield break;

        if (setLocal.IsError)
        {
            Debug.LogError($"[Receiver] SetLocalDescription error: {setLocal.Error.message}");
            yield break;
        }

        _descriptionReady?.Invoke(answer);
    }

    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        var candidateInfo = new RTCIceCandidateInit
        {
            candidate = candidate.Candidate,
            sdpMid = candidate.SdpMid,
            sdpMLineIndex = candidate.SdpMLineIndex
        };

        _candidateReady?.Invoke(candidateInfo);
    }

    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        Debug.Log($"[Receiver] IceConnectionState: {state}");
    }

    private void OnTrack(RTCTrackEvent trackEvent)
    {
        _trackReceived?.Invoke(trackEvent);
    }

    private bool IsCurrentPeer(RTCPeerConnection peer)
    {
        return !_isDisposed && peer != null && ReferenceEquals(peer, _peer);
    }
}
