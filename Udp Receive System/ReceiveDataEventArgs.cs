using System;

public class ReceiveDataEventArgs : EventArgs
{
    public byte[] Bytes { get; private set; }

    public ReceiveDataEventArgs(byte[] bytes)
    {
        Bytes = bytes;
    }
}