using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aidly.src.Modules.Core.Domain.Interfaces.User;
using Aidly.src.Modules.Core.Domain.Interfaces.Company;
using Aidly.src.Modules.Core.Application.Services.User;
using Aidly.src.Modules.Core.Application.Services.Company;
using Aidly.src.Modules.Core.Infrastructure.Persistence;
using Aidly.src.Modules.Core.Infrastructure.Persistence.Repositories.User;
using Aidly.src.Modules.Core.Infrastructure.Persistence.Repositories.Company;
using Aidly.src.Shared.Infrastructure.Modules;

namespace Aidly.src.Modules.Core;

public class CoreModule : IModule
{
    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // Add DbContext using PostgreSQL. 
        // Note: We removed `.UseSnakeCaseNamingConvention()` because your existing database
        // columns appear to use PascalCase (like "UserNo").
        // services.AddDbContext<CoreDbContext>(options =>
        //     options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
services.AddDbContext<CoreDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());
        // Register Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // Register Application Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Controllers are mapped generally in Program.cs.
        // If Minimal APIs were used, we'd define them here.
        return endpoints;
    }
}
