using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using TabletTypes;
using UnityEngine;

[Serializable]
public struct SenderAvailableRenderTextureInfo
{
    public string SourceId;
    public string SourceName;
    public int ServerDisplayIndex;
    public string RenderLayer;
    public int Width;
    public int Height;
    public bool IsDefaultLayer;
    public RenderTexture Texture;
}

public enum SenderVideoCodecPreference
{
    Auto = 0,
    H264 = 1,
    VP8 = 2,
    VP9 = 3,
    AV1 = 4
}

[DefaultExecutionOrder(-20)]
public partial class SenderManager : MonoBehaviour
{
    public static SenderManager Instance { get; private set; }

    [Header("Signaling")]
    [Tooltip("FlexNet signaling server IP.")]
    public string IP = "10.24.50.23";

    [Tooltip("FlexNet signaling server port.")]
    public int Port = 8005;

    [Header("Video Sources")]
    [HideInInspector]
    [Tooltip("Legacy cache of the current layer RenderTextures. Sending now uses the full source catalog from DisplaysManager.")]
    public List<RenderTexture> SourceRenderTextures = new();

    [Header("Connection")]
    [Tooltip("Continuously tries to connect/reconnect to the signaling server when disconnected.")]
    public bool WaitForClientConnection = true;

    [Tooltip("Reconnect retry interval in milliseconds.")]
    [Range(100, 10000)]
    public int ReconnectDelayMs = 1000;

    [Header("Codec")]
    [Tooltip("Preferred video codec for outgoing tracks. Auto keeps WebRTC default negotiation order.")]
    public SenderVideoCodecPreference PreferredVideoCodec = SenderVideoCodecPreference.H264;

    [Header("Bandwidth Budget")]
    [Tooltip("Total max bitrate budget for all video streams in Mbps.")]
    [Range(0, 1000)]
    public int TotalMaxBitrateMbps = 280;

    [Tooltip("Total min bitrate budget for all video streams in Mbps.")]
    [Range(0, 1000)]
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
    [SerializeField] private SenderAvailableRenderTextureInfo[] AvailableRenderTextureCatalog = Array.Empty<SenderAvailableRenderTextureInfo>();
    [SerializeField] private string[] ActiveRequestedSources = Array.Empty<string>();

    private FlexNetSignalingServer _signalingServer;
    private SenderRelayHub _relayHub;
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

    protected virtual void Start()
    {
        EnsureSignalingServerStarted();

        _relayHub = new SenderRelayHub(this);
        SetRenderTextures(RenderLayer.Visible);
        _relayHub.RefreshInspectorState();

        if (WaitForClientConnection)
            StartConnectionLoop();
        else
            _ = _relayHub.StartAsync();
    }

    protected virtual void OnDestroy()
    {
        StopConnectionLoop();
        _relayHub?.Dispose();
        _relayHub = null;
        DisposeSignalingServer();
        SetAvailableRenderTextureCatalogSnapshot(Array.Empty<SenderAvailableRenderTextureInfo>());
        SetActiveSourceSnapshot(Array.Empty<string>());
        StopWebRtcUpdateLoop();

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        DisposeSignalingServer();
    }

    internal void SetAvailableRenderTextureCatalogSnapshot(SenderAvailableRenderTextureInfo[] snapshot)
    {
        AvailableRenderTextureCatalog = snapshot ?? Array.Empty<SenderAvailableRenderTextureInfo>();
    }

    internal void SetActiveSourceSnapshot(string[] snapshot)
    {
        ActiveRequestedSources = snapshot ?? Array.Empty<string>();
    }

    private void EnsureSignalingServerStarted()
    {
        if (_signalingServer?.IsStarted == true)
            return;

        _signalingServer ??= new FlexNetSignalingServer(new FlexNetSignalingServerOptions
        {
            EndPoint = new IPEndPoint(IPAddress.Any, Port),
        });

        _signalingServer.Start();
    }

    private void DisposeSignalingServer()
    {
        if (_signalingServer == null)
            return;

        try
        {
            _signalingServer.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Sender] Failed to stop signaling server cleanly: {ex.Message}");
        }
        finally
        {
            _signalingServer = null;
        }
    }
}
