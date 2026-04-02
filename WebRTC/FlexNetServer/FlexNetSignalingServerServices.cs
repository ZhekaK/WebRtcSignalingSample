using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IFlexSignalingService
{
    FlexSignalingRegisterResponse Register(FlexSignalingRegisterRequest request);
    bool Unregister(string clientId);
    bool PublishCatalog(FlexSignalingPublishCatalogRequest request);
    bool SendFromReceiver(FlexSignalingReceiverMessageRequest request);
    bool SendFromSender(FlexSignalingSenderMessageRequest request);
    Task<FlexSignalingPollResponse> PollAsync(FlexSignalingPollRequest request, CancellationToken token);
}

public sealed class FlexSignalingService : IFlexSignalingService
{
    private sealed class ClientState
    {
        public string ClientId = string.Empty;
        public string ClientName = string.Empty;
        public SignalingClientRole Role = SignalingClientRole.Unknown;
        public DateTime LastSeenUtc = DateTime.UtcNow;
    }

    private readonly FlexNetSignalingServerOptions _options;
    private readonly ConcurrentDictionary<string, ClientState> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<FlexSignalingEnvelope>> _inboxes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _signals = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();

    private MediaCatalogMessage _currentCatalog;
    private string _currentSenderClientId = string.Empty;
    private int _nextClientId;

    public FlexSignalingService(FlexNetSignalingServerOptions options)
    {
        _options = options ?? new FlexNetSignalingServerOptions();
    }

    public FlexSignalingRegisterResponse Register(FlexSignalingRegisterRequest request)
    {
        PruneExpiredClients();

        string clientId = $"signal-client-{Interlocked.Increment(ref _nextClientId):0000}";
        ClientState state = new ClientState
        {
            ClientId = clientId,
            ClientName = request?.clientName?.Trim() ?? string.Empty,
            Role = request?.role ?? SignalingClientRole.Unknown,
            LastSeenUtc = DateTime.UtcNow,
        };

        lock (_stateLock)
        {
            if (state.Role == SignalingClientRole.Sender &&
                !string.IsNullOrWhiteSpace(_currentSenderClientId) &&
                !string.Equals(_currentSenderClientId, clientId, StringComparison.Ordinal))
            {
                Log($"Replacing previous sender '{_currentSenderClientId}' with '{clientId}'.");
                RemoveClientCore(_currentSenderClientId, notifySenderAboutReceiverLoss: false);
                _currentCatalog = null;
            }

            _clients[clientId] = state;
            _inboxes[clientId] = new ConcurrentQueue<FlexSignalingEnvelope>();
            _signals[clientId] = new SemaphoreSlim(0, int.MaxValue);

            if (state.Role == SignalingClientRole.Sender)
                _currentSenderClientId = clientId;
        }

        Log($"Registered {DescribeClient(state)}.");

        return new FlexSignalingRegisterResponse
        {
            clientId = clientId,
            senderClientId = GetCurrentSenderClientId(),
            role = state.Role,
            catalog = CloneCatalog(_currentCatalog),
        };
    }

    public bool Unregister(string clientId)
    {
        PruneExpiredClients();
        bool removed = RemoveClientCore(clientId, notifySenderAboutReceiverLoss: true);
        if (!removed)
            LogWarning($"Unregister ignored: client '{clientId}' was not found.");
        return removed;
    }

    public bool PublishCatalog(FlexSignalingPublishCatalogRequest request)
    {
        PruneExpiredClients();

        if (request == null)
            return false;

        if (!TryTouchClient(request.senderClientId, SignalingClientRole.Sender))
            return false;

        lock (_stateLock)
        {
            if (!string.Equals(_currentSenderClientId, request.senderClientId, StringComparison.Ordinal))
                return false;

            _currentCatalog = CloneCatalog(request.catalog);
            Log($"Catalog published by sender '{request.senderClientId}': revision={_currentCatalog?.revision ?? -1}, sources={_currentCatalog?.sources?.Length ?? 0}.");
            return true;
        }
    }

