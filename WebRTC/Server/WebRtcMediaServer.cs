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
    private readonly MediaServerActivationTable _activationTable = new();
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
            PublishActivationSnapshot();
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_serverCts.Token), _serverCts.Token);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Dispose();
        }
    }

    public async Task RestartAsync()
    {
        ShutdownCore();
        _isDisposed = false;
        await StartAsync();
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
        IReadOnlyList<MediaServerSourceCatalog.ResolvedSource> next)
    {
        string sessionLabel = string.IsNullOrWhiteSpace(clientName)
            ? sessionId
            : $"{sessionId} ({clientName})";

        foreach (var transition in _activationTable.ApplySessionSelection(sessionId, next))
        {
            string verb = transition.ChangeType == MediaServerActivationTable.ActivationChangeType.Activated
                ? "ENABLE"
                : "DISABLE";

            Debug.Log(
                $"[MediaServer] Action {verb} for {sessionLabel}: source={transition.SourceName} " +
                $"sourceId={transition.SourceId} serverDisplay={transition.ServerDisplayIndex + 1} " +
                $"layer={transition.RenderLayer} refs={transition.ReferenceCount}");
        }

        PublishActivationSnapshot();
    }

    public void NotifySessionClosed(WebRtcMediaServerClientSession session)
    {
        if (session == null)
            return;

        foreach (var transition in _activationTable.RemoveSession(session.SessionId))
        {
            Debug.Log(
                $"[MediaServer] Action DISABLE for {session.SessionId}: source={transition.SourceName} " +
                $"sourceId={transition.SourceId} serverDisplay={transition.ServerDisplayIndex + 1} " +
                $"layer={transition.RenderLayer} refs={transition.ReferenceCount}");
        }

        lock (_sessionsLock)
        {
            _sessions.Remove(session.SessionId);
        }

        PublishActivationSnapshot();
    }

    public bool PauseAllSessions()
    {
        int affected = 0;
        foreach (var session in SnapshotSessions())
        {
            session.PauseTransmission();
            affected++;
        }

        return affected > 0;
    }

    public bool ResumeAllSessions()
    {
        int affected = 0;
        foreach (var session in SnapshotSessions())
        {
            session.ResumeTransmission();
            affected++;
        }

        return affected > 0;
    }

    public bool ApplyEncoderSettingsNow()
    {
        bool applied = false;
        foreach (var session in SnapshotSessions())
            applied |= session.ApplyEncoderSettingsNow();

        return applied;
    }

    public bool RefreshAllClientSubscriptions()
    {
        bool refreshed = false;
        foreach (var session in SnapshotSessions())
            refreshed |= session.ReapplyCurrentSubscription();

        return refreshed;
    }

    public bool DebugAddTrackToFirstClient()
    {
        var session = SnapshotSessions().FirstOrDefault();
        return session != null && session.DebugAddNextSource();
    }

    public bool DebugRemoveTrackFromFirstClient()
    {
        var session = SnapshotSessions().FirstOrDefault();
        return session != null && session.DebugRemoveLastSource();
    }

    public string[] BuildActivationDebugSnapshot()
    {
        return _activationTable.BuildDebugSnapshot();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        ShutdownCore();
        _isDisposed = true;
    }

    private void ShutdownCore()
    {
        try
        {
            _serverCts?.Cancel();
        }
        catch
        {
        }

        _listener?.Dispose();
        _listener = null;

        WebRtcMediaServerClientSession[] sessions = SnapshotSessions();
        foreach (var session in sessions)
        {
            NotifySessionClosed(session);
            session.Dispose();
        }

        lock (_sessionsLock)
            _sessions.Clear();

        _serverCts?.Dispose();
        _serverCts = null;
        PublishActivationSnapshot();
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
                    _sessions[sessionId] = session;

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

    private void PublishActivationSnapshot()
    {
        _manager.SetMediaServerActivationSnapshot(BuildActivationDebugSnapshot());
    }

    private WebRtcMediaServerClientSession[] SnapshotSessions()
    {
        lock (_sessionsLock)
            return _sessions.Values.ToArray();
    }
}

