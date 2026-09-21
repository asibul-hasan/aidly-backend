using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Pur.Infrastructure.Repositories;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Pur.Infrastructure;

/// <summary>
/// Composition entry point for the Pur module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class PurModuleRegistration
{
    public static IServiceCollection AddPurModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IPurDbContext, PurDbContext>(connectionString);

        // Repository layer
        services.AddScoped<IPurSupplierRepository, PurSupplierRepository>();
        services.AddScoped<IPurInvoiceRepository, PurInvoiceRepository>();
        services.AddScoped<IPurPaymentRepository, PurPaymentRepository>();
        services.AddScoped<IPurSupplierLedgerRepository, PurSupplierLedgerRepository>();

        // Contract implementations
        services.AddScoped<AidlyErp.Pur.Contracts.IPurApLedgerReader, PurApLedgerReader>();

        return services;
    }
}
