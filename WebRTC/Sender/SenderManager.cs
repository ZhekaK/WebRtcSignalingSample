using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TabletTypes;
using Unity.WebRTC;
using UnityEngine;

public enum SenderVideoCodecPreference
{
    Auto = 0,
    H264 = 1,
    VP8 = 2,
    VP9 = 3,
    AV1 = 4
}

public enum SenderTransportMode
{
    DirectPeer = 0,
    MediaServer = 1
}

/// <summary>
/// Unity-facing manager for the sender module.
/// Attach this component to the sender GameObject.
/// </summary>
[DefaultExecutionOrder(-20)]
public class SenderManager : MonoBehaviour
{
    public static SenderManager Instance { get; private set; }

    [Header("Runtime")]
    [Tooltip("DirectPeer preserves the old one-to-one sender->receiver flow. MediaServer starts a FlexNet signaling server and creates one WebRTC peer per connected client.")]
    public SenderTransportMode RuntimeMode = SenderTransportMode.MediaServer;

    [Header("Signaling")]
    [Tooltip("Receiver signaling host IP address for DirectPeer mode.")]
    public string IP = "10.24.50.23";

    [Tooltip("Receiver signaling host port for DirectPeer mode, or local listen port for MediaServer mode.")]
    public int Port = 8005;

    [Header("Video Sources")]
    [Tooltip("RenderTextures in source order used by sender tracks.")]
    public List<RenderTexture> SourceRenderTextures = new();

    [Header("Connection")]
    [Tooltip("Continuously tries to connect/reconnect signaling and peer when disconnected.")]
    public bool WaitForClientConnection = true;

    [Tooltip("Reconnect retry interval in milliseconds.")]
    [Range(100, 10000)]
    public int ReconnectDelayMs = 1000;

    [Header("Codec")]
    [Tooltip("Preferred video codec for outgoing tracks. Auto keeps WebRTC default negotiation order.")]
    public SenderVideoCodecPreference PreferredVideoCodec = SenderVideoCodecPreference.H264;

    [Header("Bandwidth Budget")]
    [Tooltip("Total max bitrate budget for all video streams in Mbps.")]
    [Range(30, 1000)]
    public int TotalMaxBitrateMbps = 280;

    [Tooltip("Total min bitrate budget for all video streams in Mbps.")]
    [Range(10, 1000)]
    public int TotalMinBitrateMbps = 120;

    [Tooltip("If enabled, sender does not set a framerate cap in RTP encoding parameters.")]
    public bool UseMaxFps = false;

    [Tooltip("Maximum encoding framerate for each stream when UseMaxFps is disabled.")]
    [Range(1, 90)]
    public int MaxFramerate = 60;

    [Header("Diagnostics")]
    [Tooltip("Logs sender-side WebRTC stats to the console (bitrate, FPS, codec, QP, RTT, loss).")]
    public bool EnableSenderStatsLogging = false;

    [Tooltip("Interval in seconds for sender stats logging.")]
    [Range(1, 30)]
    public int SenderStatsLogIntervalSec = 10;

    private SenderSession _session;
    private WebRtcMediaServer _mediaServer;
    private bool _isRestartingTransmission;
    private Coroutine _webRtcUpdateCoroutine;
    private CancellationTokenSource _connectionLoopCts;

    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject reciever = new(nameof(SenderManager));
        Instance = reciever.AddComponent<SenderManager>();
        DontDestroyOnLoad(reciever);

