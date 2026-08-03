using AidlyErp.Pur.Application.Interfaces;
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

        return services;
    }
}
