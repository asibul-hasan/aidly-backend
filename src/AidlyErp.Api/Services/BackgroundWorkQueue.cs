using System.Threading.Channels;

namespace AidlyErp.Api.Services;

/// <summary>
/// Bounded queue for fire-and-forget work — most notably the audit-log writer in
/// <c>SysLogService</c>. The .NET counterpart of the Java <c>AsyncConfig</c> executor.
///
/// <para><b>Why this is bounded rather than unbounded.</b> The Java configuration documents a
/// production incident: without an explicit executor, Spring fell back to
/// <c>SimpleAsyncTaskExecutor</c>, which creates a thread per task and never reuses them.
/// Combined with the audit log's <c>REQUIRES_NEW</c> transaction — which opens a *second* DB
/// connection per request — that exhausted the connection pool under load
/// (<c>total=0, active=0, idle=0, waiting=N</c>).</para>
///
/// <para>The same hazard exists here: .NET's <c>SysLogService</c> writes audit rows on its own
/// connection. So the pool is capped, the queue is capped, and when the queue is full the work
/// runs on the caller's thread (the equivalent of Java's <c>CallerRunsPolicy</c>) — applying
/// backpressure rather than dropping audit entries.</para>
/// </summary>
public interface IBackgroundWorkQueue
{
    /// <summary>
    /// Queues work. If the queue is full the delegate is executed inline on the calling thread,
    /// so an audit entry is never silently lost.
    /// </summary>
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work,
                           CancellationToken cancellationToken = default);

    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class BackgroundWorkQueue : IBackgroundWorkQueue
{
    /// <summary>Matches the Java executor's queue capacity.</summary>
    public const int QueueCapacity = 200;

    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;
    private readonly IServiceProvider _services;
    private readonly ILogger<BackgroundWorkQueue> _logger;

    public BackgroundWorkQueue(IServiceProvider services, ILogger<BackgroundWorkQueue> logger)
    {
        _services = services;
        _logger = logger;

        _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
            new BoundedChannelOptions(QueueCapacity)
            {
                // Never block the request thread waiting for queue space — TryWrite fails fast
                // and we fall back to running inline.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public async ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work,
                                        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (_channel.Writer.TryWrite(work)) return;

        // Queue is saturated — CallerRuns. Slower for this request, but the entry is not dropped.
        _logger.LogWarning("Background work queue is full ({Capacity}); running the task inline", QueueCapacity);

        using var scope = _services.CreateScope();
        try
        {
            await work(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fire-and-forget work must never surface as a request failure.
            _logger.LogError(ex, "Inline background task failed");
        }
    }

    public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}

/// <summary>
/// Drains <see cref="IBackgroundWorkQueue"/> on a small fixed set of workers, so queued work can
/// never outgrow the connection pool. Mirrors the Java executor's core 2 / max 5 sizing.
/// </summary>
public sealed class BackgroundWorkQueueHostedService : BackgroundService
{
    private const int WorkerCount = 5;

    private readonly IBackgroundWorkQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<BackgroundWorkQueueHostedService> _logger;

    public BackgroundWorkQueueHostedService(IBackgroundWorkQueue queue, IServiceProvider services,
                                            ILogger<BackgroundWorkQueueHostedService> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background work queue started with {Workers} worker(s), capacity {Capacity}",
            WorkerCount, BackgroundWorkQueue.QueueCapacity);

        var workers = Enumerable.Range(0, WorkerCount).Select(_ => RunWorkerAsync(stoppingToken));
        return Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task> work;

            try
            {
                work = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return; // shutting down
            }

            // Each task gets its own scope so it never shares a DbContext with a request.
            using var scope = _services.CreateScope();
            try
            {
                await work(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task failed");
            }
        }
    }
}
