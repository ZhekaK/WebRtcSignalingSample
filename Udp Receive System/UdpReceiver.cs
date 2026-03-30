using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class UdpReceiver : MonoBehaviour
{
    public static UdpReceiver Instance { get; private set; }

    public event EventHandler<ReceiveDataEventArgs> OnReceiveTabletData;
    public event EventHandler<ReceiveDataEventArgs> OnReceiveCoordinatesData;

    private CancellationTokenSource _cancellationTokenSourceTabletRecieved;
    private CancellationTokenSource _cancellationTokenSourceCoordinatesRecieved;

    private UdpClient _udpCoordinatesClient;
    private UdpClient _udpTabletClient;


    /// <summary> Initialize reciever as singleton </summary>
    public static void InitializeReceiver()
    {
        if (Instance) return;

        GameObject reciever = new(nameof(UdpReceiver));
        Instance = reciever.AddComponent<UdpReceiver>();
        DontDestroyOnLoad(reciever);

        Instance.InitializeReciever();
    }

    /// <summary> Initialize and start UDP reciever </summary>
    public void InitializeReciever()
    {
        CreateCoordinatesClient();
        CreateTabletClient();
    }

    private void CreateCoordinatesClient()
    {
        _udpCoordinatesClient = new UdpClient();
        _udpCoordinatesClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpCoordinatesClient.Client.ExclusiveAddressUse = false;
        _udpCoordinatesClient.Client.Bind(new IPEndPoint(IPAddress.Any, SaveManager.Instance.Settings.UdpCoordinatesReciever.Port));
        _udpCoordinatesClient.JoinMulticastGroup(IPAddress.Parse(SaveManager.Instance.Settings.UdpCoordinatesReciever.IP));

        _cancellationTokenSourceCoordinatesRecieved = new CancellationTokenSource();

        Task.Run(() => ReceiveCoordinatesLoopAsync(_cancellationTokenSourceCoordinatesRecieved.Token));
    }

    private void CreateTabletClient()
    {
        _udpTabletClient = new UdpClient();
        _udpTabletClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpTabletClient.Client.ExclusiveAddressUse = false;
        _udpTabletClient.Client.Bind(new IPEndPoint(IPAddress.Any, SaveManager.Instance.Settings.UdpLayerReciever.Port));
        _udpTabletClient.JoinMulticastGroup(IPAddress.Parse(SaveManager.Instance.Settings.UdpLayerReciever.IP));

        _cancellationTokenSourceTabletRecieved = new CancellationTokenSource();

        Task.Run(() => ReceiveTabletLoopAsync(_cancellationTokenSourceTabletRecieved.Token));
    }

    private async Task ReceiveCoordinatesLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var receiveResult = await _udpCoordinatesClient.ReceiveAsync();
                OnReceiveCoordinatesData?.Invoke(this, new ReceiveDataEventArgs(receiveResult.Buffer));
            }
            catch (SocketException ex)
            {
                Debug.LogError(ex.ToString());
            }
        }
    }

    private async Task ReceiveTabletLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var receiveResult = await _udpTabletClient.ReceiveAsync();
                OnReceiveTabletData?.Invoke(this, new ReceiveDataEventArgs(receiveResult.Buffer));
            }
            catch (SocketException ex)
            {
                Debug.LogError(ex.ToString());
            }
        }
    }

    /// <summary> Destroy and dispose UDP reciever </summary>
    public void DestroyReciever()
    {
        _cancellationTokenSourceCoordinatesRecieved?.Cancel();
        _cancellationTokenSourceCoordinatesRecieved?.Dispose();
        _cancellationTokenSourceTabletRecieved?.Cancel();
        _cancellationTokenSourceTabletRecieved?.Dispose();

        _udpCoordinatesClient?.Close();
        _udpCoordinatesClient?.Dispose();
        _udpTabletClient?.Close();
        _udpTabletClient?.Dispose();
    }

    private void OnApplicationQuit() => DestroyReciever();
}