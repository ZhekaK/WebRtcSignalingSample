using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class ReconnectableClient<T> : IDisposable, ISender<T>
{
    public IPEndPoint IPEndPoint { get; private set; }
    public IFlexClient Client { get; private set; }
    public bool IsConnected => CheckConnectStatus();

    protected CancellationTokenSource _cancellationTokenSource = new();
    protected TaskCompletionSource<bool> _connectionCompletionSource;

    protected bool _connecting = false;

    private static readonly RecyclableMemoryStreamManager _memoryManager = new(new RecyclableMemoryStreamManager.Options()
    {
        BlockSize = 512 * 1024
    });

    private bool CheckConnectStatus()
    {
        if (Client == null)
        {
            return false;
        }
        else
        {
            try
            {
                return Client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary> Подключить клиента </summary>
    public async Task ConnectAsync(IPEndPoint ipEndPoint)
    {
        if (ipEndPoint == IPEndPoint && _connecting) return;
        _connecting = true;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();
        Client?.Dispose();

        IPEndPoint = ipEndPoint;
        var client = new VibeClient(ContentCodecDIProvider.Default, _memoryManager);
        var token = _cancellationTokenSource.Token;

        Debug.Log($"[EvsServer {ipEndPoint.Port}] Подключение к: {ipEndPoint}");

        await Task.Delay(100);

        try
        {
            token.ThrowIfCancellationRequested();
            await client.ConnectAsync(ipEndPoint);
            token.ThrowIfCancellationRequested();
            Client = client;
            //Debug.Log($"<color=yellow>[EvsServer {_client.RemoteEndPoint}] IsConnected: {IsConnected}!</color>");

            _connectionCompletionSource?.SetResult(true);
            _connectionCompletionSource = null;
            Debug.Log($"<color=green>[EvsServer {ipEndPoint}] Подключился!</color>");
            _connecting = false;
        }
        catch (OperationCanceledException ex)
        {
            _connecting = false;
            return;
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
            {
                _connecting = false;
                return;
            }

            Debug.LogWarning($"[EvsServer {ipEndPoint.Port}] {ex}");
            _connecting = false;
            _ = Task.Run(() => ConnectAsync(ipEndPoint));
        }
    }

    /// <summary> Отключить и уничтожить клиента </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();

        Client?.Dispose();

        Debug.Log($"<color=red>[EvsServer {IPEndPoint}] Отключился!</color>");
    }

    protected void SendExceptionHandling(Exception ex)
    {
        if (!IsConnected)
        {
            _connectionCompletionSource = new TaskCompletionSource<bool>();

            _ = Task.Run(() => ConnectAsync(IPEndPoint));
        }
        Debug.LogWarning("ОШИБКА " + ex.Message);
    }

    public virtual async Task WaitToSendAsync(T message)
    {
        try
        {
            await SendAsync(message);
        }
        catch (Exception ex)
        {
            SendExceptionHandling(ex);

            if (!IsConnected && _connectionCompletionSource != null)
            {
                await _connectionCompletionSource.Task;
            }
            else if (!IsConnected)
            {
                // Попробуем отправить снова после небольшой задержки
                await Task.Delay(1000);
                await WaitToSendAsync(message);
            }
        }
    }

    protected abstract Task SendAsync(T message);
}
