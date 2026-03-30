using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity-facing manager for the receiver module.
/// Attach this component to the receiver GameObject.
/// </summary>
[DefaultExecutionOrder(-10)]
public class ReceiverManager : MonoBehaviour
{
    [Header("Preview Targets")]
    [Tooltip("Output RawImages in display order: index 0 -> display 1, index 1 -> display 2, etc.")]
    public RawImage[] OutputImages = new RawImage[3];

    [Header("Received State (Inspector)")]
    [SerializeField] private Texture[] ReceivedTextures = new Texture[3];
    [SerializeField] private string[] ReceivedTrackIds = new string[3];
    [SerializeField] private string[] ReceivedTransceiverMids = new string[3];

    [Header("Signaling")]
    [Tooltip("Local signaling port to listen on.")]
    public int Port = 8005;

    [Header("Diagnostics")]
    [Tooltip("Logs receiver-side WebRTC stats to the console (bitrate, FPS, codec, drops, jitter, QP).")]
    public bool EnableReceiverStatsLogging = true;

    [Tooltip("Interval in seconds for receiver stats logging.")]
    [Range(1, 30)]
    public int ReceiverStatsLogIntervalSec = 2;

    private ReceiverSession _session;
    private bool _isRestartingReceiving;
    private Coroutine _webRtcUpdateCoroutine;

    protected virtual async void Start()
    {
        if (OutputImages == null || OutputImages.Length == 0)
            OutputImages = GetComponentsInChildren<RawImage>(true);

        _session = new ReceiverSession(this);
        await _session.InitializeAsync();
    }

    /// <summary>
    /// Stops receiving and decoding image updates.
    /// </summary>
    [ContextMenu("Pause Receiving")]
    public void PauseReceiving()
    {
        _session?.PauseReceiving();
    }

    /// <summary>
    /// Resumes receiving and decoding image updates.
    /// </summary>
    [ContextMenu("Resume Receiving")]
    public void ResumeReceiving()
    {
        _session?.ResumeReceiving();
    }

    /// <summary>
    /// Performs a clean receiver restart: closes current signaling/peer and starts listening again.
    /// </summary>
    [ContextMenu("Restart Receiving Clean")]
    public void RestartReceiving()
    {
        _ = RestartReceivingAsync();
    }

    /// <summary>
    /// Async clean receiver restart.
    /// </summary>
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

    /// <summary>
    /// Updates the arrays shown in the inspector for runtime diagnostics.
    /// </summary>
    internal void SetInspectionArrays(Texture[] textures, string[] trackIds, string[] mids)
    {
        ReceivedTextures = textures;
        ReceivedTrackIds = trackIds;
        ReceivedTransceiverMids = mids;
    }

    protected virtual void OnDestroy()
    {
        _session?.Dispose();
        _session = null;
        StopWebRtcUpdateLoop();
    }

    private void ClearInspectionArrays()
    {
        int count = Mathf.Max(OutputImages?.Length ?? 0, 0);

        ReceivedTextures = new Texture[count];
        ReceivedTrackIds = new string[count];
        ReceivedTransceiverMids = new string[count];
    }

    private void ClearOutputImages()
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
