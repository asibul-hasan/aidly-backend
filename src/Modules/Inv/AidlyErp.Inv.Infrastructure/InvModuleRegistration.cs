using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Inv.Infrastructure;

/// <summary>
/// Composition entry point for the Inv module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class InvModuleRegistration
{
    public static IServiceCollection AddInvModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IInvDbContext, InvDbContext>(connectionString);

        services.AddScoped<AidlyErp.Inv.Contracts.IInvCatalog, InvCatalog>();
        return services;
    }
}
