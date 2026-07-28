using AidlyErp.Fin.Application.Interfaces;
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

        return services;
    }
}
