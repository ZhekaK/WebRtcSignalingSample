using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public partial class SenderManager
{
    private void StartConnectionLoop()
    {
        StopConnectionLoop();
        _connectionLoopCts = new CancellationTokenSource();
        _ = EnsureConnectionLoopAsync(_connectionLoopCts.Token);
    }

    private void StopConnectionLoop()
    {
        if (_connectionLoopCts == null)
            return;

        _connectionLoopCts.Cancel();
        _connectionLoopCts.Dispose();
        _connectionLoopCts = null;
    }

    private async Task EnsureConnectionLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                _relayHub ??= new SenderRelayHub(this);

                if (!_relayHub.IsConnectionReady)
                    await _relayHub.StartAsync();

                await Task.Delay(Mathf.Max(100, ReconnectDelayMs), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    internal void EnsureWebRtcUpdateLoop()
    {
        if (_webRtcUpdateCoroutine != null)
            return;

        _webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());
    }

    private void StopWebRtcUpdateLoop()
    {
        if (_webRtcUpdateCoroutine == null)
            return;

        StopCoroutine(_webRtcUpdateCoroutine);
        _webRtcUpdateCoroutine = null;
    }
}
