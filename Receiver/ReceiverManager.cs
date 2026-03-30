using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public enum ReceiverTransportMode
{
    DirectPeer = 0,
    MediaServer = 1
}

public class ReceiverManager : MonoBehaviour
{
    [Header("Mode")]
    public ReceiverTransportMode RuntimeMode = ReceiverTransportMode.MediaServer;

    [Header("Connection")]
    public string IP = "127.0.0.1";
    public int Port = 8005;

    [Header("Output")]
    public RawImage[] OutputImages;

    [Header("Behavior")]
    public bool AutoRequestDefaultLayout = true;

    private ReceiverSession _session;

    async void Start()
    {
        _session = new ReceiverSession(this);
        await _session.InitializeAsync();
    }

    public async Task RestartReceivingAsync()
    {
        if (_session == null)
            _session = new ReceiverSession(this);

        await _session.RestartAsync();
    }

    [ContextMenu("Request Default Layout")]
    public void RequestDefaultLayout()
    {
        _session?.SendDefaultRequest();
    }

    public bool RequestLayer(RenderLayer layer)
    {
        return _session?.SendMediaSubscriptionRequest(new MediaSubscriptionRequest
        {
            clientName = "Receiver",
            useDefaultLayout = false,
            subscriptions = new[]
            {
                new MediaSubscriptionEntry
                {
                    sourceId = $"display-1-{layer.ToString().ToLower()}",
                    clientSlotIndex = 0,
                    clientMonitorIndex = 0,
                    clientPanelIndex = 0
                }
            }
        }) ?? false;
    }

    [ContextMenu("Test Visible Layer")]
    public void TestVisible()
    {
        RequestLayer(RenderLayer.Visible);
    }

    public void ApplyTexture(int index, Texture tex)
    {
        if (OutputImages == null || index >= OutputImages.Length)
            return;

        if (OutputImages[index] != null)
            OutputImages[index].texture = tex;
    }
}
