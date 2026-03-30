using System;

[Serializable]
public class UdpRecieverSettings
{
    public string IP;
    public int Port;

    public UdpRecieverSettings(string ip, int port)
    {
        IP = ip;
        Port = port;
    }
}