using System;

[Serializable]
public class Settings
{
    public MovementSettings Movement = new(MovementType.None);
    public UdpRecieverSettings UdpCoordinatesReciever = new("235.10.10.10", 1603);
    public UdpRecieverSettings UdpLayerReciever = new("235.10.10.10", 1641);
    public WebRtcSettings WebRtcSender = new("10.24.50.23", 8005, 100, 300);
    public NetworkSettings Network = new("10.24.65.100", "10.24.50.23");
}