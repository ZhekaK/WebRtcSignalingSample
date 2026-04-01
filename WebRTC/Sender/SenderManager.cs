using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TabletTypes;
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
public partial class SenderManager : MonoBehaviour
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

    [Header("Inspector")]
    [SerializeField] private string[] ActiveMediaServerSources = Array.Empty<string>();

    private SenderSession _session;
    private WebRtcMediaServer _mediaServer;
    private bool _isRestartingTransmission;
    private Coroutine _webRtcUpdateCoroutine;
    private CancellationTokenSource _connectionLoopCts;

    public static void InitializeManager()
    {
        if (Instance)
            return;

        GameObject sender = new(nameof(SenderManager));
        Instance = sender.AddComponent<SenderManager>();
        DontDestroyOnLoad(sender);

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
        if (LayerDataReceiverModeController.Instance == null)
            return;

        LayerDataReceiverModeController.Instance.OnEvsStateChanged += LayerDataReceiverModeController_OnStateChanged;
    }

    private void Unsubscribe()
    {
        if (LayerDataReceiverModeController.Instance == null)
            return;

        LayerDataReceiverModeController.Instance.OnEvsStateChanged -= LayerDataReceiverModeController_OnStateChanged;
    }

    protected virtual void Start()
    {
        if (RuntimeMode == SenderTransportMode.MediaServer)
        {
            _mediaServer = new WebRtcMediaServer(this);
            SetMediaServerActivationSnapshot(Array.Empty<string>());
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

    protected virtual void OnDestroy()
    {
        Unsubscribe();

        StopConnectionLoop();
        _mediaServer?.Dispose();
        _mediaServer = null;
        SetMediaServerActivationSnapshot(Array.Empty<string>());
        _session?.Dispose();
        _session = null;
        StopWebRtcUpdateLoop();
    }

    internal void SetMediaServerActivationSnapshot(string[] snapshot)
    {
        ActiveMediaServerSources = snapshot ?? Array.Empty<string>();
    }
}
