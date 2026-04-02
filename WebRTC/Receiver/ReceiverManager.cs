using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ReceiverManager : MonoBehaviour
{
    [Header("Connection")]
    public string IP = "127.0.0.1";
    public int Port = 8005;
    public bool WaitForServerConnection = true;
    [Range(100, 10000)] public int ReconnectDelayMs = 1000;

    [Header("Identity")]
    public string ClientName = "SGS_Demonstrator";

    [Header("Output")]
    public RawImage[] OutputImages = new RawImage[3];

    [Header("Behavior")]
    public bool AutoRequestDefaultLayout = true;

    [Header("Inspector")]
    [SerializeField] private Texture[] ReceivedTextures = Array.Empty<Texture>();
    [SerializeField] private string[] ReceivedTrackIds = Array.Empty<string>();
    [SerializeField] private string[] ReceivedTransceiverMids = Array.Empty<string>();
    [SerializeField] private string[] AvailableCatalogSources = Array.Empty<string>();

    private ReceiverSession _session;
    private ReceiverSubscriptionManager _subscriptionManager;
    private bool _isRestartingReceiving;
    private Coroutine _webRtcUpdateCoroutine;
    private CancellationTokenSource _connectionLoopCts;

    protected virtual void Start()
    {
        if (OutputImages == null || OutputImages.Length == 0)
            OutputImages = GetComponentsInChildren<RawImage>(true);

        ClearInspectionArrays();
        _subscriptionManager = new ReceiverSubscriptionManager(this);
        _session = new ReceiverSession(this);

        if (WaitForServerConnection)
            StartConnectionLoop();
        else
            _ = _session.InitializeAsync();
    }

    [ContextMenu("Pause Receiving")]
    public void PauseReceiving()
    {
        _session?.PauseReceiving();
    }

    [ContextMenu("Resume Receiving")]
    public void ResumeReceiving()
    {
        _session?.ResumeReceiving();
    }

    [ContextMenu("Restart Receiving Clean")]
    public void RestartReceiving()
    {
        _ = RestartReceivingAsync();
    }

    public async Task RestartReceivingAsync()
    {
        if (_isRestartingReceiving)
            return;

        _isRestartingReceiving = true;
        try
        {
            if (_session == null)
                _session = new ReceiverSession(this);

            await _session.RestartAsync();
        }
        finally
        {
            _isRestartingReceiving = false;
        }
    }

    [ContextMenu("Request Default Layout")]
    public void RequestDefaultLayout()
    {
        SendSubscription(_subscriptionManager.UseDefaultLayout());
    }

    [ContextMenu("Request One Source Per Monitor")]
    public void RequestOneSourcePerMonitor()
    {
        SendSubscription(_subscriptionManager.UseOneSourcePerMonitor());
    }

    [ContextMenu("Request Three Sources To Center")]
    public void RequestThreeSourcesToCenter()
    {
        SendSubscription(_subscriptionManager.UseThreeSourcesToCenterMonitor());
    }

    internal void OnCatalogReceived(MediaCatalogMessage catalog)
    {
        _subscriptionManager?.UpdateCatalog(catalog);
        AvailableCatalogSources = catalog?.sources == null
            ? Array.Empty<string>()
            : catalog.sources
                .Where(source => source != null)
                .Select(source => $"Display {source.serverDisplayIndex + 1} / {source.sourceName} / {source.sourceId}")
                .ToArray();

        if (AutoRequestDefaultLayout && (_subscriptionManager?.CurrentRequest == null || _subscriptionManager.ActiveScenario == ReceiverSubscriptionScenario.DefaultLayout))
            TrySendCurrentSubscription();
    }

    internal void OnSessionConnected()
    {
        TrySendCurrentSubscription();
    }

    internal void SetInspectionArrays(Texture[] textures, string[] trackIds, string[] mids)
    {
        ReceivedTextures = textures ?? Array.Empty<Texture>();
        ReceivedTrackIds = trackIds ?? Array.Empty<string>();
        ReceivedTransceiverMids = mids ?? Array.Empty<string>();
    }

    internal void ApplyTexture(int slotIndex, Texture texture)
    {
        if (OutputImages == null || slotIndex < 0 || slotIndex >= OutputImages.Length)
            return;

        if (OutputImages[slotIndex] != null)
            OutputImages[slotIndex].texture = texture;
    }

    internal void ClearOutputImages()
    {
        if (OutputImages == null)
            return;

        for (int i = 0; i < OutputImages.Length; i++)
        {
            if (OutputImages[i] != null && OutputImages[i].texture != null)
                OutputImages[i].texture = null;
        }
    }

    internal void EnsureWebRtcUpdateLoop()
    {
        if (_webRtcUpdateCoroutine != null)
            return;

        _webRtcUpdateCoroutine = StartCoroutine(Unity.WebRTC.WebRTC.Update());
    }

    protected virtual void OnDestroy()
    {
        StopConnectionLoop();
        _session?.Dispose();
        _session = null;
        StopWebRtcUpdateLoop();
    }

    private bool SendSubscription(MediaSubscriptionRequest request)
    {
        if (request == null)
            return false;

        return _session != null && _session.SendMediaSubscriptionRequest(request);
    }

    private void TrySendCurrentSubscription()
    {
        if (_session == null || !_session.IsConnectionReady)
            return;

        MediaSubscriptionRequest request = _subscriptionManager?.CurrentRequest;
        if (request == null)
            request = AutoRequestDefaultLayout ? _subscriptionManager?.UseDefaultLayout() : null;

        if (request != null)
            _session.SendMediaSubscriptionRequest(request);
    }

    private void ClearInspectionArrays()
    {
        int count = Mathf.Max(OutputImages?.Length ?? 0, 0);
        ReceivedTextures = new Texture[count];
        ReceivedTrackIds = new string[count];
        ReceivedTransceiverMids = new string[count];
        AvailableCatalogSources = Array.Empty<string>();
    }

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
                if (_session == null)
                    _session = new ReceiverSession(this);

                if (!_session.IsConnectionReady)
                    await _session.InitializeAsync(forceReconnect: false);

                if (_session.IsConnectionReady)
                    TrySendCurrentSubscription();

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

    private void StopWebRtcUpdateLoop()
    {
        if (_webRtcUpdateCoroutine == null)
            return;

        StopCoroutine(_webRtcUpdateCoroutine);
        _webRtcUpdateCoroutine = null;
    }
}