        Instance.LoadSettings(SaveManager.Instance.Settings.WebRtcSender);
        Instance.Subscribe();
    }

    private void LoadSettings(WebRtcSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("WebRTC Settings not found");
            return;
        }
        IP = settings.IP;
        Port = settings.Port;
        TotalMaxBitrateMbps = settings.TotalMaxBitrateMbps;
        TotalMinBitrateMbps = settings.TotalMinBitrateMbps;
    }

    private void Subscribe()
    {
        if (LayerDataReceiverModeController.Instance == null) return;
        LayerDataReceiverModeController.Instance.OnEvsStateChanged += LayerDataReceiverModeController_OnStateChanged;
    }

    private void Unsubscribe()
    {
        if (LayerDataReceiverModeController.Instance == null) return;
        LayerDataReceiverModeController.Instance.OnEvsStateChanged -= LayerDataReceiverModeController_OnStateChanged;
    }

    protected virtual void Start()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            _mediaServer = new WebRtcMediaServer(this);
            _ = _mediaServer.StartAsync();
            return;
        }

        _session = new SenderSession(this);
        SetRenderTextures(RenderLayer.Visible);

        if (WaitForClientConnection)
            StartConnectionLoop();
        else
            _ = _session.InitializeAsync(forceReconnect: false);
    }


    private void LayerDataReceiverModeController_OnStateChanged(object sender, EvsStateChangedEventArgs e)
    {
        if (e.CurrentStateEvs)
            SetRenderTextures(RenderLayer.EVS);
        else
            SetRenderTextures(CastToNewEnum(e.CurrentLayer));
    }

    private RenderLayer CastToNewEnum(ImitatorVisibleLayer layer)
    {
        switch (layer)
        {
            case ImitatorVisibleLayer.Visible:
                return RenderLayer.Visible;
            case ImitatorVisibleLayer.LWIR:
                return RenderLayer.LWIR;
            case ImitatorVisibleLayer.SWIR:
                return RenderLayer.SWIR;
            case ImitatorVisibleLayer.Labels:
                return RenderLayer.Labels;
            default:
                return RenderLayer.Visible;
        }
    }
    public void SetRenderTextures(RenderLayer layer)
    {
        if (SourceRenderTextures == null)
            SourceRenderTextures = new List<RenderTexture>();
        else
            SourceRenderTextures.Clear();

        foreach (DisplayData displayData in DisplaysManager.Instance.DisplaysDatas.Values)
        {
            if (displayData.RenderingLayersDatas.TryGetValue(layer, out RenderLayerData renderLayerData))
                if (renderLayerData.Settings.ExistOnDisplay) SourceRenderTextures.Add(renderLayerData.RT);
        }

        _session?.SyncTracksWithSources();
    }

    /// <summary>
    /// Stops sending video tracks without destroying the session.
    /// </summary>
    [ContextMenu("Pause Transmission")]
    public void PauseTransmission()
    {
        _session?.PauseTransmission();
    }

    /// <summary>
    /// Resumes sending previously paused video tracks.
    /// </summary>
    [ContextMenu("Resume Transmission")]
    public void ResumeTransmission()
    {
        _session?.ResumeTransmission();
    }

    /// <summary>
    /// Applies current encoder settings to all active sender tracks immediately.
    /// </summary>
    [ContextMenu("Apply Encoder Settings Now")]
    public bool ApplyEncoderSettingsNow()
    {
        return _session != null && _session.ApplyEncoderSettingsNow();
    }

    /// <summary>
    /// Resizes a source RenderTexture and refreshes the track in real time.
    /// </summary>
    public bool ChangeSourceResolution(int displayIndex, int width, int height)
    {
        return _session != null && _session.ChangeSourceResolution(displayIndex, width, height);
    }

    /// <summary>
    /// Rebuilds a sender track from the current RenderTexture at display index.
    /// </summary>
    public bool RefreshSourceTrack(int displayIndex)
    {
        return _session != null && _session.RefreshSourceTrack(displayIndex);
    }

    /// <summary>
    /// Synchronizes active WebRTC tracks with the current SourceRenderTextures collection.
    /// Non-null textures become active tracks; null slots are removed from transmission.
    /// </summary>
    [ContextMenu("Sync Tracks With Sources")]
    public bool SyncTracksWithSources()
    {
        return _session != null && _session.SyncTracksWithSources();
    }

    [ContextMenu("Test Add Track")]
    public void TestAddTrack()
    {
        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
        {
            Debug.LogError("No source textures configured.");
            return;
        }

        if (!AddSourceTrack(SourceRenderTextures[SourceRenderTextures.Count - 1])) Debug.LogError("Add track failed");
        if (!SyncTracksWithSources()) Debug.LogError("Sync track failed");
    }

    [ContextMenu("Test Remove Track")]
    public void TestRemoveTrack()
    {
        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
        {
            Debug.LogError("No source textures configured.");
            return;
        }

        if (!RemoveSourceTrack(SourceRenderTextures.Count - 1)) Debug.LogError("Remove track failed");
        if (!SyncTracksWithSources()) Debug.LogError("Sync track failed");
    }
    /// <summary>
    /// Adds or replaces a source texture at runtime and ensures a matching sender track exists.
    /// </summary>
    public bool AddSourceTrack(RenderTexture sourceTexture, int displayIndex = -1)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Cannot add null source texture.");
            return false;
        }

        int targetIndex = ResolveTargetDisplayIndex(displayIndex);
        EnsureSourceCapacity(targetIndex + 1);
        SourceRenderTextures[targetIndex] = sourceTexture;

        if (_session == null)
            return true;

        return _session.AddOrReplaceSourceTrack(targetIndex, sourceTexture);
    }

    /// <summary>
    /// Removes a source track from transmission for the specified display index.
    /// </summary>
    public bool RemoveSourceTrack(int displayIndex, bool clearSourceSlot = true)
    {
        if (displayIndex < 0 || SourceRenderTextures == null || displayIndex >= SourceRenderTextures.Count)
        {
            Debug.LogError($"Invalid display index: {displayIndex}");
            return false;
        }

        bool removed = _session == null || _session.RemoveSourceTrack(displayIndex);
        if (!removed)
            return false;

        if (clearSourceSlot)
            SourceRenderTextures[displayIndex] = null;

        TrimTrailingEmptySourceSlots();
        return true;
    }

    /// <summary>
    /// Performs a clean sender restart: closes current signaling/peer and starts again.
    /// </summary>
    [ContextMenu("Restart Transmission Clean")]
    public void RestartTransmission()
    {
        _ = RestartTransmissionAsync();
    }

    /// <summary>
    /// Async clean sender restart.
    /// </summary>
    public async Task RestartTransmissionAsync()
    {
        if (_isRestartingTransmission)
            return;

        _isRestartingTransmission = true;
        try
        {
            if (_session == null)
                _session = new SenderSession(this);

            await _session.RestartAsync();
        }
        finally
        {
            _isRestartingTransmission = false;
        }
    }

    /// <summary>
    /// Changes only destination IP and reconnects signaling/session.
    /// </summary>
    public void ChangeDestinationIp(string newIp)
    {
        _ = ChangeDestinationEndpointAsync(newIp, Port);
    }

    /// <summary>
    /// Changes destination endpoint and reconnects signaling/session.
    /// </summary>
    public void ChangeDestinationEndpoint(string newIp, int newPort)
    {
        _ = ChangeDestinationEndpointAsync(newIp, newPort);
    }

    /// <summary>
    /// Async variant of destination endpoint update and reconnect.
    /// </summary>
    public async Task ChangeDestinationEndpointAsync(string newIp, int newPort)
    {
        if (_session == null)
            return;

        await _session.ChangeDestinationEndpointAsync(newIp, newPort);
    }

    protected virtual void OnDestroy()
    {
        Unsubscribe();

        StopConnectionLoop();
        _mediaServer?.Dispose();
        _mediaServer = null;
        _session?.Dispose();
        _session = null;
        StopWebRtcUpdateLoop();
    }

    private int ResolveTargetDisplayIndex(int preferredIndex)
    {
        if (preferredIndex >= 0)
            return preferredIndex;

        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
            return 0;

        for (int i = 0; i < SourceRenderTextures.Count; i++)
        {
            if (SourceRenderTextures[i] == null)
                return i;
        }

        return SourceRenderTextures.Count;
    }

    private void EnsureSourceCapacity(int requiredLength)
    {
        requiredLength = Mathf.Max(1, requiredLength);

        if (SourceRenderTextures == null)
        {
            SourceRenderTextures = new List<RenderTexture>(requiredLength);
        }

        while (SourceRenderTextures.Count < requiredLength)
            SourceRenderTextures.Add(null);
    }

    private void TrimTrailingEmptySourceSlots()
    {
        if (SourceRenderTextures == null || SourceRenderTextures.Count == 0)
            return;

        int lastNonNull = -1;
        for (int i = SourceRenderTextures.Count - 1; i >= 0; i--)
        {
            if (SourceRenderTextures[i] != null)
            {
                lastNonNull = i;
                break;
            }
        }

        int newLength = Mathf.Max(1, lastNonNull + 1);
        if (newLength == SourceRenderTextures.Count)
            return;

        SourceRenderTextures.RemoveRange(newLength, SourceRenderTextures.Count - newLength);
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
                    _session = new SenderSession(this);

                if (!_session.IsConnectionReady)
                    await _session.InitializeAsync(forceReconnect: false);

                await Task.Delay(Mathf.Max(100, ReconnectDelayMs), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
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
