using FlexNet.Extensions;
using FlexNet.Server;
using FlexNet.Vibe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Collections;
using UnityEngine;

public sealed class FlexNetSignalingServer : IDisposable
{
    private readonly FlexNetSignalingServerOptions _options;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly object _lifecycleLock = new();

    private FlexServer _server;
    private bool _isStarted;
    private bool _isDisposed;

    public FlexNetSignalingServer()
        : this(new FlexNetSignalingServerOptions())
    {
    }

    public FlexNetSignalingServer(FlexNetSignalingServerOptions options)
    {
        _options = options ?? new FlexNetSignalingServerOptions();
        _memoryStreamManager = CreateMemoryStreamManager(_options);
    }

    public bool IsStarted => _isStarted;

    public FlexServer.Builder CreateBuilder()
    {
        FlexServer.Builder builder = new FlexServer.Builder();
        builder.Services.AddSingleton(_options.EndPoint);
        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton(_memoryStreamManager);
        builder.UseListener<VibeListener>();
        builder.Services.AddContentCodecs();
        FlexSignalingControllerRegistration.AddSignalingControllers(builder);
        return builder;
    }

    public FlexServer BuildServer()
    {
        return CreateBuilder().Build();
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_isDisposed || _isStarted)
                return;

            _server = BuildServer();
            _server.Run();
            _isStarted = true;
        }

        Log($"Started on {_options.EndPoint}.");
    }

    public void Stop()
    {
        FlexServer serverToStop;

        lock (_lifecycleLock)
        {
            if (!_isStarted && _server == null)
                return;

            serverToStop = _server;
            _server = null;
            _isStarted = false;
        }

        try
        {
            serverToStop?.Stop();
        }
        catch (Exception ex)
        {
            LogWarning($"Stop() failed: {ex.Message}");
        }

        CloseServerConnections(serverToStop);
        Log("Stopped.");
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
        }

        Stop();
    }

    private static RecyclableMemoryStreamManager CreateMemoryStreamManager(FlexNetSignalingServerOptions options)
    {
        return new RecyclableMemoryStreamManager(
            new RecyclableMemoryStreamManager.Options
            {
                BlockSize = options.MemoryBlockSizeBytes,
                LargeBufferMultiple = options.LargeBufferMultipleBytes,
                MaximumBufferSize = options.MaximumBufferSizeBytes,
                MaximumLargePoolFreeBytes = options.MaximumLargePoolFreeBytes,
                MaximumSmallPoolFreeBytes = options.MaximumSmallPoolFreeBytes,
            });
    }

    private void CloseServerConnections(FlexServer server)
    {
        IEnumerable connections = server?.Connections as IEnumerable;
        if (connections == null)
            return;

        int closedConnections = 0;
        foreach (object connection in connections)
        {
            if (connection == null)
                continue;

            try
            {
                connection.GetType().GetMethod("Close")?.Invoke(connection, null);
            }
            catch
            {
            }

            try
            {
                if (connection is IDisposable disposable)
                    disposable.Dispose();
            }
            catch
            {
            }

            closedConnections++;
        }

        if (closedConnections > 0)
            Log($"Closed {closedConnections} active connection(s).");
    }

    private void Log(string message)
    {
        if (_options.EnableLogging)
            Debug.Log($"[SignalingServer] {message}");
    }

    private void LogWarning(string message)
    {
        if (_options.EnableLogging)
            Debug.LogWarning($"[SignalingServer] {message}");
    }
}
