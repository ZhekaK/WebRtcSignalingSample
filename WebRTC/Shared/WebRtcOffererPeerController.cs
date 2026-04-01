using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;

internal sealed class WebRtcOffererPeerController : IDisposable
{
    private readonly MonoBehaviour _coroutineHost;
    private readonly Func<RTCPeerConnection, string> _trackMapJsonFactory;
    private readonly Action<string> _trackMapReady;
    private readonly Action<RTCSessionDescription> _descriptionReady;
    private readonly Action<RTCIceCandidateInit> _candidateReady;
    private readonly Action<RTCIceConnectionState> _iceStateChanged;
    private readonly string _logPrefix;

    private RTCPeerConnection _peer;
    private DelegateOnIceConnectionChange _onIceConnectionChange;
    private DelegateOnIceCandidate _onIceCandidate;
    private DelegateOnNegotiationNeeded _onNegotiationNeeded;
    private bool _isDisposed;
    private bool _isNegotiationInProgress;
    private bool _hasPendingNegotiation;

    public bool HasPeer => _peer != null;
    public RTCPeerConnection Peer => _peer;

    public WebRtcOffererPeerController(
        MonoBehaviour coroutineHost,
        Func<RTCPeerConnection, string> trackMapJsonFactory,
        Action<string> trackMapReady,
        Action<RTCSessionDescription> descriptionReady,
        Action<RTCIceCandidateInit> candidateReady,
        Action<RTCIceConnectionState> iceStateChanged,
        string logPrefix)
    {
        _coroutineHost = coroutineHost;
        _trackMapJsonFactory = trackMapJsonFactory;
        _trackMapReady = trackMapReady;
        _descriptionReady = descriptionReady;
        _candidateReady = candidateReady;
        _iceStateChanged = iceStateChanged;
        _logPrefix = logPrefix ?? "[WebRTC]";
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
        _onIceCandidate = OnIceCandidate;
        _onIceConnectionChange = OnIceConnectionChange;
        _onNegotiationNeeded = OnNegotiationNeeded;

        _peer.OnIceCandidate = _onIceCandidate;
        _peer.OnIceConnectionChange = _onIceConnectionChange;
        _peer.OnNegotiationNeeded = _onNegotiationNeeded;
    }

    public void AddIceCandidate(RTCIceCandidateInit candidate)
    {
        if (_peer == null)
            return;

        _peer.AddIceCandidate(new RTCIceCandidate(candidate));
    }

    public void SetRemoteDescription(RTCSessionDescription description)
    {
        var peer = _peer;
        if (!IsCurrentPeer(peer))
            return;

        _coroutineHost.StartCoroutine(SetRemoteDescriptionRoutine(peer, description));
    }

    public void RequestNegotiation()
    {
        var peer = _peer;
        if (!IsCurrentPeer(peer))
            return;

        _hasPendingNegotiation = true;
        if (_isNegotiationInProgress)
            return;

        _coroutineHost.StartCoroutine(PeerNegotiationNeeded(peer));
    }

    public RTCPeerConnection DetachPeer()
    {
        var peer = _peer;
        _peer = null;
        _hasPendingNegotiation = false;
        _isNegotiationInProgress = false;

        if (peer != null)
        {
            peer.OnIceCandidate = null;
            peer.OnIceConnectionChange = null;
            peer.OnNegotiationNeeded = null;
        }

        return peer;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        DisposePeer(DetachPeer());
    }

    public static void DisposePeer(RTCPeerConnection peer)
    {
        if (peer == null)
            return;

        peer.Close();
        peer.Dispose();
    }

    private IEnumerator SetRemoteDescriptionRoutine(RTCPeerConnection peer, RTCSessionDescription description)
    {
        if (!IsCurrentPeer(peer))
            yield break;

        RTCSetSessionDescriptionAsyncOperation operation;
        try
        {
            operation = peer.SetRemoteDescription(ref description);
        }
        catch (ObjectDisposedException)
        {
            yield break;
        }

        yield return operation;

        if (!IsCurrentPeer(peer))
            yield break;

        if (operation.IsError)
            Debug.LogError($"{_logPrefix} SetRemoteDescription error: {operation.Error.message}");
    }

    private IEnumerator PeerNegotiationNeeded(RTCPeerConnection peer)
    {
        if (!IsCurrentPeer(peer) || _isNegotiationInProgress)
            yield break;

        _isNegotiationInProgress = true;

        try
        {
            while (IsCurrentPeer(peer))
            {
                if (!_hasPendingNegotiation)
                    yield break;

                _hasPendingNegotiation = false;

                while (IsCurrentPeer(peer))
                {
                    RTCSignalingState signalingState;
                    try
                    {
                        signalingState = peer.SignalingState;
                    }
                    catch (ObjectDisposedException)
                    {
                        yield break;
                    }

                    if (signalingState == RTCSignalingState.Stable)
                        break;

                    yield return null;
                }

                if (!IsCurrentPeer(peer))
                    yield break;

                RTCSessionDescriptionAsyncOperation operation;
                try
                {
                    operation = peer.CreateOffer();
                }
                catch (ObjectDisposedException)
                {
                    yield break;
                }

                yield return operation;

                if (!IsCurrentPeer(peer))
                    yield break;

                if (operation.IsError)
                {
                    Debug.LogError($"{_logPrefix} CreateOffer error: {operation.Error.message}");
                    yield return null;
                    continue;
                }

                yield return _coroutineHost.StartCoroutine(SetLocalDescriptionAndPublish(peer, operation.Desc));
            }
        }
        finally
        {
            _isNegotiationInProgress = false;

            if (_hasPendingNegotiation && IsCurrentPeer(peer))
                _coroutineHost.StartCoroutine(PeerNegotiationNeeded(peer));
        }
    }

    private IEnumerator SetLocalDescriptionAndPublish(RTCPeerConnection peer, RTCSessionDescription description)
    {
        if (!IsCurrentPeer(peer))
            yield break;

        RTCSetSessionDescriptionAsyncOperation operation;
        try
        {
            operation = peer.SetLocalDescription(ref description);
        }
        catch (ObjectDisposedException)
        {
            yield break;
        }

        yield return operation;

        if (!IsCurrentPeer(peer))
            yield break;

        if (operation.IsError)
        {
            Debug.LogError($"{_logPrefix} SetLocalDescription error: {operation.Error.message}");
            yield break;
        }

        string trackMapJson = _trackMapJsonFactory?.Invoke(peer);
        if (!string.IsNullOrEmpty(trackMapJson))
            _trackMapReady?.Invoke(trackMapJson);

        _descriptionReady?.Invoke(description);
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
        _iceStateChanged?.Invoke(state);
    }

    private void OnNegotiationNeeded()
    {
        RequestNegotiation();
    }

    private bool IsCurrentPeer(RTCPeerConnection peer)
    {
        return !_isDisposed && peer != null && ReferenceEquals(peer, _peer);
    }
}
