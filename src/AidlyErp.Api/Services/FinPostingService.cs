using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AidlyErp.Application.Fin.Services;

namespace AidlyErp.Api.Services;

/// <summary>
/// Background outbox drain — periodically calls <see cref="IFinPostingService.DrainOnceAsync"/>
/// to process pending <c>sys_event_outbox</c> rows. Delegates all business logic (voucher
/// creation, ledger posting, balance updates) to the application-layer service which runs
/// inside a proper DI scope with tenant context.
/// </summary>
public class FinPostingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FinPostingBackgroundService> _logger;

    public FinPostingBackgroundService(IServiceProvider serviceProvider, ILogger<FinPostingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FinPostingBackgroundService started.");
        await Task.Delay(20000, stoppingToken); // Initial delay for app startup

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var postingService = scope.ServiceProvider.GetRequiredService<IFinPostingService>();
                int handled = await postingService.DrainOnceAsync(stoppingToken);
                if (handled > 0)
                    _logger.LogInformation("FinPostingBackgroundService drained {Count} outbox event(s).", handled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinPostingBackgroundService error: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
