using AidlyErp.Shared.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Shared.Infrastructure.Persistence;

/// <summary>
/// Shared wiring every module needs for its own persistence slice. Modules call this from their
/// <c>Add&lt;Module&gt;Module</c> extension so the Host never has to know a module's internals.
/// </summary>
public static class ModuleRegistration
{
    /// <summary>
    /// Registers a module's context plus the two abstractions bound to it: the transaction
    /// boundary (<see cref="IUnitOfWork{TContext}"/>) and the REQUIRES_NEW factory
    /// (<see cref="IModuleDbContextFactory{TContext}"/>).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TInterface, TContext>(
        this IServiceCollection services, string connectionString)
        where TContext : ModuleDbContext, TInterface
        where TInterface : class
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditingAndTenantInterceptor>();
            options.UseNpgsql(connectionString).AddInterceptors(interceptor);
        });

        services.AddScoped<TInterface>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IUnitOfWork<TInterface>, UnitOfWork<TInterface, TContext>>();
        services.AddSingleton<IModuleDbContextFactory<TInterface>, ModuleDbContextFactory<TInterface>>();

        return services;
    }
}
