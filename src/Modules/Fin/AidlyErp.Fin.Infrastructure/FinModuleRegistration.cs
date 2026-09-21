using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Infrastructure.Repositories;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Fin.Infrastructure;

/// <summary>
/// Composition entry point for the Fin module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class FinModuleRegistration
{
    public static IServiceCollection AddFinModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IFinDbContext, FinDbContext>(connectionString);

        // Repositories
        services.AddScoped<IFinAccountRepository, FinAccountRepository>();
        services.AddScoped<IFinAccountGroupRepository, FinAccountGroupRepository>();
        services.AddScoped<IFinAccountBalanceRepository, FinAccountBalanceRepository>();
        services.AddScoped<IFinVoucherRepository, FinVoucherRepository>();
        services.AddScoped<IFinVoucherDtlRepository, FinVoucherDtlRepository>();
        services.AddScoped<IFinVoucherTypeRepository, FinVoucherTypeRepository>();
        services.AddScoped<IFinBankAccountRepository, FinBankAccountRepository>();
        services.AddScoped<IFinBankReconRepository, FinBankReconRepository>();
        services.AddScoped<IFinBankReconLineRepository, FinBankReconLineRepository>();
        services.AddScoped<IFinGlMapRepository, FinGlMapRepository>();
        services.AddScoped<IFinLedgerRepository, FinLedgerRepository>();

        return services;
    }
}
