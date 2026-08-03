using AidlyErp.Sys.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Infrastructure.Persistence;

namespace AidlyErp.Api.Services;

public class DbFixer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbFixer> _logger;

    public DbFixer(IServiceProvider serviceProvider, ILogger<DbFixer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running DbFixer to clean up null row_versions and patch schema...");
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SysDbContext>();

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE hrm_employee ADD COLUMN IF NOT EXISTS taxpayer_class VARCHAR(30) NOT NULL DEFAULT 'GENERAL'", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE hrm_employee SET taxpayer_class = 'GENERAL' WHERE taxpayer_class IS NULL OR BTRIM(taxpayer_class) = ''", cancellationToken);
            _logger.LogInformation("Successfully checked and updated taxpayer_class column on hrm_employee.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DbFixer migration error for taxpayer_class: {Message}", ex.Message);
        }

        try
        {
            int menus = await db.Database.ExecuteSqlRawAsync("UPDATE sys_menu SET row_version = 1 WHERE row_version IS NULL", cancellationToken);
            int submodules = await db.Database.ExecuteSqlRawAsync("UPDATE sys_submodule SET row_version = 1 WHERE row_version IS NULL", cancellationToken);
            int modules = await db.Database.ExecuteSqlRawAsync("UPDATE sys_module SET row_version = 1 WHERE row_version IS NULL", cancellationToken);
            _logger.LogInformation("Fixed null row_versions: {Menus} menus, {Submodules} submodules, {Modules} modules.", menus, submodules, modules);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fix row versions: {Message}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
