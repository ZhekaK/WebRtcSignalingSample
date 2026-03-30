using System;

[Serializable]
public class WebRtcSettings
{
    public string IP;
    public int Port;
    public int TotalMaxBitrateMbps;
    public int TotalMinBitrateMbps;

    public WebRtcSettings(string ip, int port, int minBitrate, int maxBitrate)
    {
        IP = ip;
        Port = port;
        TotalMaxBitrateMbps = maxBitrate;
        TotalMinBitrateMbps = minBitrate;
    }
}
