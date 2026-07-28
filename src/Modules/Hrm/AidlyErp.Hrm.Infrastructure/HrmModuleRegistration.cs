using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Hrm.Infrastructure;

/// <summary>
/// Composition entry point for the Hrm module. The Host calls this and nothing else — the
/// module's context, repositories and contract implementations stay <c>internal</c>.
/// </summary>
public static class HrmModuleRegistration
{
    public static IServiceCollection AddHrmModule(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IHrmDbContext, HrmDbContext>(connectionString);

        services.AddScoped<AidlyErp.Hrm.Contracts.IEmployeeDirectory, EmployeeDirectory>();
        services.AddScoped<AidlyErp.Hrm.Contracts.IHrmLookups, HrmLookups>();
        return services;
    }
}
