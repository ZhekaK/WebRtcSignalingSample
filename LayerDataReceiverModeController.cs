using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using TabletTypes;
using UnityEngine;

public sealed class LayerDataReceiverModeController : IDisposable
{
    public static LayerDataReceiverModeController Instance { get; private set; }

    public event EventHandler<ReceiveTabletInterfaceEventArgs> OnReceiveTabletInterface;
    public event EventHandler<EvsStateChangedEventArgs> OnEvsStateChanged;

    public TabletInterface Data { get; private set; }

    private bool _isSubscribed;
    private bool _hasEvsState = false;
    private bool _lastEvsState;
    private ImitatorVisibleLayer _lastVisibleLayer;


    /// <summary> Initialize controller as singleton </summary>
    public static void InitializeController()
    {
        if (Instance != null) return;

        Instance = new LayerDataReceiverModeController();
        Instance.Subscribe();
        UtilityMonoBehaviourHooks.OnApplicationQuitting += OnApplicationQuitting;
    }

    private static void OnApplicationQuitting()
    {
        UtilityMonoBehaviourHooks.OnApplicationQuitting -= OnApplicationQuitting;
        Instance?.Dispose();
        Instance = null;
    }

    public void Dispose()
    {
        Unsubscribe();
        OnReceiveTabletInterface = null;
        OnEvsStateChanged = null;
    }

    private void Subscribe()
    {
        if (_isSubscribed) return;

        if (!UdpReceiver.Instance)
        {
            Debug.LogWarning("UdpReceiver is not initialized. LayerDataReceiverModeController can't subscribe to UDP data.");
            return;
        }

        UdpReceiver.Instance.OnReceiveTabletData += ReceiveData;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        if (UdpReceiver.Instance)
            UdpReceiver.Instance.OnReceiveTabletData -= ReceiveData;

        _isSubscribed = false;
    }

    private void ReceiveData(object sender, ReceiveDataEventArgs e)
    {
        if (e.Bytes == null || e.Bytes.Length == 0) return;
        if (!TryDeserializeTabletInterface(e.Bytes, out TabletInterface tabletInterface)) return;

        UtilityMainThreadDispatcher.Enqueue(() => ProcessTabletInterface(tabletInterface));
    }

    private void ProcessTabletInterface(TabletInterface tabletInterface)
    {
        Data = tabletInterface;
        OnReceiveTabletInterface?.Invoke(this, new ReceiveTabletInterfaceEventArgs(tabletInterface));

        if (TryExtractLayerState(tabletInterface, out bool evsOn, out ImitatorVisibleLayer layer) && (!_hasEvsState || _lastEvsState != evsOn || _lastVisibleLayer != layer))
        {
            _lastEvsState = evsOn;
            _lastVisibleLayer = layer;
            _hasEvsState = true;
            OnEvsStateChanged?.Invoke(this, new EvsStateChangedEventArgs(layer, evsOn));
        }
    }

    private bool TryDeserializeTabletInterface(byte[] bytes, out TabletInterface tabletInterface)
    {
        tabletInterface = null;

        if (TryDeserializeJson(bytes, out tabletInterface))
            return true;

        if (TryDeserializeBinaryFormatter(bytes, out tabletInterface))
            return true;

        return false;
    }

    private bool TryDeserializeJson(byte[] bytes, out TabletInterface tabletInterface)
    {
        tabletInterface = null;
        if (!LooksLikeJsonPayload(bytes)) return false;

        try
        {
            string json = Encoding.UTF8.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
            tabletInterface = JsonUtility.FromJson<TabletInterface>(json);
            return tabletInterface != null;
        }
        catch
        {
            return false;
        }
    }

    private bool TryDeserializeBinaryFormatter(byte[] bytes, out TabletInterface tabletInterface)
    {
        tabletInterface = null;
        if (!LooksLikeBinaryFormatterPayload(bytes)) return false;

        try
        {
#pragma warning disable SYSLIB0011
            using MemoryStream stream = new(bytes);
            BinaryFormatter formatter = new();
            tabletInterface = formatter.Deserialize(stream) as TabletInterface;
#pragma warning restore SYSLIB0011
            return tabletInterface != null;
        }
        catch
        {
            return false;
        }
    }

    private bool LooksLikeJsonPayload(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return false;

        for (int i = 0; i < bytes.Length; i++)
        {
            byte symbol = bytes[i];
            if (symbol == 9 || symbol == 10 || symbol == 13 || symbol == 32) continue;
            return symbol == (byte)'{';
        }

        return false;
    }

    private bool LooksLikeBinaryFormatterPayload(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return false;
        return bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0;
    }

    private bool TryExtractLayerState(TabletInterface tabletInterface, out bool evsOn, out ImitatorVisibleLayer visibleLayer)
    {
        evsOn = false;
        visibleLayer = ImitatorVisibleLayer.Visible;
        if (tabletInterface == null) return false;
        if (tabletInterface.Evs == null) return false;
        if (tabletInterface.Evs.Settings == null) return false;

        visibleLayer = tabletInterface.Evs.Settings.ImitatorVisibleLayer;
        evsOn = tabletInterface.Evs.Settings.EvsOn;
        return true;
    }
}

public class ReceiveTabletInterfaceEventArgs : EventArgs
{
    public TabletInterface TabletInterface { get; private set; }

    public ReceiveTabletInterfaceEventArgs(TabletInterface tabletInterface)
    {
        TabletInterface = tabletInterface;
    }
}

public class EvsStateChangedEventArgs : EventArgs
{
    public bool CurrentStateEvs { get; private set; }
    public ImitatorVisibleLayer CurrentLayer { get; private set; }

    public EvsStateChangedEventArgs(ImitatorVisibleLayer currentLayer, bool currentStateEvs)
    {
        CurrentStateEvs = currentStateEvs;
        CurrentLayer = currentLayer;
    }
}
