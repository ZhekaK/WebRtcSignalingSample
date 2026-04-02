using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class SenderRelayHub : IDisposable
{
    private readonly SenderManager _manager;
    private readonly SenderSourceCatalog _catalog = new();
    private readonly SenderSourceActivationTable _activationTable = new();
    private readonly SenderSharedTrackRegistry _sharedTrackRegistry = new();
    private readonly Dictionary<string, SenderSubscriberSession> _sessions = new(StringComparer.Ordinal);
    private readonly object _sessionsLock = new();

    private CancellationTokenSource _hubCts = new();
    private SenderSignalingClient _signalingClient;
    private bool _isDisposed;

    public SenderRelayHub(SenderManager manager)
    {
        _manager = manager;
    }

    public bool IsConnectionReady => _signalingClient?.IsConnected == true;
    internal SenderSharedTrackRegistry SharedTrackRegistry => _sharedTrackRegistry;

    public async Task StartAsync()
    {
        if (_isDisposed || IsConnectionReady)
            return;

        ShutdownConnection();
        ResetCancellation();

        CancellationToken token = _hubCts.Token;
        _signalingClient = new SenderSignalingClient();
        SubscribeToSignaling(_signalingClient);

        await _signalingClient.ConnectAsync(_manager.IP, _manager.Port, Application.productName, token);
        if (_isDisposed || token.IsCancellationRequested)
            return;

        _manager.EnsureWebRtcUpdateLoop();
        _signalingClient.StartPolling(token);
        RefreshInspectorState();
        await PublishCatalogAsync(token);
    }

    public async Task RestartAsync()
    {
        if (_isDisposed)
            return;

        ShutdownConnection();
        await StartAsync();
    }

    public bool PauseAllSessions()
    {
        int affected = 0;
        foreach (SenderSubscriberSession session in SnapshotSessions())
        {
            session.PauseTransmission();
            affected++;
        }

        return affected > 0;
    }

    public bool ResumeAllSessions()
    {
        int affected = 0;
        foreach (SenderSubscriberSession session in SnapshotSessions())
        {
            session.ResumeTransmission();
            affected++;
        }

        return affected > 0;
    }

    public bool ApplyEncoderSettingsNow()
    {
        bool applied = false;
        foreach (SenderSubscriberSession session in SnapshotSessions())
            applied |= session.ApplyEncoderSettingsNow();

        return applied;
    }

    public bool RefreshAllClientSubscriptions()
    {
        RefreshInspectorState();
        _ = PublishCatalogAsync(CancellationToken.None);

        bool refreshed = false;
        foreach (SenderSubscriberSession session in SnapshotSessions())
            refreshed |= session.ReapplyCurrentSubscription();

        return refreshed;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        ShutdownConnection();
        _sharedTrackRegistry.Dispose();
        _hubCts.Dispose();
    }

    internal void RefreshInspectorState()
    {
        _manager.SetAvailableRenderTextureCatalogSnapshot(_catalog.BuildAvailableRenderTextureSnapshot());
        _manager.SetActiveSourceSnapshot(_activationTable.BuildDebugSnapshot());
    }

    internal MediaCatalogMessage BuildCatalogMessage()
    {
        return _catalog.BuildCatalogMessage();
    }

    internal SenderSourceCatalog.ResolvedSource[] ResolveSubscription(MediaSubscriptionRequest request)
    {
        return _catalog.Resolve(request);
    }

    internal void LogRequestedActions(string sessionId, string clientName, IReadOnlyList<SenderSourceCatalog.ResolvedSource> next)
    {
        string sessionLabel = string.IsNullOrWhiteSpace(clientName)
            ? sessionId
            : $"{sessionId} ({clientName})";

        foreach (var transition in _activationTable.ApplySessionSelection(sessionId, next))
        {
            string verb = transition.ChangeType == SenderSourceActivationTable.ActivationChangeType.Activated
                ? "ENABLE"
                : "DISABLE";

            Debug.Log(
                $"[Sender] Action {verb} for {sessionLabel}: source={transition.SourceName} " +
                $"sourceId={transition.SourceId} serverDisplay={transition.ServerDisplayIndex + 1} " +
                $"layer={transition.RenderLayer} refs={transition.ReferenceCount}");
        }

        RefreshInspectorState();
    }

    internal void NotifySessionClosed(string receiverClientId)
    {
        if (string.IsNullOrWhiteSpace(receiverClientId))
            return;

        foreach (var transition in _activationTable.RemoveSession(receiverClientId))
        {
            Debug.Log(
                $"[Sender] Action DISABLE for {receiverClientId}: source={transition.SourceName} " +
                $"sourceId={transition.SourceId} serverDisplay={transition.ServerDisplayIndex + 1} " +
                $"layer={transition.RenderLayer} refs={transition.ReferenceCount}");
        }

        lock (_sessionsLock)
            _sessions.Remove(receiverClientId);

        RefreshInspectorState();
    }

    internal void SendToReceiver(string receiverClientId, string type, string payload)
    {
        _signalingClient?.SendToReceiver(receiverClientId, type, payload);
    }

    private void SubscribeToSignaling(SenderSignalingClient signalingClient)
    {
        if (signalingClient == null)
            return;

        signalingClient.MessageReceived += OnSignalingMessageReceived;
        signalingClient.ConnectionLost += OnSignalingConnectionLost;
    }

    private void UnsubscribeFromSignaling(SenderSignalingClient signalingClient)
    {
        if (signalingClient == null)
            return;

        signalingClient.MessageReceived -= OnSignalingMessageReceived;
        signalingClient.ConnectionLost -= OnSignalingConnectionLost;
    }

    private void OnSignalingMessageReceived(FlexSignalingEnvelope envelope)
    {
        if (_isDisposed || envelope == null)
            return;

        if (string.Equals(envelope.type, SignalingServerMessageTypes.PeerRemoved, StringComparison.Ordinal))
        {
            CloseReceiverSession(string.IsNullOrWhiteSpace(envelope.payload) ? envelope.fromClientId : envelope.payload);
            return;
        }

        string receiverClientId = envelope.fromClientId;
        if (string.IsNullOrWhiteSpace(receiverClientId))
            return;

        SenderSubscriberSession session = GetOrCreateSession(receiverClientId);
        session.HandleRemoteMessage(envelope.type, envelope.payload);
    }

    private void OnSignalingConnectionLost(Exception ex)
    {
        if (_isDisposed)
            return;

        if (ex != null)
            Debug.LogWarning($"[Sender] Signaling disconnected: {ex.Message}");

        ShutdownConnection();
    }

    private SenderSubscriberSession GetOrCreateSession(string receiverClientId)
    {
        lock (_sessionsLock)
        {
            if (_sessions.TryGetValue(receiverClientId, out SenderSubscriberSession existing))
                return existing;

            SenderSubscriberSession session = new(this, _manager, receiverClientId);
            _sessions[receiverClientId] = session;
            session.EnsureStarted();
            return session;
        }
    }

    private void CloseReceiverSession(string receiverClientId)
    {
        if (string.IsNullOrWhiteSpace(receiverClientId))
            return;

        SenderSubscriberSession session = null;
        lock (_sessionsLock)
        {
            if (_sessions.TryGetValue(receiverClientId, out session))
                _sessions.Remove(receiverClientId);
        }

        NotifySessionClosed(receiverClientId);
        session?.Dispose();
    }

    private async Task PublishCatalogAsync(CancellationToken token)
    {
        try
        {
            RefreshInspectorState();

            if (_signalingClient == null || !IsConnectionReady)
                return;

            await _signalingClient.PublishCatalogAsync(BuildCatalogMessage(), token);
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
                Debug.LogWarning($"[Sender] Failed to publish catalog: {ex.Message}");
        }
    }

    private void ShutdownConnection()
    {
        try
        {
            _hubCts.Cancel();
        }
        catch
        {
        }

        SenderSubscriberSession[] sessions = SnapshotSessions();
        lock (_sessionsLock)
            _sessions.Clear();

        foreach (SenderSubscriberSession session in sessions)
        {
            NotifySessionClosed(session.ReceiverClientId);
            session.Dispose();
        }

        SenderSignalingClient signalingClient = _signalingClient;
        _signalingClient = null;
        UnsubscribeFromSignaling(signalingClient);
        signalingClient?.Dispose();

        RefreshInspectorState();
    }

    private SenderSubscriberSession[] SnapshotSessions()
    {
        lock (_sessionsLock)
            return _sessions.Values.ToArray();
    }

    private void ResetCancellation()
    {
        _hubCts.Dispose();
        _hubCts = new CancellationTokenSource();
    }
}
