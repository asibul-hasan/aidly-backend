using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Aidly.src.Shared.Infrastructure.Modules;

public static class ModuleExtensions
{
    private static readonly List<IModule> RegisteredModules = new();

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration, params Assembly[] assemblies)
    {
        var scanAssemblies = assemblies.Length > 0 ? assemblies : new[] { Assembly.GetExecutingAssembly() };

        var moduleTypes = scanAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IModule).IsAssignableFrom(t))
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            var module = (IModule)Activator.CreateInstance(moduleType)!;
            RegisteredModules.Add(module);
            module.RegisterModule(services, configuration);
        }

        return services;
    }

    public static IApplicationBuilder UseModules(this IApplicationBuilder app)
    {
        // Currently purely logical grouping. We can extend this to run startup logic per module if needed.
        return app;
    }

    public static Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapModuleEndpoints(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        foreach (var module in RegisteredModules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
