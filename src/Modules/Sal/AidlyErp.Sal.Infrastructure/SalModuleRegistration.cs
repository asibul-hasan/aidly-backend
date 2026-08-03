using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Sal.Infrastructure;

/// <summary>
/// Composition entry point for the Sal module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class SalModuleRegistration
{
    public static IServiceCollection AddSalModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ISalDbContext, SalDbContext>(connectionString);

        return services;
    }
}