    public bool SendFromReceiver(FlexSignalingReceiverMessageRequest request)
    {
        PruneExpiredClients();

        if (request == null || string.IsNullOrWhiteSpace(request.type))
            return false;

        if (!TryTouchClient(request.receiverClientId, SignalingClientRole.Receiver))
            return false;

        string senderClientId = GetCurrentSenderClientId();
        if (string.IsNullOrWhiteSpace(senderClientId))
            return false;

        bool enqueued = EnqueueMessage(senderClientId, new FlexSignalingEnvelope
        {
            fromClientId = request.receiverClientId,
            toClientId = senderClientId,
            type = request.type,
            payload = request.payload ?? string.Empty,
        });

        if (enqueued)
            Log($"Receiver '{request.receiverClientId}' -> sender '{senderClientId}': type='{request.type}'.");
        else
            LogWarning($"Failed to relay receiver message '{request.type}' from '{request.receiverClientId}' to sender '{senderClientId}'.");

        return enqueued;
    }

    public bool SendFromSender(FlexSignalingSenderMessageRequest request)
    {
        PruneExpiredClients();

        if (request == null || string.IsNullOrWhiteSpace(request.receiverClientId) || string.IsNullOrWhiteSpace(request.type))
            return false;

        if (!TryTouchClient(request.senderClientId, SignalingClientRole.Sender))
            return false;

        if (!ClientExists(request.receiverClientId, SignalingClientRole.Receiver))
            return false;

        if (!string.Equals(request.senderClientId, GetCurrentSenderClientId(), StringComparison.Ordinal))
            return false;

        bool enqueued = EnqueueMessage(request.receiverClientId, new FlexSignalingEnvelope
        {
            fromClientId = request.senderClientId,
            toClientId = request.receiverClientId,
            type = request.type,
            payload = request.payload ?? string.Empty,
        });

        if (enqueued)
            Log($"Sender '{request.senderClientId}' -> receiver '{request.receiverClientId}': type='{request.type}'.");
        else
            LogWarning($"Failed to relay sender message '{request.type}' from '{request.senderClientId}' to receiver '{request.receiverClientId}'.");

        return enqueued;
    }

    public async Task<FlexSignalingPollResponse> PollAsync(FlexSignalingPollRequest request, CancellationToken token)
    {
        PruneExpiredClients();

        if (request == null || string.IsNullOrWhiteSpace(request.clientId))
            return null;

        if (!_clients.TryGetValue(request.clientId, out ClientState state))
            return null;

        state.LastSeenUtc = DateTime.UtcNow;
        ConcurrentQueue<FlexSignalingEnvelope> inbox = _inboxes.GetOrAdd(request.clientId, _ => new ConcurrentQueue<FlexSignalingEnvelope>());
        SemaphoreSlim signal = _signals.GetOrAdd(request.clientId, _ => new SemaphoreSlim(0, int.MaxValue));

        int timeoutMs = request.timeoutMs > 0
            ? request.timeoutMs
            : _options.DefaultPollTimeoutMs;

        using CancellationTokenSource timeoutCts = new(timeoutMs);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

        try
        {
            if (inbox.IsEmpty)
                await signal.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        List<FlexSignalingEnvelope> batch = new();
        int maxMessages = Math.Max(1, request.maxMessages);
        while (batch.Count < maxMessages && inbox.TryDequeue(out FlexSignalingEnvelope envelope))
            batch.Add(CloneEnvelope(envelope));

        if (batch.Count > 0)
            Log($"Polled {batch.Count} message(s) for {DescribeClient(state)}.");
        else if (_options.LogPollTimeouts)
            Log($"Poll timeout for {DescribeClient(state)}.");

        return new FlexSignalingPollResponse
        {
            senderClientId = GetCurrentSenderClientId(),
            catalog = CloneCatalog(_currentCatalog),
            messages = batch.ToArray(),
        };
    }

    private bool RemoveClientCore(string clientId, bool notifySenderAboutReceiverLoss)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        if (!_clients.TryRemove(clientId, out ClientState state))
            return false;

        Log($"Removed {DescribeClient(state)}.");

        _inboxes.TryRemove(clientId, out _);
        if (_signals.TryRemove(clientId, out SemaphoreSlim signal))
            signal.Dispose();

        lock (_stateLock)
        {
            if (string.Equals(_currentSenderClientId, clientId, StringComparison.Ordinal))
            {
                _currentSenderClientId = string.Empty;
                _currentCatalog = null;
                Log("Sender disconnected. Catalog reset.");
            }
        }

        if (notifySenderAboutReceiverLoss && state.Role == SignalingClientRole.Receiver)
        {
            string senderClientId = GetCurrentSenderClientId();
            if (!string.IsNullOrWhiteSpace(senderClientId))
            {
                EnqueueMessage(senderClientId, new FlexSignalingEnvelope
                {
                    fromClientId = clientId,
                    toClientId = senderClientId,
                    type = SignalingServerMessageTypes.PeerRemoved,
                    payload = clientId,
                });

                Log($"Notified sender '{senderClientId}' that receiver '{clientId}' was removed.");
            }
        }

        return true;
    }

