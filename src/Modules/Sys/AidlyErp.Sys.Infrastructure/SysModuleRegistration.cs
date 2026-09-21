using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Sys.Infrastructure;

/// <summary>
/// Composition entry point for the Sys module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class SysModuleRegistration
{
    public static IServiceCollection AddSysModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ISysDbContext, SysDbContext>(connectionString);

        // Contracts this module publishes to the rest of the system.
        services.AddScoped<AidlyErp.Sys.Contracts.ISysUserDirectory, SysUserDirectory>();
        services.AddScoped<AidlyErp.Sys.Contracts.ISysBranchDirectory, SysBranchDirectory>();
        services.AddScoped<AidlyErp.Sys.Contracts.ISysSettingsStore, SysSettingsStore>();
        services.AddScoped<AidlyErp.Sys.Contracts.IDocSequenceGenerator, DocSequenceGenerator>();
        services.AddScoped<AidlyErp.Sys.Contracts.IFinCalendar, FinCalendar>();
        services.AddScoped<AidlyErp.Sys.Contracts.IApprovalRequestReader, ApprovalRequestReader>();
        services.AddScoped<AidlyErp.Sys.Contracts.IVatTaxLookup, VatTaxLookup>();
        services.AddScoped<AidlyErp.Sys.Contracts.ICurrencyLookup, CurrencyLookup>();
        services.AddScoped<AidlyErp.Sys.Contracts.IPartyLookup, PartyLookup>();
        services.AddScoped<AidlyErp.Sys.Contracts.IInvLookup, InvLookup>();
        return services;
    }
}
