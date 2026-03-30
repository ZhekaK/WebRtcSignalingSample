using FlexNet.Interfaces;
using NetworkDTO.Types;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;


public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    public Action<List<RecognitionObject>> OnFramesRecieved;
    public Action OnEmptyFrames;

    [Header("- - Demonstrator Settings:")]
    public string IPAddressDemon = "10.24.65.100";
    [SerializeField, DisableEdit] private List<NetworkDisplay> EvsDemonPorts = new();
    [SerializeField, DisableEdit] private List<NetworkDisplay> FramesDemonPorts = new();

    [Header("- - Neural Net Settings:")]
    public string IPAddressNeural = "127.0.0.1";
    [SerializeField] private int NeuralPort = 1604;
    //public bool useNeuro => FrameManager.Instance.UseNeuroFrames;

    [Header("- - Send/Recieve")]
    public List<EvsClient> EvsClients = new();
    public FrameClient FrameClient;
    //public NeuralClient NeuralClient;


    public static void InitializeManager()
    {
        GameObject NetworkManager = new("Network Manager");
        Instance = NetworkManager.AddComponent<NetworkManager>();
        DontDestroyOnLoad(NetworkManager);

        Instance.InitializePorts();
        Instance.InitializeClients();

#warning убраны SendUIController.OnChangeDemonIP += Instance.ChangeDemonstratorHost; и SendUIController.OnChangeNeuralIP += Instance.ChangeNeuralNetHost; и перенесены в SendUIController
    }

    private void Awake()
    {
        Instance = this;
        InitializePorts();
        InitializeClients();
    }

    private void InitializePorts()
    {
        var displayDatas = DisplaysManager.Instance?.DisplaysDatas;

        foreach (var dataData in displayDatas.Keys)
        {
            // Инициализируем порты отправки Evs в Демонстратор
            EvsDemonPorts.Add(new((int)dataData, NetworkDisplay.PortType.ImagePort));

            // Инициализируем порты отправки Frames в Демонстратор
            if (dataData == 0)
                FramesDemonPorts.Add(new((int)dataData, NetworkDisplay.PortType.FramePort));
        }

        IPAddressDemon = SaveManager.Instance.Settings.Network.IPAddressDemon;
        IPAddressNeural = SaveManager.Instance.Settings.Network.IPAddressNeural;
    }

    private void InitializeClients()
    {
        // Создаем клиентов отправки Evs в Демонстратор
        foreach (NetworkDisplay evsPort in EvsDemonPorts)
        {
            var newClient = new EvsClient();
            EvsClients.Add(newClient);
            _ = Task.Run(() => newClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(IPAddressDemon), evsPort.PortForUse)));
        }

        // Создаем клиентов отправки Frames в Демонстратор
        foreach (NetworkDisplay framesPort in FramesDemonPorts)
        {
            FrameClient = new FrameClient();
            _ = Task.Run(() => FrameClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(IPAddressDemon), framesPort.PortForUse)));
        }

        //// Создаем клиента отправки Evs в Нейронную сеть
        //NeuralClient = new NeuralClient(this);
        //_ = NeuralClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(IPAddressNeural), NeuralPort));
    }

    public EvsClient GetClient(int port)
    {
        try
        {
            return EvsClients.Find(evs => evs.IPEndPoint.Port == port);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return null;
        }
    }

    public EvsClient GetClient(IFlexClient client)
    {
        try
        {
            return EvsClients.Find(evs => evs.Client == client);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return null;
        }
    }

    //public NeuralClient GetNeuralClient(int port)
    //{
    //    return NeuralClient;
    //}

    public void ChangeDemonstratorHost(IPAddress address)
    {
        foreach (var client in EvsClients)
        {
            _ = Task.Run(() => client.ConnectAsync(new(address, client.IPEndPoint.Port)));
        }
        _ = Task.Run(() => FrameClient.ConnectAsync(new(address, FrameClient.IPEndPoint.Port)));
    }

    //public void ChangeNeuralNetHost(IPAddress address)
    //{
    //    _ = Task.Run(() => NeuralClient.ConnectAsync(new(address, NeuralClient.IPEndPoint.Port)));
    //}

    private void OnDestroy()
    {
        for (int i = 0; i < EvsClients.Count; i++)
            EvsClients[i].Dispose();

        FrameClient.Dispose();
        //NeuralClient.Dispose();
    }
}