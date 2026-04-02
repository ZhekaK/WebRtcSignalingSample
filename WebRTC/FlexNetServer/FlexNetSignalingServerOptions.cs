using System.Net;

public sealed class FlexNetSignalingServerOptions
{
    public IPEndPoint EndPoint { get; set; } = new(IPAddress.Loopback, 1611);
    public bool EnableLogging { get; set; } = true;
    public bool LogPollTimeouts { get; set; } = false;
    public int MemoryBlockSizeBytes { get; set; } = 128 * 1024;
    public int LargeBufferMultipleBytes { get; set; } = 1024 * 1024;
    public int MaximumBufferSizeBytes { get; set; } = 16 * 1024 * 1024;
    public int MaximumLargePoolFreeBytes { get; set; } = 64 * 1024 * 1024;
    public int MaximumSmallPoolFreeBytes { get; set; } = 32 * 1024 * 1024;
    public int DefaultPollTimeoutMs { get; set; } = 25000;
    public int ClientLeaseSeconds { get; set; } = 30;
}
