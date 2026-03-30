using FlexNet;
using FlexNet.Interfaces;
using FlexNet.Vibe;
using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class WebRtcMediaServer : IDisposable
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

    private readonly SenderManager _manager;
    private readonly MediaServerSourceCatalog _catalog = new();
    private readonly Dictionary<string, WebRtcMediaServerClientSession> _sessions = new();
    private readonly object _sessionsLock = new();

    private CancellationTokenSource _serverCts;
    private VibeListener _listener;
    private Task _acceptLoopTask;
    private bool _isDisposed;
    private int _sessionCounter;

    public WebRtcMediaServer(SenderManager manager)
    {
        _manager = manager;
    }

    public async Task StartAsync()
    {
        if (_isDisposed || _listener != null)
            return;

        try
        {
            _serverCts = new CancellationTokenSource();
            _listener = new VibeListener(IPAddress.Any, _manager.Port, ContentCodecDIProvider.Default, MemoryStreamManager);
            _listener.Start();

            Debug.Log($"[MediaServer] FlexNet signaling server started on port {_manager.Port}.");
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_serverCts.Token), _serverCts.Token);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Dispose();
        }
    }

    public MediaCatalogMessage BuildCatalogMessage()
    {
        return _catalog.BuildCatalogMessage();
    }

    public MediaSubscriptionRequest BuildDefaultRequest()
    {
        return _catalog.BuildDefaultRequest();
    }

    public MediaServerSourceCatalog.ResolvedSource[] ResolveSubscription(MediaSubscriptionRequest request)
    {
        return _catalog.Resolve(request);
    }

    public void LogRequestedActions(
        string sessionId,
        string clientName,
        IReadOnlyList<MediaServerSourceCatalog.ResolvedSource> previous,
        IReadOnlyList<MediaServerSourceCatalog.ResolvedSource> next)
    {
        string sessionLabel = string.IsNullOrWhiteSpace(clientName)
            ? sessionId
            : $"{sessionId} ({clientName})";

        var previousItems = previous ?? Array.Empty<MediaServerSourceCatalog.ResolvedSource>();
        var nextItems = next ?? Array.Empty<MediaServerSourceCatalog.ResolvedSource>();
        var previousKeys = new HashSet<string>(previousItems.Select(BuildActionKey), StringComparer.Ordinal);
        var nextKeys = new HashSet<string>(nextItems.Select(BuildActionKey), StringComparer.Ordinal);

        foreach (var added in nextItems.Where(item => !previousKeys.Contains(BuildActionKey(item))))
        {
            Debug.Log(
                $"[MediaServer] Action ENABLE for {sessionLabel}: source={added.SourceName} " +
                $"sourceId={added.SourceId} serverDisplay={added.ServerDisplayIndex + 1} " +
                $"layer={added.RenderLayer} -> clientMonitor={added.ClientMonitorIndex + 1} panel={added.ClientPanelIndex} slot={added.ClientSlotIndex}");
        }

        foreach (var removed in previousItems.Where(item => !nextKeys.Contains(BuildActionKey(item))))
        {
            Debug.Log(
                $"[MediaServer] Action DISABLE for {sessionLabel}: source={removed.SourceName} " +
                $"sourceId={removed.SourceId} serverDisplay={removed.ServerDisplayIndex + 1} " +
                $"layer={removed.RenderLayer} -> clientMonitor={removed.ClientMonitorIndex + 1} panel={removed.ClientPanelIndex} slot={removed.ClientSlotIndex}");
        }
    }

    public void NotifySessionClosed(WebRtcMediaServerClientSession session)
    {
        if (session == null)
            return;

        lock (_sessionsLock)
        {
            _sessions.Remove(session.SessionId);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            _serverCts?.Cancel();
        }
        catch
        {
        }

        _listener?.Dispose();
        _listener = null;

        WebRtcMediaServerClientSession[] sessions;
        lock (_sessionsLock)
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }

        foreach (var session in sessions)
            session.Dispose();

        _serverCts?.Dispose();
        _serverCts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_isDisposed)
        {
            try
            {
                var listener = _listener;
                if (listener == null)
                    break;

                IFlexClient signalingClient = await listener.AcceptFlexClientAsync();
                if (token.IsCancellationRequested || _isDisposed)
                {
                    signalingClient?.Dispose();
                    break;
                }

                string sessionId = $"client-{Interlocked.Increment(ref _sessionCounter):0000}";
                var session = new WebRtcMediaServerClientSession(this, _manager, signalingClient, sessionId);

                lock (_sessionsLock)
                {
                    _sessions[sessionId] = session;
                }

                Debug.Log($"[MediaServer] Client connected: {sessionId}");
                _ = session.StartAsync();
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
            }
        }
    }

    private static string BuildActionKey(MediaServerSourceCatalog.ResolvedSource source)
    {
        return $"{source.SourceId}|{source.ClientSlotIndex}|{source.ClientMonitorIndex}|{source.ClientPanelIndex}";
    }
}