    private void PruneExpiredClients()
    {
        if (_options.ClientLeaseSeconds <= 0)
            return;

        DateTime threshold = DateTime.UtcNow.AddSeconds(-_options.ClientLeaseSeconds);
        string[] expiredClientIds = _clients.Values
            .Where(client => client.LastSeenUtc < threshold)
            .Select(client => client.ClientId)
            .ToArray();

        foreach (string expiredClientId in expiredClientIds)
        {
            Log($"Lease expired for client '{expiredClientId}'.");
            RemoveClientCore(expiredClientId, notifySenderAboutReceiverLoss: true);
        }
    }

    private bool EnqueueMessage(string targetClientId, FlexSignalingEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(targetClientId) || envelope == null)
            return false;

        if (!_clients.ContainsKey(targetClientId))
            return false;

        ConcurrentQueue<FlexSignalingEnvelope> inbox = _inboxes.GetOrAdd(targetClientId, _ => new ConcurrentQueue<FlexSignalingEnvelope>());
        SemaphoreSlim signal = _signals.GetOrAdd(targetClientId, _ => new SemaphoreSlim(0, int.MaxValue));

        inbox.Enqueue(envelope);
        try
        {
            signal.Release();
        }
        catch (ObjectDisposedException)
        {
            LogWarning($"Message queue for client '{targetClientId}' was already disposed.");
            return false;
        }

        return true;
    }

    private bool TryTouchClient(string clientId, SignalingClientRole expectedRole)
    {
        if (!_clients.TryGetValue(clientId, out ClientState state))
            return false;

        if (expectedRole != SignalingClientRole.Unknown && state.Role != expectedRole)
            return false;

        state.LastSeenUtc = DateTime.UtcNow;
        return true;
    }

    private bool ClientExists(string clientId, SignalingClientRole expectedRole)
    {
        return _clients.TryGetValue(clientId, out ClientState state) &&
               (expectedRole == SignalingClientRole.Unknown || state.Role == expectedRole);
    }

    private string GetCurrentSenderClientId()
    {
        lock (_stateLock)
            return _currentSenderClientId ?? string.Empty;
    }

    private static MediaCatalogMessage CloneCatalog(MediaCatalogMessage catalog)
    {
        if (catalog == null)
            return null;

        return new MediaCatalogMessage
        {
            revision = catalog.revision,
            sources = catalog.sources == null
                ? Array.Empty<MediaSourceDescriptor>()
                : catalog.sources
                    .Where(source => source != null)
                    .Select(source => new MediaSourceDescriptor
                    {
                        sourceId = source.sourceId,
                        serverDisplayIndex = source.serverDisplayIndex,
                        renderLayer = source.renderLayer,
                        sourceName = source.sourceName,
                        width = source.width,
                        height = source.height,
                        isDefaultLayer = source.isDefaultLayer,
                    })
                    .ToArray(),
        };
    }

    private static FlexSignalingEnvelope CloneEnvelope(FlexSignalingEnvelope envelope)
    {
        if (envelope == null)
            return null;

        return new FlexSignalingEnvelope
        {
            fromClientId = envelope.fromClientId,
            toClientId = envelope.toClientId,
            type = envelope.type,
            payload = envelope.payload,
        };
    }

    private void Log(string message)
    {
        if (_options.EnableLogging)
            Debug.Log($"[SignalingServer] {message}");
    }

    private void LogWarning(string message)
    {
        if (_options.EnableLogging)
            Debug.LogWarning($"[SignalingServer] {message}");
    }

    private static string DescribeClient(ClientState state)
    {
        if (state == null)
            return "client <null>";

        string clientName = string.IsNullOrWhiteSpace(state.ClientName)
            ? "<unnamed>"
            : state.ClientName;

        return $"clientId={state.ClientId}, role={state.Role}, name={clientName}";
    }
}
