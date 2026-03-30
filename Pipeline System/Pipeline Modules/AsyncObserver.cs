using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public abstract class AsyncObserver<T> : IObserver<T>, IDisposable
{
    private readonly Channel<T> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;

    public AsyncObserver(BoundedChannelOptions options = default)
    {
        if (options == default)
            options = new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            };

        _channel = Channel.CreateBounded<T>(options);
        _consumerTask = Task.Run(ConsumeAsync);
    }

    /// <summary> Provides the observer with new data </summary>
    public void OnNext(T value)
    {
        _channel.Writer.TryWrite(value);
    }

    /// <summary> Notifies the observer that the provider has encountered an error </summary>
    public void OnError(Exception error)
    {
        _channel.Writer.TryComplete(error);
    }

    /// <summary> Notifies the observer that the provider has completed sending push notifications </summary>
    public void OnCompleted()
    {
        _channel.Writer.TryComplete();
    }

    protected virtual async Task ConsumeAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
                await OnNextAsync(item);
            await OnCompletedAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await OnErrorAsync(ex);
        }
    }

    /// <summary> Provides the observer with new data </summary>
    public abstract Task OnNextAsync(T value);

    /// <summary> Notifies the observer that the provider has encountered an error </summary>
    public abstract Task OnCompletedAsync();

    /// <summary> Notifies the observer that the provider has completed sending push notifications </summary>
    public abstract Task OnErrorAsync(Exception exception);

    /// <summary> Dispose the observer </summary>
    public virtual void Dispose()
    {
        _cts.Cancel();
        _consumerTask.Wait();
        _cts.Dispose();
    }
}